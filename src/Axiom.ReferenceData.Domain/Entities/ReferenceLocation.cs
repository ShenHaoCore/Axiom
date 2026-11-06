// 地点档案实体（Domain 层）。
// ConcurrencyToken 是 EF Core rowversion 级别的并发令牌。
namespace Axiom.ReferenceData.Domain.Entities;

public sealed class ReferenceLocation
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public ReferenceLocationType Type { get; set; }
    public string Code { get; set; } = string.Empty;
    public required string Name { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string? City { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
