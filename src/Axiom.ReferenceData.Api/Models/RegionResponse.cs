// Region 详情/列表 DTO。
namespace Axiom.ReferenceData.Api.Models;

public sealed record RegionResponse(
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

public sealed record RegionTreeResponse(
    Guid Id,
    int Level,
    string CountryCode,
    string? Admin1Code,
    string Code,
    string Name,
    long? Population,
    IReadOnlyList<RegionTreeResponse> Children)
{
    public RegionTreeResponse WithChildren(IReadOnlyList<RegionTreeResponse> children)
        => this with { Children = children };
}
