// Region 仓储 EF Core 实现（Infrastructure 层）。
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Repositories;
using Axiom.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace Axiom.ReferenceData.Infrastructure.EfCore;

public sealed class RegionRepository(ReferenceDataDbContext dbContext) : IRegionRepository
{
    public async Task<Region?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Regions.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Region?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Regions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<PagedResponse<Region>> ListAsync(
        int page,
        int pageSize,
        int? level,
        string? countryCode,
        string? admin1Code,
        string? keyword,
        CancellationToken ct = default)
    {
        IQueryable<Region> query = dbContext.Regions.AsNoTracking();
        if (level.HasValue) query = query.Where(r => (int)r.Level == level.Value);
        if (countryCode is { } cc) query = query.Where(r => r.CountryCode == cc);
        if (admin1Code is { } a1) query = query.Where(r => r.Admin1Code == a1);
        if (keyword is { } kw)
        {
            query = query.Where(r => EF.Functions.Like(r.Name, $"%{kw}%")
                                     || (r.AsciiName != null && EF.Functions.Like(r.AsciiName, $"%{kw}%")));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.CountryCode)
            .ThenBy(r => (int)r.Level)
            .ThenBy(r => r.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<Region>(items, totalCount, page, pageSize);
    }

    public async Task<Region?> GetCountryByCodeAsync(string countryCode, CancellationToken ct = default) =>
        await dbContext.Regions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Level == RegionLevel.Country && r.CountryCode == countryCode, ct);

    public async Task<List<Region>> ListForTreeAsync(string countryCode, int depth, CancellationToken ct = default) =>
        await dbContext.Regions.AsNoTracking()
            .Where(r => r.CountryCode == countryCode && (int)r.Level <= depth)
            .ToListAsync(ct);

    public async Task<bool> AnyByUniqueKeyAsync(RegionLevel level, string countryCode, string code, CancellationToken ct = default) =>
        await dbContext.Regions.AnyAsync(r => r.CountryCode == countryCode && r.Level == level && r.Code == code, ct);

    public async Task<bool> HasChildrenAsync(Guid parentId, CancellationToken ct = default) =>
        await dbContext.Regions.AnyAsync(r => r.ParentId == parentId, ct);

    public async Task AddAsync(Region region, CancellationToken ct = default)
    {
        dbContext.Regions.Add(region);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Region region, CancellationToken ct = default)
    {
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Region region, CancellationToken ct = default)
    {
        dbContext.Regions.Remove(region);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<List<RegionTranslation>> ListTranslationsAsync(
        IReadOnlyCollection<Guid> regionIds,
        IReadOnlyCollection<string> cultures,
        CancellationToken ct = default)
    {
        if (regionIds.Count == 0 || cultures.Count == 0)
        {
            return [];
        }

        var ids = regionIds.ToArray();
        var cultureTags = cultures.ToArray();

        return await dbContext.RegionTranslations
            .AsNoTracking()
            .Where(translation => ids.Contains(translation.RegionId) && cultureTags.Contains(translation.Culture))
            .ToListAsync(ct);
    }

    public async Task<List<RegionTranslation>> GetTranslationsAsync(
        Guid regionId,
        string culture,
        CancellationToken ct = default) =>
        await dbContext.RegionTranslations
            .AsNoTracking()
            .Where(translation => translation.RegionId == regionId && translation.Culture == culture)
            .ToListAsync(ct);

    public async Task ReplaceTranslationsAsync(
        Guid regionId,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default)
    {
        var existing = await dbContext.RegionTranslations
            .Where(translation => translation.RegionId == regionId && translation.Culture == culture)
            .ToDictionaryAsync(translation => translation.Field, ct);

        foreach (var (field, value) in values)
        {
            if (existing.TryGetValue(field, out var translation))
            {
                translation.ChangeValue(value);
            }
            else
            {
                dbContext.RegionTranslations.Add(new RegionTranslation(regionId, culture, field, value));
            }
        }

        var removed = existing
            .Where(entry => !values.ContainsKey(entry.Key))
            .Select(entry => entry.Value)
            .ToArray();

        dbContext.RegionTranslations.RemoveRange(removed);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteTranslationsAsync(Guid regionId, string culture, CancellationToken ct = default)
    {
        var existing = await dbContext.RegionTranslations
            .Where(translation => translation.RegionId == regionId && translation.Culture == culture)
            .ToListAsync(ct);

        if (existing.Count == 0)
        {
            return false;
        }

        dbContext.RegionTranslations.RemoveRange(existing);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }
}
