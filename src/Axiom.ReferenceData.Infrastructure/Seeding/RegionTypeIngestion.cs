// 行政区类型推导（Infrastructure.Seeding）：GeoNames 同步入库时按国家标准区划代码归类，
// 与 geo_division 数据集的 type 口径保持一致。
namespace Axiom.ReferenceData.Infrastructure.Seeding;

public static class RegionTypeIngestion
{
    // 中国省级行政区划代码（GB/T 2260）中的直辖市、自治区与特别行政区；其余省级行政区归为省。
    private static readonly Dictionary<string, string> ChinaProvinceTypes = new(StringComparer.Ordinal)
    {
        ["11"] = "municipality",
        ["12"] = "municipality",
        ["31"] = "municipality",
        ["50"] = "municipality",
        ["15"] = "autonomous_region",
        ["45"] = "autonomous_region",
        ["54"] = "autonomous_region",
        ["64"] = "autonomous_region",
        ["65"] = "autonomous_region",
        ["81"] = "sar",
        ["82"] = "sar"
    };

    /// <summary>国家行类型。</summary>
    public static string ForCountry() => "country";

    /// <summary>推导一级行政区（depth 1）的类型；无特殊区划的国家归为省。</summary>
    public static string DeriveForProvince(string countryCode, string code)
    {
        if (countryCode.Equals("CN", StringComparison.OrdinalIgnoreCase)
            && ChinaProvinceTypes.TryGetValue(code, out var cnType))
        {
            return cnType;
        }

        return countryCode.Equals("US", StringComparison.OrdinalIgnoreCase) ? "state" : "province";
    }

    /// <summary>GeoNames 城市行（depth 2）的类型。</summary>
    public static string ForGeoNamesCity() => "city";
}
