// 表示不随航班、船期或车次变化的地点基础档案。
namespace Axiom.ReferenceData.Api.Models;

public sealed class ReferenceLocation
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public ReferenceLocationType Type { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string CountryCode { get; set; }
    public string? City { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public ReferenceLocationResponse ToResponse() => new(
        Id,
        Code,
        Name,
        CountryCode,
        City,
        Latitude,
        Longitude);
}
