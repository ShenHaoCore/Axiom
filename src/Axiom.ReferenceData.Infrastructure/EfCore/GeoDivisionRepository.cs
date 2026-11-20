// GeoDivision 仓储 EF Core 实现（Infrastructure 层）。
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Exceptions;
using Axiom.ReferenceData.Domain.Repositories;
using Axiom.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace Axiom.ReferenceData.Infrastructure.EfCore;

public sealed class GeoDivisionRepository(ReferenceDataDbContext dbContext) : IGeoDivisionRepository
{
    public async Task<GeoDivision?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.GeoDivisions.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<GeoDivision?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.GeoDivisions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<PagedResponse<GeoDivision>> ListAsync(
        int page,
        int pageSize,
        int? level,
        string? countryCode,
        string? subdivisionCode,
        string? keyword,
        CancellationToken ct = default)
    {
        IQueryable<GeoDivision> query = dbContext.GeoDivisions.AsNoTracking();
        if (level.HasValue) query = query.Where(r => (int)r.Level == level.Value);
        if (countryCode is { } cc) query = query.Where(r => r.CountryCode == cc);
        if (subdivisionCode is { } code) query = query.Where(r => r.SubdivisionCode == code);
        if (keyword is { } kw)
        {
            // 双侧 Lower + LIKE：大小写不敏感（等价 ILIKE），InMemory 测试同样可翻译；
            // 通配符转义后用户输入不再被解释为模式。
            var pattern = $"%{LikePattern.Escape(kw.Trim().ToLowerInvariant())}%";
            query = query.Where(r => EF.Functions.Like(r.Name.ToLowerInvariant(), pattern)
                                     || (r.AsciiName != null && EF.Functions.Like(r.AsciiName.ToLowerInvariant(), pattern)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.CountryCode)
            .ThenBy(r => (int)r.Level)
            .ThenBy(r => r.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<GeoDivision>(items, totalCount, page, pageSize);
    }

    public async Task<GeoDivision?> GetCountryByCodeAsync(string countryCode, CancellationToken ct = default) =>
        await dbContext.GeoDivisions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Level == GeoDivision.RootLevel && r.CountryCode == countryCode, ct);

    public async Task<List<GeoDivision>> ListForTreeAsync(string countryCode, int depth, CancellationToken ct = default) =>
        await dbContext.GeoDivisions.AsNoTracking()
            .Where(r => r.CountryCode == countryCode && r.Level <= depth)
            .ToListAsync(ct);

    public async Task<List<GeoDivision>> ListByParentAsync(Guid parentId, CancellationToken ct = default) =>
        await dbContext.GeoDivisions.AsNoTracking()
            .Where(r => r.ParentId == parentId)
            .OrderBy(r => r.Code)
            .ToListAsync(ct);

    public async Task<List<GeoDivision>> ListByParentIdsAsync(
        IReadOnlyCollection<Guid> parentIds,
        CancellationToken ct = default)
    {
        if (parentIds.Count == 0)
        {
            return [];
        }

        return await dbContext.GeoDivisions.AsNoTracking()
            .Where(r => r.ParentId != null && parentIds.Contains(r.ParentId.Value))
            .ToListAsync(ct);
    }

    public async Task<bool> AnyByUniqueKeyAsync(
        int level,
        string countryCode,
        string code,
        Guid? excludedId = null,
        CancellationToken ct = default) =>
        await dbContext.GeoDivisions.AnyAsync(
            r => r.CountryCode == countryCode && r.Level == level && r.Code == code && r.Id != excludedId, ct);

    public async Task<bool> AnyByKindAsync(string kind, CancellationToken ct = default) =>
        await dbContext.GeoDivisions.AnyAsync(r => r.Kind == kind, ct);

    public async Task<bool> HasChildrenAsync(Guid parentId, CancellationToken ct = default) =>
        await dbContext.GeoDivisions.AnyAsync(r => r.ParentId == parentId, ct);

    public async Task AddAsync(GeoDivision region, CancellationToken ct = default)
    {
        dbContext.GeoDivisions.Add(region);
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (PostgresExceptions.IsUniqueViolation(exception))
        {
            // 前查与提交之间的并发插入竞态：以数据库唯一索引为准，映射为业务冲突。
            throw new GeoDivisionUniqueKeyConflictException(region.CountryCode, region.Level, region.Code);
        }
    }

    public async Task UpdateWithETagAsync(GeoDivision region, Guid expectedToken, CancellationToken ct = default)
    {
        dbContext.Entry(region).Property(r => r.ConcurrencyToken).OriginalValue = expectedToken;

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GeoDivisionConcurrencyConflictException(region.Id);
        }
        catch (DbUpdateException exception) when (PostgresExceptions.IsUniqueViolation(exception))
        {
            // 并发换码竞态：唯一索引兜底，映射为业务冲突而非 500。
            throw new GeoDivisionUniqueKeyConflictException(region.CountryCode, region.Level, region.Code);
        }
    }

    public async Task RemoveWithETagAsync(GeoDivision region, Guid expectedToken, CancellationToken ct = default)
    {
        dbContext.Entry(region).Property(r => r.ConcurrencyToken).OriginalValue = expectedToken;

        try
        {
            dbContext.GeoDivisions.Remove(region);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GeoDivisionConcurrencyConflictException(region.Id);
        }
    }

    public async Task<List<GeoDivisionTranslation>> ListTranslationsAsync(
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

        return await dbContext.GeoDivisionTranslations
            .AsNoTracking()
            .Where(translation => ids.Contains(translation.GeoDivisionId) && cultureTags.Contains(translation.Culture))
            .ToListAsync(ct);
    }

    public async Task<List<GeoDivisionTranslation>> GetTranslationsAsync(
        Guid regionId,
        string culture,
        CancellationToken ct = default) =>
        await dbContext.GeoDivisionTranslations
            .AsNoTracking()
            .Where(translation => translation.GeoDivisionId == regionId && translation.Culture == culture)
            .ToListAsync(ct);

    public async Task ReplaceTranslationsAsync(
        Guid regionId,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default)
    {
        var existing = await dbContext.GeoDivisionTranslations
            .FirstOrDefaultAsync(
                translation => translation.GeoDivisionId == regionId && translation.Culture == culture,
                ct);

        var changed = false;
        if (!values.TryGetValue(GeoDivisionTranslationFields.Name, out var name)
            || string.IsNullOrWhiteSpace(name))
        {
            if (existing is not null)
            {
                dbContext.GeoDivisionTranslations.Remove(existing);
                changed = true;
            }
        }
        else if (existing is null)
        {
            dbContext.GeoDivisionTranslations.Add(new GeoDivisionTranslation(regionId, culture, name));
            changed = true;
        }
        else
        {
            existing.ChangeName(name);
            changed = true;
        }

        if (changed)
        {
            // 译文属于资源表示的一部分：刷新主表并发令牌，保证 If-Match 覆盖“主表+译文”表示。
            // 行政区不发地点事件，无 Outbox 写入。
            BumpConcurrencyToken(regionId);
            await dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> DeleteTranslationsAsync(Guid regionId, string culture, CancellationToken ct = default)
    {
        var existing = await dbContext.GeoDivisionTranslations
            .Where(translation => translation.GeoDivisionId == regionId && translation.Culture == culture)
            .ToListAsync(ct);

        if (existing.Count == 0)
        {
            return false;
        }

        dbContext.GeoDivisionTranslations.RemoveRange(existing);
        BumpConcurrencyToken(regionId);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    // 译文变更时同步刷新主表并发令牌。
    private void BumpConcurrencyToken(Guid regionId)
    {
        var region = dbContext.GeoDivisions.FirstOrDefault(r => r.Id == regionId);
        if (region is not null)
        {
            dbContext.Entry(region).Property(r => r.ConcurrencyToken).CurrentValue = Guid.NewGuid();
        }
    }
}
