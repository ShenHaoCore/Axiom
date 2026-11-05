// 新闻文章实体，包含草稿、发布和归档三种状态。
namespace Axiom.News.Api.Models;

public sealed class NewsArticle
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }
    public string? Summary { get; set; }
    public required string Content { get; set; }
    public required string Category { get; set; }
    public NewsArticleStatus Status { get; set; } = NewsArticleStatus.Draft;
    public required string Author { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public NewsArticleResponse ToResponse() => new(
        Id,
        Title,
        Summary,
        Content,
        Category,
        Status.ToString(),
        Author,
        PublishedAt,
        CreatedAt,
        UpdatedAt);
}
