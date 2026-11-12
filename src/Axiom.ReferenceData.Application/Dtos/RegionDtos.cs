// Application 层 DTO：行政区。按 (country, code) 复合业务键标识，对齐 geo_division 数据集结构。
namespace Axiom.ReferenceData.Application.Dtos;

public sealed record RegionDto(
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
    int? GeoNameId);

public sealed record RegionTreeDto(
    string CountryCode,
    string Code,
    string? ParentCode,
    int Depth,
    string Type,
    string Name,
    long? Population,
    IReadOnlyList<RegionTreeDto> Children);

/// <summary>
/// 层级树的单级子项：ChildCount 支撑惰性树的展开箭头渲染，
/// SameNameChildCount 支撑前端把直辖市（与父级同名的下级）合并展示，Type 驱动徽标。
/// </summary>
public sealed record RegionChildDto(
    string CountryCode,
    string Code,
    string? ParentCode,
    int Depth,
    string Type,
    string Name,
    long? Population,
    int ChildCount,
    int SameNameChildCount);

public sealed record CreateRegionInput(
    int Depth,
    string CountryCode,
    string Code,
    string? ParentCode = null,
    string? Type = null,
    string? CodeIso = null,
    string? SortKey = null,
    string? Abbrev = null,
    string Name = null!,
    string? AsciiName = null,
    string? LocalName = null,
    double? Latitude = null,
    double? Longitude = null,
    long? Population = null,
    string? Status = null);

/// <summary>Code / Depth / Type 为业务标识，更新接口不接受修改。</summary>
public sealed record UpdateRegionInput(
    string? Name = null,
    string? AsciiName = null,
    string? LocalName = null,
    double? Latitude = null,
    double? Longitude = null,
    long? Population = null,
    string? CodeIso = null,
    string? SortKey = null,
    string? Abbrev = null,
    string? Status = null);

public sealed record RegionListQuery(
    int? Depth = null,
    string? CountryCode = null,
    string? ParentCode = null,
    string? Keyword = null,
    int Page = 1,
    int PageSize = 50);

/// <summary>某个实体在单一语言下的翻译集合（字段名 → 译文）。</summary>
public sealed record TranslationDto(string Culture, IReadOnlyDictionary<string, string> Values);

/// <summary>整语言翻译的写入体：未出现在 Values 中的字段会被删除。</summary>
public sealed record SaveTranslationsInput(IReadOnlyDictionary<string, string> Values);
