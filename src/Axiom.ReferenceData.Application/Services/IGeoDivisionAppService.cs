// 行政区应用服务接口（Application 层）。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Application.Services;

public interface IGeoDivisionAppService
{
    /// <summary>分页查询；culture 为 null 时使用默认语言，并按回退链合并译文。</summary>
    Task<PagedResponse<GeoDivisionDto>> ListAsync(GeoDivisionListQuery query, string? culture = null, CancellationToken ct = default);

    /// <summary>按语言合并译文后返回国家层级树。</summary>
    Task<GeoDivisionTreeDto?> GetTreeAsync(string countryCode, int depth, string? culture = null, CancellationToken ct = default);

    /// <summary>返回指定节点的直接子级（含下级数量），供前端惰性树展开。</summary>
    Task<IReadOnlyList<GeoDivisionChildDto>> GetChildrenAsync(
        Guid id,
        string? culture = null,
        CancellationToken ct = default);

    /// <summary>按 ID 读取；同时返回并发令牌，供接口层写入 ETag 响应头。</summary>
    Task<(GeoDivisionDto? Dto, Guid? ETag)> GetAsync(Guid id, string? culture = null, CancellationToken ct = default);

    Task<(GeoDivisionDto Dto, Uri LocationUri, Guid ETag)> CreateAsync(
        CreateGeoDivisionInput input,
        CancellationToken ct = default);

    /// <summary>按期望令牌更新；令牌与库中不一致时抛出并发冲突。</summary>
    Task<(GeoDivisionDto Dto, Guid ETag)> UpdateAsync(
        Guid id,
        UpdateGeoDivisionInput input,
        Guid expectedToken,
        CancellationToken ct = default);

    /// <summary>按期望令牌删除；令牌与库中不一致时抛出并发冲突。</summary>
    Task DeleteAsync(Guid id, Guid expectedToken, CancellationToken ct = default);

    /// <summary>读取单个行政区在指定语言下的原始译文；不存在时返回 null。</summary>
    Task<TranslationDto?> GetTranslationsAsync(Guid id, string culture, CancellationToken ct = default);

    /// <summary>全量替换单个行政区在指定语言下的译文；不存在时返回 null。</summary>
    Task<TranslationDto?> SaveTranslationsAsync(
        Guid id,
        string culture,
        SaveTranslationsInput input,
        CancellationToken ct = default);

    /// <summary>删除单个行政区在指定语言下的全部译文；不存在时抛出 NotFound 异常。</summary>
    Task DeleteTranslationsAsync(Guid id, string culture, CancellationToken ct = default);
}
