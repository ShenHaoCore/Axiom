// 将内部 gRPC 机场消息映射为外部 REST 响应。
using Axiom.Contracts.ReferenceData.V1;

namespace Axiom.ApiGateway;

public sealed record AirportResponse(
    Guid Id,
    string Code,
    string Name,
    string CountryCode,
    string? City,
    double? Latitude,
    double? Longitude)
{
    public static AirportResponse FromMessage(Airport airport) => new(
        Guid.Parse(airport.Id),
        airport.Code,
        airport.Name,
        airport.CountryCode,
        airport.HasCity ? airport.City : null,
        airport.HasLatitude ? airport.Latitude : null,
        airport.HasLongitude ? airport.Longitude : null);
}
