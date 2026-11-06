// 新闻文章仓储 EF Core 实现（Infrastructure 层）。
// 实现 INewsArticleRepository 接口，内部处理并发令牌设置和 DbUpdateConcurrencyException → 领域异常转换。
using Axiom.News.Domain.Entities;
using Axiom.News.Domain.Exceptions;
using Axiom.News.Domain.Repositories;
using Axiom.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace Axiom.News.Infrastructure.EfCore;

public sealed class NewsArticleRepository(NewsDbContext dbContext) : INewsArticleRepository
{
    public async Task<NewsArticle?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.NewsArticles.SingleOrDefaultAsync(article => article.Id == id, ct);

    public async Task<NewsArticle?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.NewsArticles.AsNoTracking().SingleOrDefaultAsync(article => article.Id == id, ct);

    public async Task<PagedResponse<NewsArticle>> ListAsync(
        int page,
        int pageSize,
        string? category,
        NewsArticleStatus? status,
        string? keyword,
        CancellationToken ct = default)
    {
        var query = dbContext.NewsArticles.AsNoTracking();

        if (category is { } c) query = query.Where(a => a.Category == c);
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);
        if (keyword is { } kw)
        {
            query = query.Where(a =>
                a.Title.Contains(kw) ||
                (a.Summary != null && a.Summary.Contains(kw)) ||
                a.Content.Contains(kw));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<NewsArticle>(items, totalCount, page, pageSize);
    }

    public async Task AddAsync(NewsArticle article, CancellationToken ct = default)
    {
        dbContext.NewsArticles.Add(article);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateWithETagAsync(NewsArticle article, Guid expectedVersion, CancellationToken ct = default)
    {
        // 设置 EF Core 的并发原始值。SaveChangesAsync 时 EF Core 会比较数据库中的实际值和 OriginalValue。
        dbContext.Entry(article).Property(a => a.ConcurrencyToken).OriginalValue = expectedVersion;

        try
        {
            dbContext.NewsArticles.Update(article);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new NewsArticleConcurrencyConflictException(article.Id);
        }
    }

    public async Task RemoveWithETagAsync(NewsArticle article, Guid expectedVersion, CancellationToken ct = default)
    {
        dbContext.Entry(article).Property(a => a.ConcurrencyToken).OriginalValue = expectedVersion;

        try
        {
            dbContext.NewsArticles.Remove(article);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new NewsArticleConcurrencyConflictException(article.Id);
        }
    }
}
