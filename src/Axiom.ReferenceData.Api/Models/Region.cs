// 行政区层级实体：国家 / 一级行政区（省/州/直辖市）/ 城市。
// 种子数据来自 GeoNames，通过 GeoNameId 保证幂等。
namespace Axiom.ReferenceData.Api.Models;

public sealed class Region
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public RegionLevel Level { get; set; }
    public required string CountryCode { get; set; }
    public string? Admin1Code { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? AsciiName { get; set; }
    public string? LocalName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public long? Population { get; set; }
    public int? GeoNameId { get; set; }
}
