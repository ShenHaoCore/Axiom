// 行政区列表 / 详情 DTO，映射网关返回的 JSON。
// 按 (country, code) 复合业务键标识，对齐 geo_division 数据集结构。
namespace Axiom.Web.Models;

public sealed record RegionView(
    string CountryCode,
    string Code,
    string? ParentCode,
    int Depth,
    string Type,
    string Status,
    string? CodeIso,
    string SortKey,
    string? Abbrev,
    string Name,
    string? AsciiName,
    string? LocalName,
    double? Latitude,
    double? Longitude,
    long? Population,
    int? GeoNameId)
{
    /// <summary>翻译查找使用的实体标识（CN.110000）。</summary>
    public string TranslationKey => $"{CountryCode}.{Code}";
}

public sealed record RegionTreeView(
    string CountryCode,
    string Code,
    string? ParentCode,
    int Depth,
    string Type,
    string Name,
    long? Population,
    IReadOnlyList<RegionTreeView> Children);

/// <summary>/children 接口的子级条目：ChildCount 渲染展开箭头，SameNameChildCount 合并同名市级记录，Type 驱动徽标。</summary>
public sealed record RegionChildView(
    string CountryCode,
    string Code,
    string? ParentCode,
    int Depth,
    string Type,
    string Name,
    long? Population,
    int ChildCount,
    int SameNameChildCount);

/// <summary>与 ReferenceData API 的 type 字符串口径保持一致的常量。</summary>
public static class RegionTypes
{
    public const string Country = "country";
    public const string Province = "province";
    public const string Municipality = "municipality";
    public const string AutonomousRegion = "autonomous_region";
    public const string SpecialAdministrativeRegion = "sar";
    public const string State = "state";
    public const string PrefectureCity = "prefecture_city";
    public const string District = "district";
    public const string County = "county";
    public const string Other = "other";
}

/// <summary>行政区树单个节点的展示模型（服务端渲染节点行 + 空的惰性子级容器）。</summary>
public sealed record RegionNodeViewModel(
    string CountryCode,
    string Code,
    int Depth,
    string Type,
    string Name,
    long? Population,
    bool HasChildren)
{
    /// <summary>由 /children 接口返回的子级构造展示模型。</summary>
    public static RegionNodeViewModel FromChild(RegionChildView child) => new(
        child.CountryCode,
        child.Code,
        child.Depth,
        child.Type,
        child.Name,
        child.Population,
        // 与父级同名的下级（直辖市市级重复）会被合并隐藏，不参与展开判断。
        HasChildren: child.ChildCount > child.SameNameChildCount);
}
