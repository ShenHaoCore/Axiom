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

        return BuildTree(country, byParent, lookup, new HashSet<Guid>());
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

    public async Task<(GeoDivisionDto? Dto, Guid? ETag)> GetAsync(
        Guid id,
        string? culture = null,
        CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsNoTrackingAsync(id, ct);
        if (entity is null)
        {
            return (null, null);
        }

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync([id], chain, ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        return (GeoDivisionMapper.ToDto(entity, lookup), entity.ConcurrencyToken);
    }

    public async Task<(GeoDivisionDto Dto, Uri LocationUri, Guid ETag)> CreateAsync(
        CreateGeoDivisionInput input,
        CancellationToken ct = default)
    {
        // Level 1（国家行）未单独给国家代码时，以区划代码兼任国家代码。
        var countryCode = NormalizeCountryCode(input.CountryCode)
            ?? (input.Level == 1 ? NormalizeCountryCode(input.Code) : null);

        // 一级区划代码口径：国家行没有；二级行取输入；三级及以下必须与所属一级行政区
        // （Level 2 祖先）一致，留空时自动带出。需先于 Kind 推导完成（推导要使用该值）。
        var subdivisionCode = await ResolveSubdivisionCodeAsync(input, countryCode, ct);

        // Kind 留空时按国家与深度推导默认类型；非空则按字典强校验。
        var kind = string.IsNullOrWhiteSpace(input.Kind)
            ? GeoDivisionKinds.DeriveDefault(countryCode ?? string.Empty, input.Level, subdivisionCode)
            : GeoDivisionKinds.Normalize(input.Kind);
        await EnsureKindExistsAsync(kind, countryCode, ct);

        var entity = GeoDivision.Create(
            input.Level,
            input.CountryCode,
            input.Name,
            subdivisionCode: subdivisionCode,
            code: input.Code,
            asciiName: input.AsciiName,
            localName: input.LocalName,
            latitude: input.Latitude,
            longitude: input.Longitude,
            population: input.Population,
            parentId: input.ParentId,
            kind: kind);

        if (await repository.AnyByUniqueKeyAsync(entity.Level, entity.CountryCode, entity.Code, ct: ct))
        {
            throw new GeoDivisionUniqueKeyConflictException(entity.CountryCode, entity.Level, entity.Code);
        }

        await repository.AddAsync(entity, ct);

        var dto = GeoDivisionMapper.ToDto(entity);
        var location = new Uri($"/api/geo-divisions/{entity.Id}", UriKind.Relative);
        return (dto, location, entity.ConcurrencyToken);
    }

    /// <summary>
    /// 解析新建行政区的一级区划代码：顺带校验父级链（存在、层级相邻、同国家），
    /// 国家行返回 null，二级行取输入（ISO 3166-2 后缀，无法由父级推导），
    /// 三级及以下取所属一级行政区（Level 2 祖先）的代码，输入值与之不符时报错。
    /// </summary>
    private async Task<string?> ResolveSubdivisionCodeAsync(
        CreateGeoDivisionInput input,
        string? countryCode,
        CancellationToken ct)
    {
        if (input.Level == 1)
        {
            return null;
        }

        if (input.ParentId is not { } parentId)
        {
            throw GeoDivisionValidationException.ParentRequired();
        }

        if (countryCode is null)
        {
            // 国家代码非法时跳过链校验，交由实体的国家代码校验统一报错。
            return input.SubdivisionCode;
        }

        GeoDivision? firstLevel = null; // 所属一级行政区（Level 2）。
        var currentId = parentId;
        for (var expectedLevel = input.Level - 1; expectedLevel >= 1; expectedLevel--)
        {
            var node = await repository.GetByIdAsNoTrackingAsync(currentId, ct)
                ?? throw GeoDivisionValidationException.ParentNotFound(currentId);

            if (node.Level != expectedLevel
                || !string.Equals(node.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase))
            {
                throw GeoDivisionValidationException.ParentInvalid();
            }

            if (node.Level == 2)
            {
                firstLevel = node;
                break;
            }

            if (node.Level == 1)
            {
                // 链到国家行为止：一级行政区自身的区划码保持输入值（仅二级行走此路径）。
                break;
            }

            // 链上的中间节点必须有父级，否则层级链中断。
            currentId = node.ParentId ?? throw GeoDivisionValidationException.ParentInvalid();
        }

        if (input.Level == 2)
        {
            return input.SubdivisionCode;
        }

        if (string.IsNullOrWhiteSpace(input.SubdivisionCode))
        {
            return firstLevel?.SubdivisionCode;
        }

        return string.Equals(
                   input.SubdivisionCode.Trim(),
                   firstLevel?.SubdivisionCode,
                   StringComparison.OrdinalIgnoreCase)
            ? firstLevel?.SubdivisionCode
            : throw GeoDivisionValidationException.SubdivisionMismatch(
                firstLevel?.SubdivisionCode, input.SubdivisionCode.Trim());
    }

    public async Task<(GeoDivisionDto Dto, Guid ETag)> UpdateAsync(
        Guid id,
        UpdateGeoDivisionInput input,
        Guid expectedToken,
        CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new GeoDivisionNotFoundException(id);

        if (input.ParentId.HasValue)
        {
            await EnsureParentOnUpdateAsync(entity, input.ParentId.Value, ct);
        }

        if (input.Kind is not null)
        {
            await EnsureKindExistsAsync(GeoDivisionKinds.Normalize(input.Kind), entity.CountryCode, ct);
        }

        // 改码时先做唯一键业务检查（排除自身），并发换码竞态由数据库唯一索引兜底（仓储层映射 409）。
        if (!string.IsNullOrWhiteSpace(input.Code))
        {
            var normalizedCode = input.Code.Trim().ToUpperInvariant();
            if (await repository.AnyByUniqueKeyAsync(entity.Level, entity.CountryCode, normalizedCode, entity.Id, ct))
            {
                throw new GeoDivisionUniqueKeyConflictException(entity.CountryCode, entity.Level, normalizedCode);
            }
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

        await repository.UpdateWithETagAsync(entity, expectedToken, ct);
        return (GeoDivisionMapper.ToDto(entity), entity.ConcurrencyToken);
    }

    public async Task DeleteAsync(Guid id, Guid expectedToken, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new GeoDivisionNotFoundException(id);

        if (await repository.HasChildrenAsync(id, ct))
        {
            throw new GeoDivisionHasChildrenException(id);
        }

        await repository.RemoveWithETagAsync(entity, expectedToken, ct);
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

    private static GeoDivisionTreeDto BuildTree(
        GeoDivision node,
        Dictionary<Guid, List<GeoDivision>> byParent,
        TranslationLookup lookup,
        HashSet<Guid> visited)
    {
        // 防御性防环：即使历史数据里出现父子环，也把已访问节点按叶子返回，避免无限递归。
        if (!visited.Add(node.Id) || !byParent.TryGetValue(node.Id, out var children))
        {
            return GeoDivisionMapper.ToTreeNode(node, children: [], lookup);
        }

        var childNodes = children
            .OrderBy(r => r.Code)
            .Select(child => BuildTree(child, byParent, lookup, visited))
            .ToList();

        return GeoDivisionMapper.ToTreeNode(node, childNodes, lookup);
    }

    /// <summary>
    /// 更新路径的父级校验：直接父级必须层级相邻且同国家；沿祖先链上溯到国家行，
    /// 链上出现自身即为父子环（覆盖直接自指与深层互指）。
    /// </summary>
    private async Task EnsureParentOnUpdateAsync(GeoDivision entity, Guid parentId, CancellationToken ct)
    {
        if (entity.Level == GeoDivision.RootLevel)
        {
            throw GeoDivisionValidationException.ParentInvalid();
        }

        var expectedLevel = entity.Level - 1;
        var currentId = parentId;
        while (expectedLevel >= GeoDivision.RootLevel)
        {
            var node = await repository.GetByIdAsNoTrackingAsync(currentId, ct)
                ?? throw GeoDivisionValidationException.ParentNotFound(parentId);

            if (node.Id == entity.Id)
            {
                throw GeoDivisionValidationException.CyclicParent();
            }

            if (!string.Equals(node.CountryCode, entity.CountryCode, StringComparison.OrdinalIgnoreCase)
                || node.Level != expectedLevel)
            {
                throw GeoDivisionValidationException.ParentInvalid();
            }

            if (node.Level == GeoDivision.RootLevel)
            {
                return; // 已达国家行，链完整。
            }

            // 链上的中间节点必须有父级，否则层级链中断。
            currentId = node.ParentId ?? throw GeoDivisionValidationException.ParentInvalid();
            expectedLevel--;
        }
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
