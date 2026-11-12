// 行政区应用服务实现（Application 层）。
// 包含层级树构建、唯一键冲突检查、删除子节点检查等业务规则。
// 读取时按语言回退链合并翻译子表；没有译文则回退到主表默认语言原值。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Application.ObjectMapping;
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Exceptions;
using Axiom.ReferenceData.Domain.Repositories;
using Axiom.SharedKernel.Localization;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Application.Services;

public sealed class GeoDivisionAppService(
    IGeoDivisionRepository repository,
    IReferenceEnumRepository enumRepository,
    AxiomLocalizationOptions localization) : IGeoDivisionAppService
{
    public async Task<PagedResponse<GeoDivisionDto>> ListAsync(
        GeoDivisionListQuery query,
        string? culture = null,
        CancellationToken ct = default)
    {
        var paged = await repository.ListAsync(
            query.Page,
            Math.Clamp(query.PageSize, 1, 200),
            query.Level,
            NormalizeCountryCode(query.CountryCode),
            query.SubdivisionCode,
            query.Keyword,
            ct);

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync(
            paged.Items.Select(region => region.Id).ToArray(),
            chain,
            ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        return new PagedResponse<GeoDivisionDto>(
            paged.Items.Select(region => GeoDivisionMapper.ToDto(region, lookup)).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize);
    }

    public async Task<GeoDivisionTreeDto?> GetTreeAsync(
        string countryCode,
        int depth,
        string? culture = null,
        CancellationToken ct = default)
    {
        var cc = NormalizeCountryCode(countryCode);
        if (cc is null)
        {
            throw new GeoDivisionCountryNotFoundException(countryCode);
        }

        depth = Math.Clamp(depth, GeoDivision.RootLevel, GeoDivision.MaxTreeDepth);

        var regions = await repository.ListForTreeAsync(cc, depth, ct);
        var country = regions.FirstOrDefault(r => r.Level == GeoDivision.RootLevel && r.CountryCode == cc);
        if (country is null)
        {
            throw new GeoDivisionCountryNotFoundException(cc);
        }

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync(
            regions.Select(region => region.Id).ToArray(),
            chain,
            ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        var byParent = regions
            .Where(r => r.ParentId.HasValue)
            .GroupBy(r => r.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        return BuildTree(country, byParent, lookup);
    }

    public async Task<IReadOnlyList<GeoDivisionChildDto>> GetChildrenAsync(
        Guid id,
        string? culture = null,
        CancellationToken ct = default)
    {
        _ = await repository.GetByIdAsNoTrackingAsync(id, ct)
            ?? throw new GeoDivisionNotFoundException(id);

        var children = await repository.ListByParentAsync(id, ct);
        var grandchildren = await repository.ListByParentIdsAsync(
            children.Select(child => child.Id).ToArray(), ct);
        var byParent = grandchildren
            .GroupBy(grandchild => grandchild.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync(
            children.Select(child => child.Id).ToArray(),
            chain,
            ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        return children
            .Select(child =>
            {
                byParent.TryGetValue(child.Id, out var childChildren);
                // 同名检测使用默认语言名称：直辖市在数据源中可能是省、市两条同名记录。
                var sameNameChildCount = childChildren?.Count(grandchild =>
                    string.Equals(grandchild.Name?.Trim(), child.Name?.Trim(), StringComparison.Ordinal)) ?? 0;

                return new GeoDivisionChildDto(
                    child.Id,
                    child.Level,
                    child.Kind,
                    child.CountryCode,
                    child.SubdivisionCode,
                    child.Code,
                    lookup.Find(child.Id, GeoDivisionTranslationFields.Name) ?? child.Name,
                    child.Population,
                    childChildren?.Count ?? 0,
                    sameNameChildCount);
            })
            .ToList();
    }

    public async Task<GeoDivisionDto?> GetAsync(Guid id, string? culture = null, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsNoTrackingAsync(id, ct);
        if (entity is null)
        {
            return null;
        }

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync([id], chain, ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        return GeoDivisionMapper.ToDto(entity, lookup);
    }

    public async Task<(GeoDivisionDto Dto, Uri LocationUri)> CreateAsync(
        CreateGeoDivisionInput input,
        CancellationToken ct = default)
    {
        var countryCode = NormalizeCountryCode(input.CountryCode);

        // Kind 留空时按国家与深度推导默认类型；非空则按字典强校验。
        var kind = string.IsNullOrWhiteSpace(input.Kind)
            ? GeoDivisionKinds.DeriveDefault(countryCode ?? string.Empty, input.Level, input.SubdivisionCode)
            : GeoDivisionKinds.Normalize(input.Kind);
        await EnsureKindExistsAsync(kind, countryCode, ct);

        var entity = GeoDivision.Create(
            input.Level,
            input.CountryCode,
            input.Name,
            subdivisionCode: input.SubdivisionCode,
            code: input.Code,
            asciiName: input.AsciiName,
            localName: input.LocalName,
            latitude: input.Latitude,
            longitude: input.Longitude,
            population: input.Population,
            parentId: input.ParentId,
            kind: input.Kind);

        if (await repository.AnyByUniqueKeyAsync(entity.Level, entity.CountryCode, entity.Code, ct))
        {
            throw new GeoDivisionUniqueKeyConflictException(entity.CountryCode, entity.Level, entity.Code);
        }

        await repository.AddAsync(entity, ct);

        var dto = GeoDivisionMapper.ToDto(entity);
        var location = new Uri($"/api/geo-divisions/{entity.Id}", UriKind.Relative);
        return (dto, location);
    }

    public async Task<GeoDivisionDto> UpdateAsync(Guid id, UpdateGeoDivisionInput input, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new GeoDivisionNotFoundException(id);

        if (input.Kind is not null)
        {
            await EnsureKindExistsAsync(GeoDivisionKinds.Normalize(input.Kind), entity.CountryCode, ct);
        }

        entity.Update(
            input.Name,
            input.AsciiName,
            input.LocalName,
            input.Code,
            input.Latitude,
            input.Longitude,
            input.Population,
            input.ParentId,
            kind: input.Kind);

        await repository.UpdateAsync(entity, ct);
        return GeoDivisionMapper.ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new GeoDivisionNotFoundException(id);

        if (await repository.HasChildrenAsync(id, ct))
        {
            throw new GeoDivisionHasChildrenException(id);
        }

        await repository.RemoveAsync(entity, ct);
    }

    public async Task<TranslationDto?> GetTranslationsAsync(
        Guid id,
        string culture,
        CancellationToken ct = default)
    {
        if (await repository.GetByIdAsNoTrackingAsync(id, ct) is null)
        {
            return null;
        }

        var rows = await repository.GetTranslationsAsync(id, culture, ct);
        var values = rows.ToDictionary(
            _ => GeoDivisionTranslationFields.Name,
            row => row.Name);
        return new TranslationDto(culture, values);
    }

    public async Task<TranslationDto?> SaveTranslationsAsync(
        Guid id,
        string culture,
        SaveTranslationsInput input,
        CancellationToken ct = default)
    {
        if (await repository.GetByIdAsNoTrackingAsync(id, ct) is null)
        {
            return null;
        }

        var values = ValidateTranslationValues(input.Values);
        await repository.ReplaceTranslationsAsync(id, culture, values, ct);
        return new TranslationDto(culture, values);
    }

    public async Task DeleteTranslationsAsync(Guid id, string culture, CancellationToken ct = default)
    {
        if (await repository.GetByIdAsNoTrackingAsync(id, ct) is null)
        {
            throw new GeoDivisionNotFoundException(id);
        }

        await repository.DeleteTranslationsAsync(id, culture, ct);
    }

    // ---------- 私有辅助方法 ----------

    private GeoDivisionTreeDto BuildTree(
        GeoDivision node,
        Dictionary<Guid, List<GeoDivision>> byParent,
        TranslationLookup lookup)
    {
        if (!byParent.TryGetValue(node.Id, out var children))
        {
            return GeoDivisionMapper.ToTreeNode(node, children: [], lookup);
        }

        var childNodes = children
            .OrderBy(r => r.Code)
            .Select(child => BuildTree(child, byParent, lookup))
            .ToList();

        return GeoDivisionMapper.ToTreeNode(node, childNodes, lookup);
    }

    private static TranslationValue ToTranslationValue(GeoDivisionTranslation row) =>
        new(row.GeoDivisionId, row.Culture, GeoDivisionTranslationFields.Name, row.Name);

    private static Dictionary<string, string> ValidateTranslationValues(IReadOnlyDictionary<string, string> values)
    {
        var normalized = new Dictionary<string, string>();
        foreach (var (field, value) in values)
        {
            if (!GeoDivisionTranslationFields.IsKnown(field))
            {
                throw GeoDivisionValidationException.UnknownTranslatableField(field);
            }

            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                throw GeoDivisionValidationException.Required(field);
            }

            var maxLength = GeoDivisionTranslationFields.MaxLength(field);
            if (trimmed.Length > maxLength)
            {
                throw GeoDivisionValidationException.TooLong(field, maxLength);
            }

            normalized[field] = trimmed;
        }

        return normalized;
    }

    private static string? NormalizeCountryCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    /// <summary>Kind 强校验：优先行所属国家的分域字典，未维护时回退通用兜底域。</summary>
    private async Task EnsureKindExistsAsync(string kind, string? countryCode, CancellationToken ct)
    {
        var category = GeoDivisionKinds.CategoryFor(countryCode);
        if (await enumRepository.ExistsAsync(category, kind, ct))
        {
            return;
        }

        if (await enumRepository.ExistsAsync(ReferenceEnumCategories.GeoDivisionKind, kind, ct))
        {
            return;
        }

        throw GeoDivisionValidationException.InvalidKind();
    }
}
