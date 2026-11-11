// 行政区列表 / 详情 DTO，映射网关返回的 JSON。
namespace Axiom.Web.Models;

public sealed record RegionView(
    Guid Id,
    Guid? ParentId,
    int Level,
    string CountryCode,
    string? Admin1Code,
    string Code,
    string Name,
    string? AsciiName,
    string? LocalName,
    double? Latitude,
    double? Longitude,
    long? Population,
    int? GeoNameId);

public sealed record RegionTreeView(
    Guid Id,
    int Level,
    string CountryCode,
    string? Admin1Code,
    string Code,
    string Name,
    long? Population,
    IReadOnlyList<RegionTreeView> Children);

/// <summary>/children 接口的子级条目：ChildCount 渲染展开箭头，SameNameChildCount 识别直辖市。</summary>
public sealed record RegionChildView(
    Guid Id,
    int Level,
    string CountryCode,
    string? Admin1Code,
    string Code,
    string Name,
    long? Population,
    int ChildCount,
    int SameNameChildCount);

/// <summary>行政区树单个节点的展示模型（服务端渲染节点行 + 空的惰性子级容器）。</summary>
public sealed record RegionNodeViewModel(
    Guid Id,
    int Level,
    string Code,
    string Name,
    long? Population,
    bool HasChildren,
    bool IsMunicipality)
{
    /// <summary>由 /children 接口返回的子级构造展示模型。</summary>
    public static RegionNodeViewModel FromChild(RegionChildView child) => new(
        child.Id,
        child.Level,
        child.Code,
        child.Name,
        child.Population,
        // 与父级同名的下级（直辖市市级重复）会被合并隐藏，不参与展开判断。
        HasChildren: child.ChildCount > child.SameNameChildCount,
        IsMunicipality: child is { Level: 2, SameNameChildCount: > 0 });
}
