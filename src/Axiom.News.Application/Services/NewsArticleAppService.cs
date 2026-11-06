// 新闻文章应用服务实现（Application 层）。
// 只依赖 Domain 层的 Repository 接口 + Entity + 常量 + 异常，不直接引用 EF Core。
using Axiom.News.Application.Dtos;
using Axiom.News.Application.ObjectMapping;
using Axiom.News.Domain.Entities;
using Axiom.News.Domain.Exceptions;
using Axiom.News.Domain.Repositories;
using Axiom.SharedKernel.Models;

namespace Axiom.News.Application.Services;

public sealed class NewsArticleAppService(INewsArticleRepository repository) : INewsArticleAppService
{
    public async Task<PagedResponse<NewsArticleDto>> ListAsync(
        NewsArticleListQuery query,
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

        return new PagedResponse<NewsArticleDto>(
            paged.Items.Select(NewsArticleMapper.ToDto).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize);
    }

    public async Task<NewsArticleDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsNoTrackingAsync(id, ct);
        return entity is null ? null : NewsArticleMapper.ToDto(entity);
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

    // ---------- 私有辅助方法 ----------

    private static string? NormalizeString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static NewsArticleStatus ParseStatus(string? value, NewsArticleStatus fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return Enum.TryParse<NewsArticleStatus>(value.Trim(), true, out var parsed) ? parsed : fallback;
    }
}
