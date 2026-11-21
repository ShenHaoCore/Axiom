// 通用枚举字典应用服务接口。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Application.Services;

public interface IReferenceEnumAppService
{
    /// <summary>分页查询；culture 为 null 时使用默认语言，并按回退链合并译文。</summary>
    Task<PagedResponse<ReferenceEnumDto>> ListAsync(
        ReferenceEnumListQuery query,
        string? culture = null,
        CancellationToken ct = default);

    /// <summary>读取单个枚举项；返回并发令牌（ETag），不存在时 Dto 与 ETag 均为 null。</summary>
    Task<(ReferenceEnumDto? Dto, Guid? ETag)> GetAsync(Guid id, string? culture = null, CancellationToken ct = default);

    Task<(ReferenceEnumDto Dto, Uri LocationUri, Guid ETag)> CreateAsync(CreateReferenceEnumInput input, CancellationToken ct = default);

    /// <summary>按 If-Match 期望令牌更新；返回新并发令牌。</summary>
    Task<(ReferenceEnumDto Dto, Guid ETag)> UpdateAsync(Guid id, UpdateReferenceEnumInput input, Guid expectedToken, CancellationToken ct = default);

    /// <summary>按 If-Match 期望令牌删除。</summary>
    Task DeleteAsync(Guid id, Guid expectedToken, CancellationToken ct = default);

    Task<TranslationDto?> GetTranslationsAsync(Guid id, string culture, CancellationToken ct = default);

    Task<TranslationDto?> SaveTranslationsAsync(
        Guid id,
        string culture,
        SaveTranslationsInput input,
        CancellationToken ct = default);

    Task DeleteTranslationsAsync(Guid id, string culture, CancellationToken ct = default);
}
