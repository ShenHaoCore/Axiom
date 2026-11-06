// 新闻文章实体（Domain 层）。
// ConcurrencyToken 是 EF Core rowversion 级别的并发令牌，由 Infrastructure 层配置为 IsConcurrencyToken。
namespace Axiom.News.Domain.Entities;

public sealed class NewsArticle
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public required string Title { get; set; }
    public string? Summary { get; set; }
    public required string Content { get; set; }
    public required string Category { get; set; }
    public required string Author { get; set; }
    public NewsArticleStatus Status { get; set; } = NewsArticleStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
