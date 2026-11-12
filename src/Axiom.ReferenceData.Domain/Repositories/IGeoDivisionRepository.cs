// 行政区仓储接口（Domain 层，依赖倒置）。
using Axiom.ReferenceData.Domain.Entities;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Domain.Repositories;

public interface IGeoDivisionRepository
{
    Task<GeoDivision?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<GeoDivision?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken ct = default);

    Task<PagedResponse<GeoDivision>> ListAsync(
        int page,
        int pageSize,
        int? level,
        string? countryCode,
        string? subdivisionCode,
        string? keyword,
        CancellationToken ct = default);

    Task<GeoDivision?> GetCountryByCodeAsync(string countryCode, CancellationToken ct = default);

    Task<List<GeoDivision>> ListForTreeAsync(string countryCode, int depth, CancellationToken ct = default);

    /// <summary>读取指定父节点的直接子级（按代码排序），供层级树按级惰性加载。</summary>
    Task<List<GeoDivision>> ListByParentAsync(Guid parentId, CancellationToken ct = default);

    /// <summary>一次读取多个父节点的直接子级（用于统计各子级的下级数量与同名子级检测）。</summary>
    Task<List<GeoDivision>> ListByParentIdsAsync(IReadOnlyCollection<Guid> parentIds, CancellationToken ct = default);

    Task<bool> AnyByUniqueKeyAsync(int level, string countryCode, string code, CancellationToken ct = default);
    Task<bool> AnyByKindAsync(string kind, CancellationToken ct = default);
    Task<bool> HasChildrenAsync(Guid parentId, CancellationToken ct = default);

    Task AddAsync(GeoDivision region, CancellationToken ct = default);
    Task UpdateAsync(GeoDivision region, CancellationToken ct = default);
    Task RemoveAsync(GeoDivision region, CancellationToken ct = default);

    /// <summary>批量读取指定行政区在若干语言下的翻译，供列表/层级树一次性合并。</summary>
    Task<List<GeoDivisionTranslation>> ListTranslationsAsync(
        IReadOnlyCollection<Guid> regionIds,
        IReadOnlyCollection<string> cultures,
        CancellationToken ct = default);

    /// <summary>读取单个行政区在指定语言下的全部已翻译字段。</summary>
    Task<List<GeoDivisionTranslation>> GetTranslationsAsync(
        Guid regionId,
        string culture,
        CancellationToken ct = default);

    /// <summary>全量替换某个行政区在指定语言下的翻译集合（未出现在 values 中的字段会被删除）。</summary>
    Task ReplaceTranslationsAsync(
        Guid regionId,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default);

    /// <summary>删除某个行政区在指定语言下的全部翻译；返回是否确实删除了记录。</summary>
    Task<bool> DeleteTranslationsAsync(Guid regionId, string culture, CancellationToken ct = default);
}
