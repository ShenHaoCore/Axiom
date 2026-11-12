// Application 层 DTO：行政区。
// Entity 在 Domain 层，DTO 在 Application 层，职责分离。
namespace Axiom.ReferenceData.Application.Dtos;

public sealed record GeoDivisionDto(
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

public sealed record GeoDivisionTreeDto(
    Guid Id,
    int Level,
    string Kind,
    string CountryCode,
    string? SubdivisionCode,
    string Code,
    string Name,
    long? Population,
    IReadOnlyList<GeoDivisionTreeDto> Children);

/// <summary>
/// 层级树的单级子项：ChildCount 支撑惰性树展开箭头，
/// SameNameChildCount 支撑前端把直辖市（与父级同名的下级）合并展示。
/// </summary>
public sealed record GeoDivisionChildDto(
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

public sealed record CreateGeoDivisionInput(
    int Level,
    string CountryCode,
    string? SubdivisionCode = null,
    string? Code = null,
    string Name = null!,
    string? AsciiName = null,
    string? LocalName = null,
    double? Latitude = null,
    double? Longitude = null,
    long? Population = null,
    Guid? ParentId = null,
    string? Kind = null);

public sealed record UpdateGeoDivisionInput(
    string? Name = null,
    string? AsciiName = null,
    string? LocalName = null,
    string? Code = null,
    double? Latitude = null,
    double? Longitude = null,
    long? Population = null,
    Guid? ParentId = null,
    string? Kind = null);

public sealed record GeoDivisionListQuery(
    int? Level = null,
    string? CountryCode = null,
    string? SubdivisionCode = null,
    string? Keyword = null,
    int Page = 1,
    int PageSize = 50);

/// <summary>某个实体在单一语言下的翻译集合（字段名 → 译文）。</summary>
public sealed record TranslationDto(string Culture, IReadOnlyDictionary<string, string> Values);

/// <summary>整语言翻译的写入体：未出现在 Values 中的字段会被删除。</summary>
public sealed record SaveTranslationsInput(IReadOnlyDictionary<string, string> Values);
