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

public sealed class RegionAppService(
    IRegionRepository repository,
    AxiomLocalizationOptions localization) : IRegionAppService
{
    public async Task<PagedResponse<RegionDto>> ListAsync(
        RegionListQuery query,
        string? culture = null,
        CancellationToken ct = default)
    {
        var paged = await repository.ListAsync(
            query.Page,
            Math.Clamp(query.PageSize, 1, 200),
            query.Level,
            NormalizeCountryCode(query.CountryCode),
            query.Admin1Code,
            query.Keyword,
            ct);

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync(
            paged.Items.Select(region => region.Id).ToArray(),
            chain,
            ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        return new PagedResponse<RegionDto>(
            paged.Items.Select(region => RegionMapper.ToDto(region, lookup)).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize);
    }

    public async Task<RegionTreeDto?> GetTreeAsync(
        string countryCode,
        int depth,
        string? culture = null,
        CancellationToken ct = default)
    {
        var cc = NormalizeCountryCode(countryCode);
        if (cc is null)
        {
            throw new RegionCountryNotFoundException(countryCode);
        }

        depth = Math.Clamp(depth, 1, 3);

        var regions = await repository.ListForTreeAsync(cc, depth, ct);
        var country = regions.FirstOrDefault(r => r.Level == RegionLevel.Country && r.CountryCode == cc);
        if (country is null)
        {
            throw new RegionCountryNotFoundException(cc);
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

    public async Task<IReadOnlyList<RegionChildDto>> GetChildrenAsync(
        Guid id,
        string? culture = null,
        CancellationToken ct = default)
    {
        var parent = await repository.GetByIdAsNoTrackingAsync(id, ct)
            ?? throw new RegionNotFoundException(id);

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
                // 同名检测使用默认语言名称：直辖市在数据源中就是省、市两条同名记录。
                var sameNameChildCount = childChildren?.Count(grandchild =>
                    string.Equals(grandchild.Name?.Trim(), child.Name?.Trim(), StringComparison.Ordinal)) ?? 0;

                return new RegionChildDto(
                    child.Id,
                    (int)child.Level,
                    child.CountryCode,
                    child.Admin1Code,
                    child.Code,
                    lookup.Find(child.Id, RegionTranslationFields.Name) ?? child.Name,
                    child.Population,
                    childChildren?.Count ?? 0,
                    sameNameChildCount);
            })
            .ToList();
    }

    public async Task<RegionDto?> GetAsync(Guid id, string? culture = null, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsNoTrackingAsync(id, ct);
        if (entity is null)
        {
            return null;
        }

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync([id], chain, ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        return RegionMapper.ToDto(entity, lookup);
    }

    public async Task<(RegionDto Dto, Uri LocationUri)> CreateAsync(
        CreateRegionInput input,
        CancellationToken ct = default)
    {
        var level = ParseLevel(input.Level);
        var entity = Region.Create(
            level,
            input.CountryCode,
            input.Name,
            admin1Code: input.Admin1Code,
            code: input.Code,
            asciiName: input.AsciiName,
            localName: input.LocalName,
            latitude: input.Latitude,
            longitude: input.Longitude,
            population: input.Population,
            parentId: input.ParentId);

        if (await repository.AnyByUniqueKeyAsync(level, entity.CountryCode, entity.Code, ct))
        {
            throw new RegionUniqueKeyConflictException(entity.CountryCode, (int)level, entity.Code);
        }

        await repository.AddAsync(entity, ct);

        var dto = RegionMapper.ToDto(entity);
        var location = new Uri($"/api/regions/{entity.Id}", UriKind.Relative);
        return (dto, location);
    }

    public async Task<RegionDto> UpdateAsync(Guid id, UpdateRegionInput input, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new RegionNotFoundException(id);

        entity.Update(
            input.Name,
            input.AsciiName,
            input.LocalName,
            input.Code,
            input.Latitude,
            input.Longitude,
            input.Population,
            input.ParentId);

        await repository.UpdateAsync(entity, ct);
        return RegionMapper.ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new RegionNotFoundException(id);

        if (await repository.HasChildrenAsync(id, ct))
        {
            throw new RegionHasChildrenException(id);
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
        return new TranslationDto(culture, rows.ToDictionary(row => row.Field, row => row.Value));
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
            throw new RegionNotFoundException(id);
        }

        await repository.DeleteTranslationsAsync(id, culture, ct);
    }

    // ---------- 私有辅助方法 ----------

    private RegionTreeDto BuildTree(
        Region node,
        Dictionary<Guid, List<Region>> byParent,
        TranslationLookup lookup)
    {
        if (!byParent.TryGetValue(node.Id, out var children))
        {
            return RegionMapper.ToTreeNode(node, children: [], lookup);
        }

        var childNodes = children
            .OrderBy(r => r.Code)
            .Select(child => BuildTree(child, byParent, lookup))
            .ToList();

        return RegionMapper.ToTreeNode(node, childNodes, lookup);
    }

    private static TranslationValue ToTranslationValue(RegionTranslation row) =>
        new(row.RegionId, row.Culture, row.Field, row.Value);

    private static Dictionary<string, string> ValidateTranslationValues(IReadOnlyDictionary<string, string> values)
    {
        var normalized = new Dictionary<string, string>();
        foreach (var (field, value) in values)
        {
            if (!RegionTranslationFields.IsKnown(field))
            {
                throw RegionValidationException.UnknownTranslatableField(field);
            }

            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                throw RegionValidationException.Required(field);
            }

            var maxLength = RegionTranslationFields.MaxLength(field);
            if (trimmed.Length > maxLength)
            {
                throw RegionValidationException.TooLong(field, maxLength);
            }

            normalized[field] = trimmed;
        }

        return normalized;
    }

    private static RegionLevel ParseLevel(string level)
    {
        if (!Enum.TryParse<RegionLevel>(level.Trim(), ignoreCase: true, out var parsed))
        {
            throw RegionValidationException.InvalidLevel();
        }

        return parsed;
    }

    private static string? NormalizeCountryCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
