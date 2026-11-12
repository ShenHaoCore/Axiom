// 行政区仓储接口（Domain 层，依赖倒置）：按 (country, code) 复合业务键寻址。
using Axiom.ReferenceData.Domain.Entities;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Domain.Repositories;

public interface IRegionRepository
{
    Task<Region?> GetAsync(string countryCode, string code, CancellationToken ct = default);
    Task<Region?> GetAsNoTrackingAsync(string countryCode, string code, CancellationToken ct = default);

    Task<PagedResponse<Region>> ListAsync(
        int page,
        int pageSize,
        int? depth,
        string? countryCode,
        string? parentCode,
        string? keyword,
        CancellationToken ct = default);

    /// <summary>读取国家行（Depth = 0，Code = 国家代码）。</summary>
    Task<Region?> GetCountryAsync(string countryCode, CancellationToken ct = default);

    /// <summary>读取国家下 depth ≤ maxDepth 的全部行，供层级树一次性构建。</summary>
    Task<List<Region>> ListForTreeAsync(string countryCode, int maxDepth, CancellationToken ct = default);

    /// <summary>读取指定父节点的直接子级（按代码排序），供层级树按级惰性加载。</summary>
    Task<List<Region>> ListChildrenAsync(string countryCode, string parentCode, CancellationToken ct = default);

    /// <summary>一次读取多个父节点的直接子级（用于统计各子级的下级数量与同名子级检测）。</summary>
    Task<List<Region>> ListByParentCodesAsync(string countryCode, IReadOnlyCollection<string> parentCodes, CancellationToken ct = default);

    Task<bool> HasChildrenAsync(string countryCode, string parentCode, CancellationToken ct = default);

    /// <summary>GeoNameId 唯一键冲突检测（GeoNames 同步链路使用）。</summary>
    Task<bool> GeoNameIdExistsAsync(int geoNameId, CancellationToken ct = default);

    Task AddAsync(Region region, CancellationToken ct = default);
    Task UpdateAsync(Region region, CancellationToken ct = default);
    Task RemoveAsync(Region region, CancellationToken ct = default);

    /// <summary>批量读取同一国家下若干区划在若干语言中的翻译，供列表/层级树一次性合并。</summary>
    Task<List<RegionTranslation>> ListTranslationsAsync(
        string countryCode,
        IReadOnlyCollection<string> codes,
        IReadOnlyCollection<string> cultures,
        CancellationToken ct = default);

    /// <summary>读取单个行政区在指定语言下的全部已翻译字段。</summary>
    Task<List<RegionTranslation>> GetTranslationsAsync(
        string countryCode,
        string code,
        string culture,
        CancellationToken ct = default);

    /// <summary>全量替换某个行政区在指定语言下的翻译集合（未出现在 values 中的字段会被删除）。</summary>
    Task ReplaceTranslationsAsync(
        string countryCode,
        string code,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default);

    /// <summary>删除某个行政区在指定语言下的全部翻译；返回是否确实删除了记录。</summary>
    Task<bool> DeleteTranslationsAsync(
        string countryCode,
        string code,
        string culture,
        CancellationToken ct = default);
}
