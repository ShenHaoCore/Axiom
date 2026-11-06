// Application 层 DTO：行政区。
// Entity 在 Domain 层，DTO 在 Application 层，职责分离。
using Axiom.ReferenceData.Domain.Entities;

namespace Axiom.ReferenceData.Application.Dtos;

public sealed record RegionDto(
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

public sealed record RegionTreeDto(
    Guid Id,
    int Level,
    string CountryCode,
    string? Admin1Code,
    string Code,
    string Name,
    long? Population,
    IReadOnlyList<RegionTreeDto> Children);

public sealed record CreateRegionInput(
    string Level,
    string CountryCode,
    string? Admin1Code = null,
    string? Code = null,
    string Name = null!,
    string? AsciiName = null,
    string? LocalName = null,
    double? Latitude = null,
    double? Longitude = null,
    long? Population = null,
    Guid? ParentId = null);

public sealed record UpdateRegionInput(
    string? Name = null,
    string? AsciiName = null,
    string? LocalName = null,
    string? Code = null,
    double? Latitude = null,
    double? Longitude = null,
    long? Population = null,
    Guid? ParentId = null);

public sealed record RegionListQuery(
    int? Level = null,
    string? CountryCode = null,
    string? Admin1Code = null,
    string? Keyword = null,
    int Page = 1,
    int PageSize = 50);
