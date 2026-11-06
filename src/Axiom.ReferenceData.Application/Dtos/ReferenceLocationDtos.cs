// Application 层 DTO：地点档案。
using Axiom.ReferenceData.Domain.Entities;

namespace Axiom.ReferenceData.Application.Dtos;

public sealed record ReferenceLocationDto(
    Guid Id,
    Guid ConcurrencyToken,
    string Type,
    string Code,
    string Name,
    string CountryCode,
    string? City,
    double? Latitude,
    double? Longitude);

public sealed record UpsertReferenceLocationInput(
    string Code,
    string Name,
    string CountryCode,
    string? City = null,
    double? Latitude = null,
    double? Longitude = null);

public sealed record ReferenceLocationListQuery(
    string? Code = null,
    string? Name = null,
    int Page = 1,
    int PageSize = 20);

// 输入校验（按 ABP 约定：校验逻辑归 Application 层 DTO）。
public static class ReferenceLocationInputValidator
{
    public const int MaxNameLength = 200;
    public const int MaxCityLength = 100;

    public static Dictionary<string, string[]> Validate(
        ReferenceLocationType type,
        UpsertReferenceLocationInput input,
        bool isCreate)
    {
        var errors = new Dictionary<string, string[]>();

        var code = input.Code?.Trim() ?? string.Empty;
        if (!ReferenceLocation.IsValidCode(type, code))
            errors["code"] = [ReferenceLocation.GetCodeFormatError(type)];

        var name = input.Name?.Trim() ?? string.Empty;
        if (isCreate && string.IsNullOrWhiteSpace(name))
            errors["name"] = [$"名称必填且不能超过 {ReferenceLocation.MaxNameLength} 个字符。"];
        else if (name.Length > ReferenceLocation.MaxNameLength)
            errors["name"] = [$"名称必填且不能超过 {ReferenceLocation.MaxNameLength} 个字符。"];

        var countryCode = input.CountryCode?.Trim() ?? string.Empty;
        if (countryCode.Length != 2 || !countryCode.All(char.IsAsciiLetter))
            errors["countryCode"] = ["国家代码必须是 2 个英文字母。"];

        if (input.City is { Length: > ReferenceLocation.MaxCityLength })
            errors["city"] = [$"城市名称不能超过 {ReferenceLocation.MaxCityLength} 个字符。"];

        if (input.Latitude is < -90 or > 90)
            errors["latitude"] = ["纬度必须位于 -90 到 90 之间。"];

        if (input.Longitude is < -180 or > 180)
            errors["longitude"] = ["经度必须位于 -180 到 180 之间。"];

        return errors;
    }
}
