// 新闻文章的只读响应 DTO。
namespace Axiom.News.Api.Models;

public sealed record NewsArticleResponse(
    Guid Id,
    string Title,
    string? Summary,
    string Content,
    string Category,
    string Status,
    string Author,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
