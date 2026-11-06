// 新闻文章仓储接口（Domain 层，依赖倒置）。
// Infrastructure 层实现此接口。并发令牌设置由 Repository 内部处理。
using Axiom.News.Domain.Entities;
using Axiom.SharedKernel.Models;

namespace Axiom.News.Domain.Repositories;

public interface INewsArticleRepository
{
    Task<NewsArticle?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<NewsArticle?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken ct = default);

    /// <summary>列出文章并按条件筛选、排序、分页。</summary>
    Task<PagedResponse<NewsArticle>> ListAsync(
        int page,
        int pageSize,
        string? category,
        NewsArticleStatus? status,
        string? keyword,
        CancellationToken ct = default);

    Task AddAsync(NewsArticle article, CancellationToken ct = default);

    /// <summary>带并发检查的更新——Repository 内部用 expectedVersion 设置 EF Core OriginalValue。</summary>
    Task UpdateWithETagAsync(NewsArticle article, Guid expectedVersion, CancellationToken ct = default);

    /// <summary>带并发检查的删除。</summary>
    Task RemoveWithETagAsync(NewsArticle article, Guid expectedVersion, CancellationToken ct = default);
}
