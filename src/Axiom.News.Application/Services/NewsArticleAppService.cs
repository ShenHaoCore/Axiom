// 新闻文章应用服务实现（Application 层）。
// 只依赖 Domain 层的 Repository 接口 + Entity + 常量 + 异常，不直接引用 EF Core。
// 读取时按语言回退链合并翻译子表；没有译文则回退到主表默认语言原值。
using Axiom.News.Application.Dtos;
using Axiom.News.Application.ObjectMapping;
using Axiom.News.Domain.Constants;
using Axiom.News.Domain.Entities;
using Axiom.News.Domain.Exceptions;
using Axiom.News.Domain.Repositories;
using Axiom.SharedKernel.Localization;
using Axiom.SharedKernel.Models;

namespace Axiom.News.Application.Services;

public sealed class NewsArticleAppService(
    INewsArticleRepository repository,
    AxiomLocalizationOptions localization) : INewsArticleAppService
{
    public async Task<PagedResponse<NewsArticleDto>> ListAsync(
        NewsArticleListQuery query,
        string? culture = null,
        CancellationToken ct = default)
    {
        var status = string.IsNullOrWhiteSpace(query.Status)
            ? (NewsArticleStatus?)null
            : Enum.TryParse<NewsArticleStatus>(query.Status.Trim(), true, out var parsed) ? parsed : null;

        var paged = await repository.ListAsync(
            query.Page,
            query.PageSize,
            NormalizeString(query.Category),
            status,
            NormalizeString(query.Keyword),
            ct);

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync(
            paged.Items.Select(article => article.Id).ToArray(),
            chain,
            ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        return new PagedResponse<NewsArticleDto>(
            paged.Items.Select(article => NewsArticleMapper.ToDto(article, lookup)).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize);
    }

    public async Task<NewsArticleDto?> GetAsync(Guid id, string? culture = null, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsNoTrackingAsync(id, ct);
        if (entity is null)
        {
            return null;
        }

        var chain = localization.CultureChain(culture);
        var translations = await repository.ListTranslationsAsync([id], chain, ct);
        var lookup = TranslationLookup.Create(translations.Select(ToTranslationValue), chain);

        return NewsArticleMapper.ToDto(entity, lookup);
    }

    public async Task<(NewsArticleDto Dto, Guid ETag)> CreateAsync(
        CreateNewsArticleInput input,
        CancellationToken ct = default)
    {
        var status = ParseStatus(input.Status, NewsArticleStatus.Draft);
        var entity = NewsArticle.Create(
            input.Title,
            input.Summary,
            input.Content,
            input.Category,
            input.Author,
            status);

        await repository.AddAsync(entity, ct);

        var dto = NewsArticleMapper.ToDto(entity);
        return (dto, entity.ConcurrencyToken);
    }

    public async Task<(NewsArticleDto Dto, Guid ETag)> UpdateAsync(
        Guid id,
        UpdateNewsArticleInput input,
        Guid expectedETag,
        CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new NewsArticleNotFoundException(id);

        var nextStatus = input.Status is null
            ? (NewsArticleStatus?)null
            : ParseStatus(input.Status, entity.Status);
        entity.Update(
            input.Title,
            input.Summary,
            input.Content,
            input.Category,
            input.Author,
            nextStatus);

        await repository.UpdateWithETagAsync(entity, expectedETag, ct);

        var dto = NewsArticleMapper.ToDto(entity);
        return (dto, entity.ConcurrencyToken);
    }

    public async Task DeleteAsync(Guid id, Guid expectedETag, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new NewsArticleNotFoundException(id);

        await repository.RemoveWithETagAsync(entity, expectedETag, ct);
    }

    public async Task<TranslationDto?> GetTranslationsAsync(
        Guid id,
        string culture,
        CancellationToken ct = default)
    {
        if (await repository.GetByIdAsNoTrackingAsync(id, ct) is null)
        {
            return null;
        }

        var rows = await repository.GetTranslationsAsync(id, culture, ct);
        return new TranslationDto(culture, rows.ToDictionary(row => row.Field, row => row.Value));
    }

    public async Task<TranslationDto?> SaveTranslationsAsync(
        Guid id,
        string culture,
        SaveTranslationsInput input,
        CancellationToken ct = default)
    {
        if (await repository.GetByIdAsNoTrackingAsync(id, ct) is null)
        {
            return null;
        }

        var values = ValidateTranslationValues(input.Values);
        await repository.ReplaceTranslationsAsync(id, culture, values, ct);
        return new TranslationDto(culture, values);
    }

    public async Task DeleteTranslationsAsync(Guid id, string culture, CancellationToken ct = default)
    {
        if (await repository.GetByIdAsNoTrackingAsync(id, ct) is null)
        {
            throw new NewsArticleNotFoundException(id);
        }

        await repository.DeleteTranslationsAsync(id, culture, ct);
    }

    // ---------- 私有辅助方法 ----------

    private static TranslationValue ToTranslationValue(NewsArticleTranslation row) =>
        new(row.NewsArticleId, row.Culture, row.Field, row.Value);

    private static Dictionary<string, string> ValidateTranslationValues(IReadOnlyDictionary<string, string> values)
    {
        var normalized = new Dictionary<string, string>();
        foreach (var (field, value) in values)
        {
            if (!NewsArticleTranslationFields.IsKnown(field))
            {
                throw NewsArticleValidationException.UnknownTranslatableField(field);
            }

            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                throw NewsArticleValidationException.Required(field);
            }

            var maxLength = NewsArticleTranslationFields.MaxLength(field);
            if (trimmed.Length > maxLength)
            {
                throw NewsArticleValidationException.TooLong(field, maxLength);
            }

            normalized[field] = trimmed;
        }

        return normalized;
    }

    private static string? NormalizeString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static NewsArticleStatus ParseStatus(string? value, NewsArticleStatus fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return Enum.TryParse<NewsArticleStatus>(value.Trim(), true, out var parsed) ? parsed : fallback;
    }
}
