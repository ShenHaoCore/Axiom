// 新闻文章实体（Domain 层）：聚合根，承载内容与生命周期状态的不变量。
// 所有字段变更、状态流转都必须经过本实体的方法，避免上层绕过业务规则直接改属性。
using Axiom.News.Domain.Constants;
using Axiom.News.Domain.Exceptions;

namespace Axiom.News.Domain.Entities;

public sealed class NewsArticle
{
    // 供 EF Core 物化实体使用，不应在业务代码中调用。
    private NewsArticle()
    {
    }

    public Guid Id { get; private set; }
    public Guid ConcurrencyToken { get; private set; }

    public string Title { get; private set; } = null!;
    public string? Summary { get; private set; }
    public string Content { get; private set; } = null!;
    public string Category { get; private set; } = null!;
    public string Author { get; private set; } = null!;
    public NewsArticleStatus Status { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>创建新闻稿。首次直接以 Published 提交时会记录发布时间。</summary>
    public static NewsArticle Create(
        string title,
        string? summary,
        string content,
        string category,
        string author,
        NewsArticleStatus status)
    {
        var article = new NewsArticle
        {
            Id = Guid.NewGuid(),
            ConcurrencyToken = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Status = NewsArticleStatus.Draft,
            Title = string.Empty,
            Content = string.Empty,
            Category = string.Empty,
            Author = string.Empty
        };

        article.ChangeDetails(title, summary, content, category, author);
        article.ChangeStatus(status);
        return article;
    }

    /// <summary>更新内容。参数为 null 表示该字段不修改；status 为 null 表示状态保持不变。</summary>
    public void Update(
        string? title,
        string? summary,
        string? content,
        string? category,
        string? author,
        NewsArticleStatus? status)
    {
        ChangeDetails(title, summary, content, category, author);

        if (status is { } nextStatus)
        {
            ChangeStatus(nextStatus);
        }

        Touch();
    }

    /// <summary>切换状态。由 Draft 转为 Published 时记录发布时间。</summary>
    private void ChangeStatus(NewsArticleStatus status)
    {
        if (status == Status)
        {
            return;
        }

        Status = status;
        if (status == NewsArticleStatus.Published && PublishedAt is null)
        {
            PublishedAt = DateTimeOffset.UtcNow;
        }
    }

    private void ChangeDetails(
        string? title,
        string? summary,
        string? content,
        string? category,
        string? author)
    {
        if (title is not null)
        {
            Title = RequiredText(title, nameof(Title), NewsArticleLengths.MaxTitleLength);
        }

        if (summary is not null)
        {
            Summary = OptionalText(summary, nameof(Summary), NewsArticleLengths.MaxSummaryLength);
        }

        if (content is not null)
        {
            Content = RequiredText(content, nameof(Content), NewsArticleLengths.MaxContentLength);
        }

        if (category is not null)
        {
            Category = RequiredText(category, nameof(Category), NewsArticleLengths.MaxCategoryLength);
        }

        if (author is not null)
        {
            Author = RequiredText(author, nameof(Author), NewsArticleLengths.MaxAuthorLength);
        }
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        ConcurrencyToken = Guid.NewGuid();
    }

    private static string RequiredText(string value, string field, int maxLength)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new NewsArticleValidationException(field, "该字段不能为空。");
        }

        if (trimmed.Length > maxLength)
        {
            throw new NewsArticleValidationException(field, $"长度不能超过 {maxLength} 个字符。");
        }

        return trimmed;
    }

    private static string? OptionalText(string value, string field, int maxLength)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new NewsArticleValidationException(field, $"长度不能超过 {maxLength} 个字符。");
        }

        return trimmed;
    }
}
