// 行政区应用服务实现（Application 层）。
// 包含层级树构建、唯一键冲突检查、删除子节点检查等业务规则。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Application.ObjectMapping;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Exceptions;
using Axiom.ReferenceData.Domain.Repositories;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Application.Services;

public sealed class RegionAppService(IRegionRepository repository) : IRegionAppService
{
    public async Task<PagedResponse<RegionDto>> ListAsync(
        RegionListQuery query,
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

        return new PagedResponse<RegionDto>(
            paged.Items.Select(RegionMapper.ToDto).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize);
    }

    public async Task<RegionTreeDto?> GetTreeAsync(
        string countryCode,
        int depth,
        CancellationToken ct = default)
    {
        var cc = NormalizeCountryCode(countryCode);
        depth = Math.Clamp(depth, 1, 3);

        var regions = await repository.ListForTreeAsync(cc, depth, ct);
        var country = regions.FirstOrDefault(r => r.Level == RegionLevel.Country && r.CountryCode == cc);
        if (country is null)
        {
            throw new RegionCountryNotFoundException(cc);
        }

        var byParent = regions
            .Where(r => r.ParentId.HasValue)
            .GroupBy(r => r.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        return BuildTree(country, byParent);
    }

    public async Task<RegionDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsNoTrackingAsync(id, ct);
        return entity is null ? null : RegionMapper.ToDto(entity);
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

    // ---------- 私有辅助方法 ----------

    private RegionTreeDto BuildTree(Region node, Dictionary<Guid, List<Region>> byParent)
    {
        if (!byParent.TryGetValue(node.Id, out var children))
        {
            return RegionMapper.ToTreeNode(node, children: []);
        }

        var childNodes = children
            .OrderBy(r => r.Code)
            .Select(child => BuildTree(child, byParent))
            .ToList();

        return RegionMapper.ToTreeNode(node, childNodes);
    }

    private static RegionLevel ParseLevel(string level)
    {
        if (!Enum.TryParse<RegionLevel>(level.Trim(), ignoreCase: true, out var parsed))
            throw new RegionValidationException(nameof(level), "level 必须是 Country/Admin1/City。");
        return parsed;
    }

    private static string? NormalizeCountryCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
