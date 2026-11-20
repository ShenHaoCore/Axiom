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
            // 双侧 Lower + LIKE：大小写不敏感（等价 ILIKE），InMemory 测试同样可翻译。
            // 搜索限定在 Title/Summary（短文本列）；Content 是 50k 大字段，避免逐行全扫。
            // 通配符转义后用户输入不再被解释为模式。
            var pattern = $"%{LikePattern.Escape(kw.Trim().ToLowerInvariant())}%";
            query = query.Where(a =>
                EF.Functions.Like(a.Title.ToLowerInvariant(), pattern) ||
                (a.Summary != null && EF.Functions.Like(a.Summary.ToLowerInvariant(), pattern)));
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

    public async Task<List<NewsArticleTranslation>> ListTranslationsAsync(
        IReadOnlyCollection<Guid> articleIds,
        IReadOnlyCollection<string> cultures,
        CancellationToken ct = default)
    {
        if (articleIds.Count == 0 || cultures.Count == 0)
        {
            return [];
        }

        var ids = articleIds.ToArray();
        var cultureTags = cultures.ToArray();

        return await dbContext.NewsArticleTranslations
            .AsNoTracking()
            .Where(translation => ids.Contains(translation.NewsArticleId) && cultureTags.Contains(translation.Culture))
            .ToListAsync(ct);
    }

    public async Task<List<NewsArticleTranslation>> GetTranslationsAsync(
        Guid articleId,
        string culture,
        CancellationToken ct = default) =>
        await dbContext.NewsArticleTranslations
            .AsNoTracking()
            .Where(translation => translation.NewsArticleId == articleId && translation.Culture == culture)
            .ToListAsync(ct);

    public async Task ReplaceTranslationsAsync(
        Guid articleId,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default)
    {
        var existing = await dbContext.NewsArticleTranslations
            .Where(translation => translation.NewsArticleId == articleId && translation.Culture == culture)
            .ToDictionaryAsync(translation => translation.Field, ct);

        foreach (var (field, value) in values)
        {
            if (existing.TryGetValue(field, out var translation))
            {
                translation.ChangeValue(value);
            }
            else
            {
                dbContext.NewsArticleTranslations.Add(new NewsArticleTranslation(articleId, culture, field, value));
            }
        }

        var removed = existing
            .Where(entry => !values.ContainsKey(entry.Key))
            .Select(entry => entry.Value)
            .ToArray();

        dbContext.NewsArticleTranslations.RemoveRange(removed);

        // 译文属于资源表示的一部分：刷新主表并发令牌，保证 If-Match 覆盖“主表+译文”表示。
        // 新闻模块不发地点事件，无 Outbox 写入。
        var article = await dbContext.NewsArticles.FirstOrDefaultAsync(a => a.Id == articleId, ct)
            ?? throw new NewsArticleNotFoundException(articleId);
        dbContext.Entry(article).Property(a => a.ConcurrencyToken).CurrentValue = Guid.NewGuid();

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteTranslationsAsync(Guid articleId, string culture, CancellationToken ct = default)
    {
        var existing = await dbContext.NewsArticleTranslations
            .Where(translation => translation.NewsArticleId == articleId && translation.Culture == culture)
            .ToListAsync(ct);

        if (existing.Count == 0)
        {
            return false;
        }

        dbContext.NewsArticleTranslations.RemoveRange(existing);

        // 与 ReplaceTranslationsAsync 同理：表示发生变化，刷新并发令牌。
        var article = await dbContext.NewsArticles.FirstOrDefaultAsync(a => a.Id == articleId, ct)
            ?? throw new NewsArticleNotFoundException(articleId);
        dbContext.Entry(article).Property(a => a.ConcurrencyToken).CurrentValue = Guid.NewGuid();

        await dbContext.SaveChangesAsync(ct);
        return true;
    }
}
