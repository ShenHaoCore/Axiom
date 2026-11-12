// 通用枚举字典应用服务接口。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Application.Services;

public interface IReferenceEnumAppService
{
    Task<PagedResponse<ReferenceEnumDto>> ListAsync(ReferenceEnumListQuery query, CancellationToken ct = default);

    Task<ReferenceEnumDto?> GetAsync(Guid id, CancellationToken ct = default);

    Task<(ReferenceEnumDto Dto, Uri LocationUri)> CreateAsync(CreateReferenceEnumInput input, CancellationToken ct = default);

    Task<ReferenceEnumDto> UpdateAsync(Guid id, UpdateReferenceEnumInput input, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
