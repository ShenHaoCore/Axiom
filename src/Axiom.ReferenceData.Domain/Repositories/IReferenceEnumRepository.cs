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

    /// <summary>按 If-Match 期望令牌更新；令牌不一致时抛出并发冲突。</summary>
    Task UpdateWithETagAsync(ReferenceEnum entity, Guid expectedToken, CancellationToken ct = default);

    /// <summary>按 If-Match 期望令牌删除；令牌不一致时抛出并发冲突。</summary>
    Task RemoveWithETagAsync(ReferenceEnum entity, Guid expectedToken, CancellationToken ct = default);

    Task<List<ReferenceEnumTranslation>> ListTranslationsAsync(
        IReadOnlyCollection<Guid> enumIds,
        IReadOnlyCollection<string> cultures,
        CancellationToken ct = default);

    Task<List<ReferenceEnumTranslation>> GetTranslationsAsync(
        Guid enumId,
        string culture,
        CancellationToken ct = default);

    Task ReplaceTranslationsAsync(
        Guid enumId,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default);

    Task<bool> DeleteTranslationsAsync(Guid enumId, string culture, CancellationToken ct = default);
}
