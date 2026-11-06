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
        if (!IsValidCode(type, code))
            errors["code"] = [GetCodeError(type)];

        var name = input.Name?.Trim() ?? string.Empty;
        if (isCreate && string.IsNullOrWhiteSpace(name))
            errors["name"] = ["名称必填且不能超过 200 个字符。"];
        else if (name.Length > MaxNameLength)
            errors["name"] = ["名称必填且不能超过 200 个字符。"];

        var countryCode = input.CountryCode?.Trim() ?? string.Empty;
        if (countryCode.Length != 2 || !countryCode.All(char.IsAsciiLetter))
            errors["countryCode"] = ["国家代码必须是 2 个英文字母。"];

        if (input.City is { Length: > MaxCityLength })
            errors["city"] = ["城市名称不能超过 100 个字符。"];

        if (input.Latitude is < -90 or > 90)
            errors["latitude"] = ["纬度必须位于 -90 到 90 之间。"];

        if (input.Longitude is < -180 or > 180)
            errors["longitude"] = ["经度必须位于 -180 到 180 之间。"];

        return errors;
    }

    private static bool IsValidCode(ReferenceLocationType type, string code) => type switch
    {
        ReferenceLocationType.Airport => code.Length == 3 && code.All(char.IsAsciiLetter),
        ReferenceLocationType.Seaport => code.Length == 5 && code.All(char.IsAsciiLetterOrDigit),
        ReferenceLocationType.RailStation => code.Length is >= 1 and <= 20 &&
            code.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'),
        _ => false
    };

    private static string GetCodeError(ReferenceLocationType type) => type switch
    {
        ReferenceLocationType.Airport => "机场代码必须是 3 个英文字母。",
        ReferenceLocationType.Seaport => "港口代码必须是 5 位字母或数字组成的 UN/LOCODE。",
        ReferenceLocationType.RailStation => "火车站代码只能包含字母、数字和连字符，且不能超过 20 个字符。",
        _ => "不支持的基础档案类型。"
    };
}
