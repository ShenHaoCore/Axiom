// 地点仓储接口（Domain 层，依赖倒置）。
using Axiom.ReferenceData.Domain.Entities;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Domain.Repositories;

public interface IReferenceLocationRepository
{
    Task<ReferenceLocation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ReferenceLocation?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken ct = default);

    Task<PagedResponse<ReferenceLocation>> ListByTypeAsync(
        ReferenceLocationType type,
        string? code,
        string? name,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<bool> CodeExistsAsync(ReferenceLocationType type, string code, Guid? excludedId, CancellationToken ct = default);

    Task AddAsync(ReferenceLocation location, CancellationToken ct = default);
    Task UpdateWithETagAsync(ReferenceLocation location, Guid expectedVersion, CancellationToken ct = default);
    Task RemoveWithETagAsync(ReferenceLocation location, Guid expectedVersion, CancellationToken ct = default);
}
