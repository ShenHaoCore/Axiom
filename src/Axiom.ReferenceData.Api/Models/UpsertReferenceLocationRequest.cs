// 承载地点档案新增和修改时的外部输入。
namespace Axiom.ReferenceData.Api.Models;

public sealed record UpsertReferenceLocationRequest(
    string? Code,
    string? Name,
    string? CountryCode,
    string? City,
    double? Latitude,
    double? Longitude);
