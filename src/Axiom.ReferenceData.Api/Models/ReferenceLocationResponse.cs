// 定义地点档案 API 返回给客户端的数据结构。
namespace Axiom.ReferenceData.Api.Models;

public sealed record ReferenceLocationResponse(
    Guid Id,
    string Code,
    string Name,
    string CountryCode,
    string? City,
    double? Latitude,
    double? Longitude);
