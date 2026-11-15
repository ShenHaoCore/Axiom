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

    Task<ReferenceEnumDto?> GetAsync(Guid id, string? culture = null, CancellationToken ct = default);

    Task<(ReferenceEnumDto Dto, Uri LocationUri)> CreateAsync(CreateReferenceEnumInput input, CancellationToken ct = default);

    Task<ReferenceEnumDto> UpdateAsync(Guid id, UpdateReferenceEnumInput input, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<TranslationDto?> GetTranslationsAsync(Guid id, string culture, CancellationToken ct = default);

    Task<TranslationDto?> SaveTranslationsAsync(
        Guid id,
        string culture,
        SaveTranslationsInput input,
        CancellationToken ct = default);

    Task DeleteTranslationsAsync(Guid id, string culture, CancellationToken ct = default);
}
