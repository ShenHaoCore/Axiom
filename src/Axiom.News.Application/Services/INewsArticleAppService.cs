// 新闻文章应用服务接口（Application 层）。
// HttpApi 层的 Endpoint 只做 HTTP 绑定，调用此接口完成业务逻辑。
using Axiom.News.Application.Dtos;
using Axiom.SharedKernel.Models;

namespace Axiom.News.Application.Services;

public interface INewsArticleAppService
{
    /// <summary>分页查询；culture 为 null 时使用默认语言，并按回退链合并译文。</summary>
    Task<PagedResponse<NewsArticleDto>> ListAsync(
        NewsArticleListQuery query,
        string? culture = null,
        CancellationToken ct = default);

    /// <summary>按语言合并译文后返回单篇文章。</summary>
    Task<NewsArticleDto?> GetAsync(Guid id, string? culture = null, CancellationToken ct = default);

    /// <returns>返回 DTO + 创建后的 ConcurrencyToken（用于 HTTP ETag 响应头）。</returns>
    Task<(NewsArticleDto Dto, Guid ETag)> CreateAsync(CreateNewsArticleInput input, CancellationToken ct = default);

    /// <returns>返回 DTO + 更新后的 ConcurrencyToken。</returns>
    Task<(NewsArticleDto Dto, Guid ETag)> UpdateAsync(
        Guid id,
        UpdateNewsArticleInput input,
        Guid expectedETag,
        CancellationToken ct = default);

    Task DeleteAsync(Guid id, Guid expectedETag, CancellationToken ct = default);

    /// <summary>读取单篇文章在指定语言下的原始译文；文章不存在时返回 null。</summary>
    Task<TranslationDto?> GetTranslationsAsync(Guid id, string culture, CancellationToken ct = default);

    /// <summary>全量替换单篇文章在指定语言下的译文；文章不存在时返回 null。</summary>
    Task<TranslationDto?> SaveTranslationsAsync(
        Guid id,
        string culture,
        SaveTranslationsInput input,
        CancellationToken ct = default);

    /// <summary>删除单篇文章在指定语言下的全部译文；文章不存在时抛出 NotFound 异常。</summary>
    Task DeleteTranslationsAsync(Guid id, string culture, CancellationToken ct = default);
}
