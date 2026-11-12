// 行政区列表 / 详情 DTO，映射网关返回的 JSON。
namespace Axiom.Web.Models;

public sealed record GeoDivisionView(
    Guid Id,
    Guid? ParentId,
    int Level,
    string Kind,
    string CountryCode,
    string? SubdivisionCode,
    string Code,
    string Name,
    string? AsciiName,
    string? LocalName,
    double? Latitude,
    double? Longitude,
    long? Population,
    int? GeoNameId);

public sealed record GeoDivisionTreeView(
    Guid Id,
    int Level,
    string Kind,
    string CountryCode,
    string? SubdivisionCode,
    string Code,
    string Name,
    long? Population,
    IReadOnlyList<GeoDivisionTreeView> Children);

/// <summary>/children 接口的子级条目：ChildCount 渲染展开箭头，SameNameChildCount 识别直辖市合并。</summary>
public sealed record GeoDivisionChildView(
    Guid Id,
    int Level,
    string Kind,
    string CountryCode,
    string? SubdivisionCode,
    string Code,
    string Name,
    long? Population,
    int ChildCount,
    int SameNameChildCount);

/// <summary>行政区树单个节点的展示模型（服务端渲染节点行 + 空的惰性子级容器）。</summary>
public sealed record GeoDivisionNodeViewModel(
    Guid Id,
    int Level,
    string Kind,
    string Code,
    string Name,
    long? Population,
    bool HasChildren)
{
    public bool IsMunicipality =>
        string.Equals(Kind, "municipality", StringComparison.OrdinalIgnoreCase);

    public bool IsAutonomousRegion =>
        string.Equals(Kind, "autonomous_region", StringComparison.OrdinalIgnoreCase);

    public bool IsSpecialAdministrativeRegion =>
        string.Equals(Kind, "special_administrative_region", StringComparison.OrdinalIgnoreCase);

    /// <summary>由 /children 接口返回的子级构造展示模型。</summary>
    public static GeoDivisionNodeViewModel FromChild(GeoDivisionChildView child) => new(
        child.Id,
        child.Level,
        child.Kind,
        child.Code,
        child.Name,
        child.Population,
        // 与父级同名的下级（直辖市市级重复）会被合并隐藏，不参与展开判断。
        HasChildren: child.ChildCount > child.SameNameChildCount);
}
