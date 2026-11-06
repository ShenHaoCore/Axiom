// 手工 DTO ↔ Entity 映射（Application 层）。
// 引入 AutoMapper 对于 5-10 字段的简单 Entity 不值得，手写更清晰。
using Axiom.News.Application.Dtos;
using Axiom.News.Domain.Constants;
using Axiom.News.Domain.Entities;
using Axiom.SharedKernel.Localization;

namespace Axiom.News.Application.ObjectMapping;

public static class NewsArticleMapper
{
    /// <summary>映射为 DTO；传入翻译查找表时，内容类字段优先使用当前语言的译文。</summary>
    public static NewsArticleDto ToDto(NewsArticle entity, TranslationLookup? translations = null) => new(
        Id: entity.Id,
        Title: translations?.Find(entity.Id, NewsArticleTranslationFields.Title) ?? entity.Title,
        Summary: translations?.Find(entity.Id, NewsArticleTranslationFields.Summary) ?? entity.Summary,
        Content: translations?.Find(entity.Id, NewsArticleTranslationFields.Content) ?? entity.Content,
        Category: entity.Category,
        Status: entity.Status.ToString(),
        Author: entity.Author,
        PublishedAt: entity.PublishedAt,
        CreatedAt: entity.CreatedAt,
        UpdatedAt: entity.UpdatedAt,
        ConcurrencyToken: entity.ConcurrencyToken);
}
