// 地点档案应用服务接口（Application 层）。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Application.Services;

public interface IReferenceLocationAppService
{
    /// <summary>分页查询；culture 为 null 时使用默认语言，并按回退链合并译文。</summary>
    Task<PagedResponse<ReferenceLocationDto>> ListAsync(
        ReferenceLocationType type,
        ReferenceLocationListQuery query,
        string? culture = null,
        CancellationToken ct = default);

    /// <summary>按语言合并译文后返回单条地点档案。</summary>
    Task<(ReferenceLocationDto? Dto, Guid? ETag)> GetAsync(
        ReferenceLocationType type,
        Guid id,
        string? culture = null,
        CancellationToken ct = default);

    Task<(ReferenceLocationDto Dto, Guid ETag, Uri LocationUri)> CreateAsync(
        ReferenceLocationType type,
        UpsertReferenceLocationInput input,
        CancellationToken ct = default);

    Task<(ReferenceLocationDto Dto, Guid ETag)> UpdateAsync(
        ReferenceLocationType type,
        Guid id,
        UpsertReferenceLocationInput input,
        Guid expectedETag,
        CancellationToken ct = default);

    Task DeleteAsync(
        ReferenceLocationType type,
        Guid id,
        Guid expectedETag,
        CancellationToken ct = default);

    /// <summary>读取单条地点档案在指定语言下的原始译文；不存在时返回 null。</summary>
    Task<TranslationDto?> GetTranslationsAsync(
        ReferenceLocationType type,
        Guid id,
        string culture,
        CancellationToken ct = default);

    /// <summary>全量替换单条地点档案在指定语言下的译文；不存在时返回 null。</summary>
    Task<TranslationDto?> SaveTranslationsAsync(
        ReferenceLocationType type,
        Guid id,
        string culture,
        SaveTranslationsInput input,
        CancellationToken ct = default);

    /// <summary>删除单条地点档案在指定语言下的全部译文；不存在时抛出 NotFound 异常。</summary>
    Task DeleteTranslationsAsync(
        ReferenceLocationType type,
        Guid id,
        string culture,
        CancellationToken ct = default);
}
