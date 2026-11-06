// 行政区应用服务接口（Application 层）。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Application.Services;

public interface IRegionAppService
{
    Task<PagedResponse<RegionDto>> ListAsync(RegionListQuery query, CancellationToken ct = default);
    Task<RegionTreeDto?> GetTreeAsync(string countryCode, int depth, CancellationToken ct = default);
    Task<RegionDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<(RegionDto Dto, Uri LocationUri)> CreateAsync(CreateRegionInput input, CancellationToken ct = default);
    Task<RegionDto> UpdateAsync(Guid id, UpdateRegionInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
