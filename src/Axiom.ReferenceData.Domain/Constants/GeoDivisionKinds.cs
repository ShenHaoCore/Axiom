// 行政区 Kind 种子 code：须先写入 reference_enums（Category = geo_division.kind）。
// 口径对齐 geo_division 数据集：按国家给出全部机器可读类型，不存在"标准"这一种。
namespace Axiom.ReferenceData.Domain.Constants;

public static class GeoDivisionKinds
{
    public const int MaxLength = 64;

    // 国家行
    public const string Country = "country";

    // 中国
    public const string Province = "province";
    public const string Municipality = "municipality";
    public const string AutonomousRegion = "autonomous_region";
    public const string SpecialAdministrativeRegion = "special_administrative_region";
    public const string AutonomousPrefecture = "autonomous_prefecture";
    public const string PrefectureCity = "prefecture_city";
    public const string Prefecture = "prefecture";
    public const string League = "league";
    public const string CountyLevel = "county_level";
    public const string County = "county";
    public const string CountyCity = "county_city";
    public const string District = "district";
    public const string AutonomousCounty = "autonomous_county";
    public const string Banner = "banner";
    public const string AutonomousBanner = "autonomous_banner";
    public const string ForestryArea = "forestry_area";
    public const string SpecialZone = "special_zone";

    // 美国
    public const string State = "state";
    public const string Borough = "borough";
    public const string Parish = "parish";
    public const string CensusArea = "census_area";

    // 美/英等通用的城市
    public const string City = "city";

    // 英国
    public const string ConstituentCountry = "constituent_country";

    // 日本
    public const string Metropolis = "metropolis";
    public const string SpecialWard = "special_ward";

    // 兜底
    public const string Other = "other";

    /// <summary>种子与默认下拉用的约定项（显示名由字典表维护）。</summary>
    public static IReadOnlyList<(string Code, string Name, int SortOrder)> SeedItems { get; } =
    [
        (Country, "国家", 0),

        // 中国
        (Province, "省", 10),
        (Municipality, "直辖市", 11),
        (AutonomousRegion, "自治区", 12),
        (SpecialAdministrativeRegion, "特别行政区", 13),
        (AutonomousPrefecture, "自治州", 14),
        (PrefectureCity, "地级市", 15),
        (Prefecture, "地区", 16),
        (League, "盟", 17),
        (CountyLevel, "省直辖县级行政区划", 18),
        (County, "县", 19),
        (CountyCity, "县级市", 20),
        (District, "市辖区", 21),
        (AutonomousCounty, "自治县", 22),
        (Banner, "旗", 23),
        (AutonomousBanner, "自治旗", 24),
        (ForestryArea, "林区", 25),
        (SpecialZone, "特区", 26),
        (Other, "其他", 27),

        // 美国
        (State, "州", 40),
        (County, "县", 41),
        (Borough, "自治市镇", 42),
        (Parish, "教区", 43),
        (CensusArea, "人口普查区", 44),
        (City, "市", 45),

        // 英国
        (ConstituentCountry, "构成国", 60),

        // 日本
        (Metropolis, "都", 80),
        (SpecialWard, "特别区", 81)
    ];

    /// <summary>规范化 Kind 输入：小写、去空白；空值兜底为 other。</summary>
    public static string Normalize(string? kind) =>
        string.IsNullOrWhiteSpace(kind) ? Other : kind.Trim().ToLowerInvariant();

    /// <summary>
    /// 按国家与深度推导默认 Kind（输入未提供 Kind 时使用）。
    /// 中国按 GB/T 区划代码识别直辖市/自治区/特别行政区，其余归为省。
    /// </summary>
    public static string DeriveDefault(string countryCode, int level, string? subdivisionCode)
    {
        if (level == 1)
        {
            return Country;
        }

        if (countryCode.Equals("CN", StringComparison.OrdinalIgnoreCase))
        {
            if (level == 2)
            {
                return subdivisionCode switch
                {
                    "11" or "12" or "31" or "50" => Municipality,
                    "15" or "45" or "54" or "64" or "65" => AutonomousRegion,
                    "81" or "82" => SpecialAdministrativeRegion,
                    _ => Province
                };
            }

            return level == 3 ? PrefectureCity : District;
        }

        if (countryCode.Equals("US", StringComparison.OrdinalIgnoreCase))
        {
            return level == 2 ? State : County;
        }

        if (countryCode.Equals("GB", StringComparison.OrdinalIgnoreCase))
        {
            return level == 2 ? ConstituentCountry : City;
        }

        if (countryCode.Equals("JP", StringComparison.OrdinalIgnoreCase))
        {
            if (level == 2)
            {
                return Prefecture;
            }

            return subdivisionCode == "13" ? SpecialWard : City;
        }

        return level == 2 ? Province : Other;
    }
}
