// ReferenceEnum 仓储 EF Core 实现（Infrastructure 层）。
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
}
