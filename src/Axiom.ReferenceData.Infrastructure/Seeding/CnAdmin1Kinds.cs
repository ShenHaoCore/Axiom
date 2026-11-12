// 中国一级行政区 Kind 规则：按 SubdivisionCode（GeoNames admin1）识别直辖市/自治区/特区。
using Axiom.ReferenceData.Domain.Constants;

namespace Axiom.ReferenceData.Infrastructure.Seeding;

internal static class CnAdmin1Kinds
{
    private static readonly HashSet<string> MunicipalityCodes = new(StringComparer.Ordinal)
    {
        "11", // 北京
        "12", // 天津
        "31", // 上海
        "50"  // 重庆
    };

    private static readonly HashSet<string> AutonomousRegionCodes = new(StringComparer.Ordinal)
    {
        "15", // 内蒙古
        "45", // 广西
        "54", // 西藏
        "64", // 宁夏
        "65"  // 新疆
    };

    private static readonly HashSet<string> SpecialAdministrativeRegionCodes = new(StringComparer.Ordinal)
    {
        "81", // 香港
        "82"  // 澳门
    };

    public static string Resolve(string countryCode, string? subdivisionCode)
    {
        if (!string.Equals(countryCode, "CN", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(subdivisionCode))
        {
            return GeoDivisionKinds.Standard;
        }

        if (MunicipalityCodes.Contains(subdivisionCode))
        {
            return GeoDivisionKinds.Municipality;
        }

        if (AutonomousRegionCodes.Contains(subdivisionCode))
        {
            return GeoDivisionKinds.AutonomousRegion;
        }

        if (SpecialAdministrativeRegionCodes.Contains(subdivisionCode))
        {
            return GeoDivisionKinds.SpecialAdministrativeRegion;
        }

        return GeoDivisionKinds.Standard;
    }
}
