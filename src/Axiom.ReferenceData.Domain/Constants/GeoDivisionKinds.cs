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

    /// <summary>Kind 字典按国家分域：geo_division.kind.{country}，另留 geo_division.kind 作通用兜底。</summary>
    public static string CategoryFor(string? countryCode)
    {
        var normalized = (countryCode ?? string.Empty).Trim();
        return normalized.Length == 2 && normalized.All(char.IsAsciiLetter)
            ? $"{ReferenceEnumCategories.GeoDivisionKind}.{normalized.ToLowerInvariant()}"
            : ReferenceEnumCategories.GeoDivisionKind;
    }

    /// <summary>
    /// 种子项（Category, Code, 显示名, 排序）：通用兜底域 + 各国专属域，
    /// 显示名由枚举字典表维护。
    /// </summary>
    public static IReadOnlyList<(string Category, string Code, string Name, int SortOrder)> SeedItems { get; } =
        BuildSeedItems();

    /// <summary>Kind Code → 英文显示名，供 reference_enum_translations(en) 种子使用。</summary>
    public static IReadOnlyDictionary<string, string> EnglishNames { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Country] = "Country",
        [Province] = "Province",
        [Municipality] = "Municipality",
        [AutonomousRegion] = "Autonomous Region",
        [SpecialAdministrativeRegion] = "Special Administrative Region",
        [AutonomousPrefecture] = "Autonomous Prefecture",
        [PrefectureCity] = "Prefecture-level City",
        [Prefecture] = "Prefecture",
        [League] = "League",
        [CountyLevel] = "Province-administered County-level Division",
        [County] = "County",
        [CountyCity] = "County-level City",
        [District] = "District",
        [AutonomousCounty] = "Autonomous County",
        [Banner] = "Banner",
        [AutonomousBanner] = "Autonomous Banner",
        [ForestryArea] = "Forestry Area",
        [SpecialZone] = "Special Zone",
        [State] = "State",
        [Borough] = "Borough",
        [Parish] = "Parish",
        [CensusArea] = "Census Area",
        [City] = "City",
        [ConstituentCountry] = "Constituent Country",
        [Metropolis] = "Metropolis",
        [SpecialWard] = "Special Ward",
        [Other] = "Other"
    };

    private static List<(string Category, string Code, string Name, int SortOrder)> BuildSeedItems()
    {
        // 通用兜底：未维护国别类型的国家使用
        var baseKinds = new[] { (Country, "国家"), (Province, "省"), (PrefectureCity, "地级市"), (County, "县"), (City, "市"), (Other, "其他") };

        // 中国：全量行政区划类型
        var cn = new[]
        {
            (Country, "国家"), (Province, "省"), (Municipality, "直辖市"), (AutonomousRegion, "自治区"),
            (SpecialAdministrativeRegion, "特别行政区"), (AutonomousPrefecture, "自治州"), (PrefectureCity, "地级市"),
            (Prefecture, "地区"), (League, "盟"), (CountyLevel, "省直辖县级行政区划"), (County, "县"),
            (CountyCity, "县级市"), (District, "市辖区"), (AutonomousCounty, "自治县"), (Banner, "旗"),
            (AutonomousBanner, "自治旗"), (ForestryArea, "林区"), (SpecialZone, "特区"), (Other, "其他")
        };

        // 美国
        var us = new[]
        {
            (Country, "国家"), (State, "州"), (County, "县"), (Borough, "自治市镇"),
            (Parish, "教区"), (CensusArea, "人口普查区"), (City, "市"), (Other, "其他")
        };

        // 英国
        var gb = new[] { (Country, "国家"), (ConstituentCountry, "构成国"), (City, "市"), (Other, "其他") };

        // 日本
        var jp = new[]
        {
            (Country, "国家"), (Metropolis, "都"), (Prefecture, "府"), (SpecialWard, "特别区"),
            (City, "市"), (Other, "其他")
        };

        var items = new List<(string, string, string, int)>();
        var sort = 0;

        void Add(string category, IEnumerable<(string Code, string Name)> kinds)
        {
            foreach (var (code, name) in kinds)
            {
                items.Add((category, code, name, sort++));
            }
        }

        Add(ReferenceEnumCategories.GeoDivisionKind, baseKinds);
        Add(CategoryFor("CN"), cn);
        Add(CategoryFor("US"), us);
        Add(CategoryFor("GB"), gb);
        Add(CategoryFor("JP"), jp);

        return items;
    }

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
