// 手工 DTO ↔ Entity 映射（Application 层）。
// 引入 AutoMapper 对于 5-10 字段的简单 Entity 不值得，手写更清晰。
using Axiom.News.Application.Dtos;
using Axiom.News.Domain.Entities;

namespace Axiom.News.Application.ObjectMapping;

public static class NewsArticleMapper
{
    public static NewsArticleDto ToDto(NewsArticle entity) => new(
        Id: entity.Id,
        Title: entity.Title,
        Summary: entity.Summary,
        Content: entity.Content,
        Category: entity.Category,
        Status: entity.Status.ToString(),
        Author: entity.Author,
        PublishedAt: entity.PublishedAt,
        CreatedAt: entity.CreatedAt,
        UpdatedAt: entity.UpdatedAt,
        ConcurrencyToken: entity.ConcurrencyToken);

    public static NewsArticle ToEntity(CreateNewsArticleInput input, NewsArticleStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Title = input.Title.Trim(),
        Summary = NormalizeNullable(input.Summary),
        Content = input.Content.Trim(),
        Category = input.Category.Trim(),
        Author = input.Author.Trim(),
        Status = status,
        PublishedAt = status == NewsArticleStatus.Published ? DateTimeOffset.UtcNow : null
    };

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
