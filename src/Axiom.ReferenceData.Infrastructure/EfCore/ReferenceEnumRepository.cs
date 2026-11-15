// ReferenceEnum 仓储 EF Core 实现（Infrastructure 层）。
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Repositories;
using Axiom.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace Axiom.ReferenceData.Infrastructure.EfCore;

public sealed class ReferenceEnumRepository(ReferenceDataDbContext dbContext) : IReferenceEnumRepository
{
    public async Task<ReferenceEnum?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.ReferenceEnums.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<ReferenceEnum?> GetByCategoryAndCodeAsync(
        string category,
        string code,
        CancellationToken ct = default) =>
        await dbContext.ReferenceEnums.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Category == category && e.Code == code, ct);

    public async Task<PagedResponse<ReferenceEnum>> ListAsync(
        string? category,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        IQueryable<ReferenceEnum> query = dbContext.ReferenceEnums.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(e => e.Category == category);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(e => e.Category)
            .ThenBy(e => e.SortOrder)
            .ThenBy(e => e.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<ReferenceEnum>(items, totalCount, page, pageSize);
    }

    public async Task<bool> ExistsAsync(string category, string code, CancellationToken ct = default) =>
        await dbContext.ReferenceEnums.AsNoTracking()
            .AnyAsync(e => e.Category == category && e.Code == code, ct);

    public async Task AddAsync(ReferenceEnum entity, CancellationToken ct = default)
    {
        dbContext.ReferenceEnums.Add(entity);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ReferenceEnum entity, CancellationToken ct = default) =>
        await dbContext.SaveChangesAsync(ct);

    public async Task RemoveAsync(ReferenceEnum entity, CancellationToken ct = default)
    {
        dbContext.ReferenceEnums.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<List<ReferenceEnumTranslation>> ListTranslationsAsync(
        IReadOnlyCollection<Guid> enumIds,
        IReadOnlyCollection<string> cultures,
        CancellationToken ct = default)
    {
        if (enumIds.Count == 0 || cultures.Count == 0)
        {
            return [];
        }

        var ids = enumIds.ToArray();
        var cultureTags = cultures.ToArray();

        return await dbContext.ReferenceEnumTranslations
            .AsNoTracking()
            .Where(translation => ids.Contains(translation.ReferenceEnumId) && cultureTags.Contains(translation.Culture))
            .ToListAsync(ct);
    }

    public async Task<List<ReferenceEnumTranslation>> GetTranslationsAsync(
        Guid enumId,
        string culture,
        CancellationToken ct = default) =>
        await dbContext.ReferenceEnumTranslations
            .AsNoTracking()
            .Where(translation => translation.ReferenceEnumId == enumId && translation.Culture == culture)
            .ToListAsync(ct);

    public async Task ReplaceTranslationsAsync(
        Guid enumId,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default)
    {
        var existing = await dbContext.ReferenceEnumTranslations
            .FirstOrDefaultAsync(
                translation => translation.ReferenceEnumId == enumId && translation.Culture == culture,
                ct);

        if (!values.TryGetValue(ReferenceEnumTranslationFields.Name, out var name)
            || string.IsNullOrWhiteSpace(name))
        {
            if (existing is not null)
            {
                dbContext.ReferenceEnumTranslations.Remove(existing);
                await dbContext.SaveChangesAsync(ct);
            }

            return;
        }

        if (existing is null)
        {
            dbContext.ReferenceEnumTranslations.Add(new ReferenceEnumTranslation(enumId, culture, name));
        }
        else
        {
            existing.ChangeName(name);
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteTranslationsAsync(Guid enumId, string culture, CancellationToken ct = default)
    {
        var existing = await dbContext.ReferenceEnumTranslations
            .Where(translation => translation.ReferenceEnumId == enumId && translation.Culture == culture)
            .ToListAsync(ct);

        if (existing.Count == 0)
        {
            return false;
        }

        dbContext.ReferenceEnumTranslations.RemoveRange(existing);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }
}
