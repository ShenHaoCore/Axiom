// 行政区实体（Domain 层）。
namespace Axiom.ReferenceData.Domain.Entities;

public sealed class Region
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public RegionLevel Level { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string? Admin1Code { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AsciiName { get; set; }
    public string? LocalName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public long? Population { get; set; }
    public int? GeoNameId { get; set; }
}
