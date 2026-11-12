// Region 仓储 EF Core 实现（Infrastructure 层）：按 (country, code) 复合业务键寻址。
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Repositories;
using Axiom.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace Axiom.ReferenceData.Infrastructure.EfCore;

public sealed class RegionRepository(ReferenceDataDbContext dbContext) : IRegionRepository
{
    public Task<Region?> GetAsync(string countryCode, string code, CancellationToken ct = default) =>
        dbContext.Regions.FirstOrDefaultAsync(r => r.CountryCode == countryCode && r.Code == code, ct);

    public Task<Region?> GetAsNoTrackingAsync(string countryCode, string code, CancellationToken ct = default) =>
        dbContext.Regions.AsNoTracking().FirstOrDefaultAsync(r => r.CountryCode == countryCode && r.Code == code, ct);

    public async Task<PagedResponse<Region>> ListAsync(
        int page,
        int pageSize,
        int? depth,
        string? countryCode,
        string? parentCode,
        string? keyword,
        CancellationToken ct = default)
    {
        IQueryable<Region> query = dbContext.Regions.AsNoTracking();
        if (depth.HasValue) query = query.Where(r => r.Depth == depth.Value);
        if (countryCode is { } cc) query = query.Where(r => r.CountryCode == cc);
        if (parentCode is { } pc) query = query.Where(r => r.ParentCode == pc);
        if (keyword is { } kw)
        {
            query = query.Where(r => EF.Functions.Like(r.Name, $"%{kw}%")
                                     || (r.AsciiName != null && EF.Functions.Like(r.AsciiName, $"%{kw}%")));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.CountryCode)
            .ThenBy(r => r.Depth)
            .ThenBy(r => r.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<Region>(items, totalCount, page, pageSize);
    }

    public Task<Region?> GetCountryAsync(string countryCode, CancellationToken ct = default) =>
        dbContext.Regions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Depth == 0 && r.CountryCode == countryCode, ct);

    public Task<List<Region>> ListForTreeAsync(string countryCode, int maxDepth, CancellationToken ct = default) =>
        dbContext.Regions.AsNoTracking()
            .Where(r => r.CountryCode == countryCode && r.Depth <= maxDepth)
            .ToListAsync(ct);

    public Task<List<Region>> ListChildrenAsync(string countryCode, string parentCode, CancellationToken ct = default) =>
        dbContext.Regions.AsNoTracking()
            .Where(r => r.CountryCode == countryCode && r.ParentCode == parentCode)
            .OrderBy(r => r.Code)
            .ToListAsync(ct);

    public Task<List<Region>> ListByParentCodesAsync(
        string countryCode,
        IReadOnlyCollection<string> parentCodes,
        CancellationToken ct = default)
    {
        if (parentCodes.Count == 0)
        {
            return Task.FromResult<List<Region>>([]);
        }

        var codes = parentCodes.ToArray();
        return dbContext.Regions.AsNoTracking()
            .Where(r => r.CountryCode == countryCode && r.ParentCode != null && codes.Contains(r.ParentCode))
            .ToListAsync(ct);
    }

    public Task<bool> HasChildrenAsync(string countryCode, string parentCode, CancellationToken ct = default) =>
        dbContext.Regions.AnyAsync(r => r.CountryCode == countryCode && r.ParentCode == parentCode, ct);

    public Task<bool> GeoNameIdExistsAsync(int geoNameId, CancellationToken ct = default) =>
        dbContext.Regions.AnyAsync(r => r.GeoNameId == geoNameId, ct);

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
        string countryCode,
        IReadOnlyCollection<string> codes,
        IReadOnlyCollection<string> cultures,
        CancellationToken ct = default)
    {
        if (codes.Count == 0 || cultures.Count == 0)
        {
            return [];
        }

        var codeTags = codes.ToArray();
        var cultureTags = cultures.ToArray();

        return await dbContext.RegionTranslations
            .AsNoTracking()
            .Where(translation => translation.RegionCountryCode == countryCode
                                  && codeTags.Contains(translation.RegionCode)
                                  && cultureTags.Contains(translation.Culture))
            .ToListAsync(ct);
    }

    public Task<List<RegionTranslation>> GetTranslationsAsync(
        string countryCode,
        string code,
        string culture,
        CancellationToken ct = default) =>
        dbContext.RegionTranslations
            .AsNoTracking()
            .Where(translation => translation.RegionCountryCode == countryCode
                                  && translation.RegionCode == code
                                  && translation.Culture == culture)
            .ToListAsync(ct);

    public async Task ReplaceTranslationsAsync(
        string countryCode,
        string code,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default)
    {
        var existing = await dbContext.RegionTranslations
            .Where(translation => translation.RegionCountryCode == countryCode
                                  && translation.RegionCode == code
                                  && translation.Culture == culture)
            .ToDictionaryAsync(translation => translation.Field, ct);

        foreach (var (field, value) in values)
        {
            if (existing.TryGetValue(field, out var translation))
            {
                translation.ChangeValue(value);
            }
            else
            {
                dbContext.RegionTranslations.Add(new RegionTranslation(countryCode, code, culture, field, value));
            }
        }

        var removed = existing
            .Where(entry => !values.ContainsKey(entry.Key))
            .Select(entry => entry.Value)
            .ToArray();

        dbContext.RegionTranslations.RemoveRange(removed);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteTranslationsAsync(
        string countryCode,
        string code,
        string culture,
        CancellationToken ct = default)
    {
        var existing = await dbContext.RegionTranslations
            .Where(translation => translation.RegionCountryCode == countryCode
                                  && translation.RegionCode == code
                                  && translation.Culture == culture)
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
