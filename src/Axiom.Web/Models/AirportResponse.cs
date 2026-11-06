// 定义机场列表从基础数据 API 返回的数据结构。
namespace Axiom.Web.Models;

public sealed record AirportResponse(
    Guid Id,
    string Code,
    string Name,
    string CountryCode,
    string? City,
    double? Latitude,
    double? Longitude);
