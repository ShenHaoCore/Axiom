// 行政区应用服务接口（Application 层）。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Application.Services;

public interface IRegionAppService
{
    /// <summary>分页查询；culture 为 null 时使用默认语言，并按回退链合并译文。</summary>
    Task<PagedResponse<RegionDto>> ListAsync(RegionListQuery query, string? culture = null, CancellationToken ct = default);

    /// <summary>按语言合并译文后返回国家层级树。</summary>
    Task<RegionTreeDto?> GetTreeAsync(string countryCode, int depth, string? culture = null, CancellationToken ct = default);

    Task<RegionDto?> GetAsync(Guid id, string? culture = null, CancellationToken ct = default);

    Task<(RegionDto Dto, Uri LocationUri)> CreateAsync(CreateRegionInput input, CancellationToken ct = default);

    Task<RegionDto> UpdateAsync(Guid id, UpdateRegionInput input, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

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
