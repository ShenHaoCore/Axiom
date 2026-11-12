// 通用枚举字典仓储接口（Domain 层，依赖倒置）。
using Axiom.ReferenceData.Domain.Entities;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Domain.Repositories;

public interface IReferenceEnumRepository
{
    Task<ReferenceEnum?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<ReferenceEnum?> GetByCategoryAndCodeAsync(string category, string code, CancellationToken ct = default);

    Task<PagedResponse<ReferenceEnum>> ListAsync(
        string? category,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<bool> ExistsAsync(string category, string code, CancellationToken ct = default);

    Task AddAsync(ReferenceEnum entity, CancellationToken ct = default);
    Task UpdateAsync(ReferenceEnum entity, CancellationToken ct = default);
    Task RemoveAsync(ReferenceEnum entity, CancellationToken ct = default);
}
