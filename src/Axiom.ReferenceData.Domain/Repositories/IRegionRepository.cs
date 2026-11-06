// 行政区仓储接口（Domain 层，依赖倒置）。
using Axiom.ReferenceData.Domain.Entities;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Domain.Repositories;

public interface IRegionRepository
{
    Task<Region?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Region?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken ct = default);

    Task<PagedResponse<Region>> ListAsync(
        int page,
        int pageSize,
        int? level,
        string? countryCode,
        string? admin1Code,
        string? keyword,
        CancellationToken ct = default);

    Task<Region?> GetCountryByCodeAsync(string countryCode, CancellationToken ct = default);

    Task<List<Region>> ListForTreeAsync(string countryCode, int depth, CancellationToken ct = default);

    Task<bool> AnyByUniqueKeyAsync(RegionLevel level, string countryCode, string code, CancellationToken ct = default);
    Task<bool> HasChildrenAsync(Guid parentId, CancellationToken ct = default);

    Task AddAsync(Region region, CancellationToken ct = default);
    Task UpdateAsync(Region region, CancellationToken ct = default);
    Task RemoveAsync(Region region, CancellationToken ct = default);
}
