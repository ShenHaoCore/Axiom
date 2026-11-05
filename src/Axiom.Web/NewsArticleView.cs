// 定义新闻列表与详情从新闻 API 返回的数据结构。
namespace Axiom.Web;

public sealed record NewsArticleView(
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
