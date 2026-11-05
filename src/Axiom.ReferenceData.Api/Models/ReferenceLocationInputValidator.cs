// 在 API 边界校验并规范化前检查基础档案输入。
namespace Axiom.ReferenceData.Api.Models;

public static class ReferenceLocationInputValidator
{
    public static Dictionary<string, string[]> Validate(
        ReferenceLocationType type,
        UpsertReferenceLocationRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var code = request.Code?.Trim() ?? string.Empty;

        if (!IsValidCode(type, code))
        {
            errors["code"] = [GetCodeError(type)];
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
        {
            errors["name"] = ["名称必填且不能超过 200 个字符。"];
        }

        var countryCode = request.CountryCode?.Trim() ?? string.Empty;
        if (countryCode.Length != 2 || !countryCode.All(char.IsAsciiLetter))
        {
            errors["countryCode"] = ["国家代码必须是 2 个英文字母。"];
        }

        if (request.City?.Trim().Length > 100)
        {
            errors["city"] = ["城市名称不能超过 100 个字符。"];
        }

        if (request.Latitude is < -90 or > 90)
        {
            errors["latitude"] = ["纬度必须位于 -90 到 90 之间。"];
        }

        if (request.Longitude is < -180 or > 180)
        {
            errors["longitude"] = ["经度必须位于 -180 到 180 之间。"];
        }

        return errors;
    }

    private static bool IsValidCode(ReferenceLocationType type, string code) => type switch
    {
        ReferenceLocationType.Airport => code.Length == 3 && code.All(char.IsAsciiLetter),
        ReferenceLocationType.Seaport => code.Length == 5 && code.All(char.IsAsciiLetterOrDigit),
        ReferenceLocationType.RailStation => code.Length is >= 1 and <= 20 &&
            code.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'),
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
