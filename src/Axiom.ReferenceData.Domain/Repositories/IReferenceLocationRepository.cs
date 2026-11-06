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

    /// <summary>批量读取指定地点在若干语言下的翻译，供列表一次性合并。</summary>
    Task<List<ReferenceLocationTranslation>> ListTranslationsAsync(
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<string> cultures,
        CancellationToken ct = default);

    /// <summary>读取单个地点在指定语言下的全部已翻译字段。</summary>
    Task<List<ReferenceLocationTranslation>> GetTranslationsAsync(
        Guid locationId,
        string culture,
        CancellationToken ct = default);

    /// <summary>全量替换某个地点在指定语言下的翻译集合（未出现在 values 中的字段会被删除）。</summary>
    Task ReplaceTranslationsAsync(
        Guid locationId,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default);

    /// <summary>删除某个地点在指定语言下的全部翻译；返回是否确实删除了记录。</summary>
    Task<bool> DeleteTranslationsAsync(Guid locationId, string culture, CancellationToken ct = default);
}
