// 新闻文章应用服务接口（Application 层）。
// HttpApi 层的 Endpoint 只做 HTTP 绑定，调用此接口完成业务逻辑。
using Axiom.News.Application.Dtos;
using Axiom.SharedKernel.Models;

namespace Axiom.News.Application.Services;

public interface INewsArticleAppService
{
    Task<PagedResponse<NewsArticleDto>> ListAsync(NewsArticleListQuery query, CancellationToken ct = default);
    Task<NewsArticleDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <returns>返回 DTO + 创建后的 ConcurrencyToken（用于 HTTP ETag 响应头）。</returns>
    Task<(NewsArticleDto Dto, Guid ETag)> CreateAsync(CreateNewsArticleInput input, CancellationToken ct = default);

    /// <returns>返回 DTO + 更新后的 ConcurrencyToken。</returns>
    Task<(NewsArticleDto Dto, Guid ETag)> UpdateAsync(
        Guid id,
        UpdateNewsArticleInput input,
        Guid expectedETag,
        CancellationToken ct = default);

    Task DeleteAsync(Guid id, Guid expectedETag, CancellationToken ct = default);
}
