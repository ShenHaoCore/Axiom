// 地点档案应用服务接口（Application 层）。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Application.Services;

public interface IReferenceLocationAppService
{
    Task<PagedResponse<ReferenceLocationDto>> ListAsync(
        ReferenceLocationType type,
        ReferenceLocationListQuery query,
        CancellationToken ct = default);

    Task<(ReferenceLocationDto? Dto, Guid? ETag)> GetAsync(
        ReferenceLocationType type,
        Guid id,
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
}
