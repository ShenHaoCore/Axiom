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
        var entity = NewsArticleMapper.ToEntity(input, status);

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

        ApplyUpdate(entity, input);

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

    private static void ApplyUpdate(NewsArticle entity, UpdateNewsArticleInput input)
    {
        if (input.Title is not null) entity.Title = input.Title.Trim();
        if (input.Summary is not null) entity.Summary = NormalizeNullable(input.Summary);
        if (input.Content is not null) entity.Content = input.Content.Trim();
        if (input.Category is not null) entity.Category = input.Category.Trim();
        if (input.Author is not null) entity.Author = input.Author.Trim();

        if (input.Status is { } statusStr)
        {
            var newStatus = ParseStatus(statusStr, entity.Status);
            if (newStatus == NewsArticleStatus.Published && entity.Status != NewsArticleStatus.Published)
            {
                entity.PublishedAt = DateTimeOffset.UtcNow;
            }
            entity.Status = newStatus;
        }

        entity.ConcurrencyToken = Guid.NewGuid();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? NormalizeString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static NewsArticleStatus ParseStatus(string? value, NewsArticleStatus fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return Enum.TryParse<NewsArticleStatus>(value.Trim(), true, out var parsed) ? parsed : fallback;
    }
}
