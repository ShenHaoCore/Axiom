// 行政区应用服务实现（Application 层）。
// 包含层级树构建、唯一键冲突检查、删除子节点检查等业务规则。
// 读取时按语言回退链合并翻译子表；没有译文则回退到主表默认语言原值。
// 全部按 (country, code) 复合业务键寻址，对齐 geo_division 数据集。
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
        var countryCode = NormalizeCountryCode(query.CountryCode);
        var paged = await repository.ListAsync(
            query.Page,
            Math.Clamp(query.PageSize, 1, 200),
            query.Depth,
            countryCode,
            NormalizeCode(query.ParentCode),
            query.Keyword,
            ct);

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync(
            countryCode ?? string.Empty,
            paged.Items.Select(region => region.Code).ToArray(),
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
        int maxDepth,
        string? culture = null,
        CancellationToken ct = default)
    {
        var cc = NormalizeCountryCode(countryCode)
            ?? throw new RegionCountryNotFoundException(countryCode);

        maxDepth = Math.Clamp(maxDepth, 0, 3);

        var regions = await repository.ListForTreeAsync(cc, maxDepth, ct);
        var country = regions.FirstOrDefault(r => r.Depth == 0 && r.CountryCode == cc);
        if (country is null)
        {
            throw new RegionCountryNotFoundException(cc);
        }

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync(
            cc,
            regions.Select(region => region.Code).ToArray(),
            chain,
            ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        var byParent = regions
            .Where(r => r.ParentCode is not null)
            .GroupBy(r => r.ParentCode!)
            .ToDictionary(g => g.Key, g => g.ToList());

        return BuildTree(country, byParent, lookup);
    }

    public async Task<IReadOnlyList<RegionChildDto>> GetChildrenAsync(
        string countryCode,
        string code,
        string? culture = null,
        CancellationToken ct = default)
    {
        var cc = NormalizeCountryCode(countryCode)
            ?? throw new RegionCountryNotFoundException(countryCode);
        var parent = await repository.GetAsNoTrackingAsync(cc, NormalizeCode(code))
            ?? throw new RegionNotFoundException(cc, NormalizeCode(code));

        var children = await repository.ListChildrenAsync(cc, parent.Code);
        var grandchildren = await repository.ListByParentCodesAsync(
            cc, children.Select(child => child.Code).ToArray(), ct);
        var byParent = grandchildren
            .GroupBy(grandchild => grandchild.ParentCode!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync(
            cc,
            children.Select(child => child.Code).ToArray(),
            chain,
            ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        return children
            .Select(child =>
            {
                byParent.TryGetValue(child.Code, out var childChildren);
                // 同名检测使用默认语言名称：直辖市在数据源中就是省、市两条同名记录。
                var sameNameChildCount = childChildren?.Count(grandchild =>
                    string.Equals(grandchild.Name?.Trim(), child.Name?.Trim(), StringComparison.Ordinal)) ?? 0;

                return new RegionChildDto(
                    child.CountryCode,
                    child.Code,
                    child.ParentCode,
                    child.Depth,
                    child.Type,
                    lookup.Find(RegionMapper.TranslationKey(child), RegionTranslationFields.Name) ?? child.Name,
                    child.Population,
                    childChildren?.Count ?? 0,
                    sameNameChildCount);
            })
            .ToList();
    }

    public async Task<RegionDto?> GetAsync(
        string countryCode,
        string code,
        string? culture = null,
        CancellationToken ct = default)
    {
        var cc = NormalizeCountryCode(countryCode);
        var normalizedCode = NormalizeCode(code);
        if (cc is null || normalizedCode is null)
        {
            return null;
        }

        var entity = await repository.GetAsNoTrackingAsync(cc, normalizedCode, ct);
        if (entity is null)
        {
            return null;
        }

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync(cc, [normalizedCode], chain, ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        return RegionMapper.ToDto(entity, lookup);
    }

    public async Task<(RegionDto Dto, Uri LocationUri)> CreateAsync(
        CreateRegionInput input,
        CancellationToken ct = default)
    {
        var countryCode = NormalizeCountryCode(input.CountryCode)
            ?? throw RegionValidationException.InvalidCountryCode();

        // 上级必须存在（depth 0 为国家行，无上级）。
        if (input.Depth > 0)
        {
            var parentCode = NormalizeCode(input.ParentCode ?? string.Empty);
            if (parentCode.Length == 0)
            {
                throw RegionValidationException.Required("ParentCode");
            }

            if (await repository.GetAsNoTrackingAsync(countryCode, parentCode, ct) is null)
            {
                throw new RegionParentNotFoundException(countryCode, parentCode);
            }
        }

        var entity = Region.Create(
            depth: input.Depth,
            countryCode: countryCode,
            code: input.Code,
            name: input.Name,
            parentCode: input.ParentCode,
            type: input.Type,
            codeIso: input.CodeIso,
            sortKey: input.SortKey,
            abbrev: input.Abbrev,
            asciiName: input.AsciiName,
            localName: input.LocalName,
            latitude: input.Latitude,
            longitude: input.Longitude,
            population: input.Population,
            status: input.Status);

        if (await repository.GetAsync(countryCode, entity.Code, ct) is not null)
        {
            throw new RegionUniqueKeyConflictException(countryCode, entity.Code);
        }

        await repository.AddAsync(entity, ct);

        var dto = RegionMapper.ToDto(entity);
        var location = new Uri($"/api/regions/{countryCode}/{entity.Code}", UriKind.Relative);
        return (dto, location);
    }

    public async Task<RegionDto> UpdateAsync(
        string countryCode,
        string code,
        UpdateRegionInput input,
        CancellationToken ct = default)
    {
        var cc = NormalizeCountryCode(countryCode)
            ?? throw RegionValidationException.InvalidCountryCode();
        var entity = await repository.GetAsync(cc, NormalizeCode(code), ct)
            ?? throw new RegionNotFoundException(cc, NormalizeCode(code));

        entity.Update(
            input.Name,
            input.AsciiName,
            input.LocalName,
            input.Latitude,
            input.Longitude,
            input.Population,
            input.CodeIso,
            input.SortKey,
            input.Abbrev,
            input.Status);

        await repository.UpdateAsync(entity, ct);
        return RegionMapper.ToDto(entity);
    }

    public async Task DeleteAsync(string countryCode, string code, CancellationToken ct = default)
    {
        var cc = NormalizeCountryCode(countryCode)
            ?? throw RegionValidationException.InvalidCountryCode();
        var entity = await repository.GetAsync(cc, NormalizeCode(code), ct)
            ?? throw new RegionNotFoundException(cc, NormalizeCode(code));

        if (await repository.HasChildrenAsync(cc, entity.Code, ct))
        {
            throw new RegionHasChildrenException(cc, entity.Code);
        }

        await repository.RemoveAsync(entity, ct);
    }

    public async Task<TranslationDto?> GetTranslationsAsync(
        string countryCode,
        string code,
        string culture,
        CancellationToken ct = default)
    {
        var cc = NormalizeCountryCode(countryCode);
        var normalizedCode = NormalizeCode(code);
        if (cc is null || normalizedCode is null
            || await repository.GetAsNoTrackingAsync(cc, normalizedCode, ct) is null)
        {
            return null;
        }

        var rows = await repository.GetTranslationsAsync(cc, normalizedCode, culture, ct);
        return new TranslationDto(culture, rows.ToDictionary(row => row.Field, row => row.Value));
    }

    public async Task<TranslationDto?> SaveTranslationsAsync(
        string countryCode,
        string code,
        string culture,
        SaveTranslationsInput input,
        CancellationToken ct = default)
    {
        var cc = NormalizeCountryCode(countryCode);
        var normalizedCode = NormalizeCode(code);
        if (cc is null || normalizedCode is null
            || await repository.GetAsNoTrackingAsync(cc, normalizedCode, ct) is null)
        {
            return null;
        }

        var values = ValidateTranslationValues(input.Values);
        await repository.ReplaceTranslationsAsync(cc, normalizedCode, culture, values, ct);
        return new TranslationDto(culture, values);
    }

    public async Task DeleteTranslationsAsync(
        string countryCode,
        string code,
        string culture,
        CancellationToken ct = default)
    {
        var cc = NormalizeCountryCode(countryCode)
            ?? throw RegionValidationException.InvalidCountryCode();
        var normalizedCode = NormalizeCode(code)
            ?? throw RegionValidationException.Required("Code");

        if (await repository.GetAsNoTrackingAsync(cc, normalizedCode, ct) is null)
        {
            throw new RegionNotFoundException(cc, normalizedCode);
        }

        await repository.DeleteTranslationsAsync(cc, normalizedCode, culture, ct);
    }

    // ---------- 私有辅助方法 ----------

    private RegionTreeDto BuildTree(
        Region node,
        Dictionary<string, List<Region>> byParent,
        TranslationLookup lookup)
    {
        if (!byParent.TryGetValue(node.Code, out var children))
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
        new($"{row.RegionCountryCode}.{row.RegionCode}", row.Culture, row.Field, row.Value);

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

    private static string? NormalizeCountryCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
