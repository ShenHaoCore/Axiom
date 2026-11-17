// 开发 Mock 的进程内数据仓储：种子数据、可变集合与全局锁，跨 handler 实例共享（应用重启即重置）。
using Axiom.ReferenceData.Domain.Constants;
using Axiom.Web.Models;

namespace Axiom.Web.Mocking;

internal static class MockDataStore
{
    // 进程内示例数据（仅开发 Mock 用），应用重启即重置。
    internal static readonly object Gate = new();
    internal static readonly List<AirportResponse> Airports =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "PEK", "北京首都国际机场", "CN", "北京", 40.0801, 116.5846),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "JFK", "纽约肯尼迪国际机场", "US", "纽约", 40.6413, -73.7781),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "LHR", "伦敦希思罗机场", "GB", "伦敦", 51.4700, -0.4543),
        new(Guid.Parse("44444444-4444-4444-4444-444444444444"), "NRT", "东京成田国际机场", "JP", "东京", 35.7720, 140.3929)
    ];

    internal static readonly List<ReferenceEnumView> Enums =
    [
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000"), ReferenceEnumCategories.GeoDivisionKind, GeoDivisionKinds.Country, "国家", 1),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), ReferenceEnumCategories.GeoDivisionKind, GeoDivisionKinds.Province, "省", 2),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"), ReferenceEnumCategories.GeoDivisionKind, GeoDivisionKinds.PrefectureCity, "地级市", 3),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"), ReferenceEnumCategories.GeoDivisionKind, GeoDivisionKinds.County, "县", 4),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004"), ReferenceEnumCategories.GeoDivisionKind, GeoDivisionKinds.City, "市", 5),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000005"), ReferenceEnumCategories.GeoDivisionKind, GeoDivisionKinds.Other, "其他", 6),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000006"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.Province, "省", 2),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000008"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.Municipality, "直辖市", 3),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.AutonomousRegion, "自治区", 4),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000010"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.SpecialAdministrativeRegion, "特别行政区", 5),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000011"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.AutonomousPrefecture, "自治州", 6),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000012"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.PrefectureCity, "地级市", 7),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000013"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.Prefecture, "地区", 8),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000014"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.League, "盟", 9),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000015"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.CountyLevel, "省直辖县级行政区划", 10),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000016"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.County, "县", 11),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000017"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.CountyCity, "县级市", 12),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000018"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.District, "市辖区", 13),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000019"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.AutonomousCounty, "自治县", 14),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000020"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.Banner, "旗", 15),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000021"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.AutonomousBanner, "自治旗", 16),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000022"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.ForestryArea, "林区", 17),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000023"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.SpecialZone, "特区", 18),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000024"), GeoDivisionKinds.CategoryFor("CN"), GeoDivisionKinds.Other, "其他", 19),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000025"), GeoDivisionKinds.CategoryFor("US"), GeoDivisionKinds.State, "州", 2),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000027"), GeoDivisionKinds.CategoryFor("US"), GeoDivisionKinds.County, "县", 3),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000028"), GeoDivisionKinds.CategoryFor("US"), GeoDivisionKinds.Borough, "自治市镇", 4),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000029"), GeoDivisionKinds.CategoryFor("US"), GeoDivisionKinds.Parish, "教区", 5),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000030"), GeoDivisionKinds.CategoryFor("US"), GeoDivisionKinds.CensusArea, "人口普查区", 6),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000031"), GeoDivisionKinds.CategoryFor("US"), GeoDivisionKinds.City, "市", 7),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000032"), GeoDivisionKinds.CategoryFor("US"), GeoDivisionKinds.Other, "其他", 8),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000033"), GeoDivisionKinds.CategoryFor("GB"), GeoDivisionKinds.ConstituentCountry, "构成国", 2),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000035"), GeoDivisionKinds.CategoryFor("GB"), GeoDivisionKinds.City, "市", 3),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000036"), GeoDivisionKinds.CategoryFor("GB"), GeoDivisionKinds.Other, "其他", 4),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000037"), GeoDivisionKinds.CategoryFor("JP"), GeoDivisionKinds.Metropolis, "都", 2),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000039"), GeoDivisionKinds.CategoryFor("JP"), GeoDivisionKinds.Prefecture, "府", 3),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000040"), GeoDivisionKinds.CategoryFor("JP"), GeoDivisionKinds.SpecialWard, "特别区", 4),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000041"), GeoDivisionKinds.CategoryFor("JP"), GeoDivisionKinds.City, "市", 5),
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000042"), GeoDivisionKinds.CategoryFor("JP"), GeoDivisionKinds.Other, "其他", 6)
    ];

    // 机场响应模型不含并发令牌，因此在 Mock 中与主数据分开维护。
    internal static readonly Dictionary<Guid, Guid> AirportETags =
        Airports.ToDictionary(airport => airport.Id, _ => Guid.NewGuid());

    internal static readonly List<NewsArticleView> Articles =
    [
        new(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            "Axiom 平台发布 2026 春季更新",
            "本次更新包含行政区模块、国际化与开发 Mock 支持。",
            "Axiom 平台于本月发布 2026 春季更新，重点包括：全球行政区层级浏览、界面与业务数据的多语言支持，以及面向开发者的 Mock 鉴权模式。",
            "平台动态",
            "Published",
            "平台运营",
            new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero)),
        new(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
            "新机场数据接入指南",
            "介绍如何通过管理界面录入机场档案并维护多语言名称。",
            "本指南演示机场档案的录入流程：填写 IATA 代码、国家与城市坐标，保存后可在翻译页面补充英文名称，供多语言界面展示。",
            "接入指南",
            "Draft",
            "数据团队",
            null,
            new DateTimeOffset(2026, 11, 5, 14, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 5, 14, 30, 0, TimeSpan.Zero)),
        new(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"),
            "全球行政区数据集升级至 v2",
            "行政区数据集新增人口与地理坐标字段。",
            "行政区数据集升级至 v2：补齐了省州与市级节点的人口估算和经纬度坐标，并修正了部分行政代码映射。",
            "数据公告",
            "Published",
            "数据团队",
            new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero))
    ];

    internal static readonly List<GeoDivisionView> GeoDivisions = LoadGeoDivisions();

    // 行政区响应模型同样不含并发令牌，与主数据分开维护。
    internal static readonly Dictionary<Guid, Guid> GeoDivisionETags =
        GeoDivisions.ToDictionary(region => region.Id, _ => Guid.NewGuid());

    // 译文存储：key = "{kind}/{id}/{culture}"，kind ∈ airport | news | region | enum。
    internal static readonly Dictionary<string, Dictionary<string, string>> Translations = new();

    // Mock 枚举译文：Kind Code → 英文显示名，与 GeoDivisionKinds.EnglishNames 对齐。
    private static IReadOnlyDictionary<string, string> EnumEnglishNames => GeoDivisionKinds.EnglishNames;

    // 本地 Mock 没有数据库，预置英文译文，保证切换语言后能直接看到业务数据切换。
    static MockDataStore()
    {
        SeedTranslations();
    }

    private static void SeedTranslations()
    {
        const string culture = "en";

        void Set(string kind, Guid id, params (string Field, string Value)[] fields)
        {
            var key = $"{kind}/{id}/{culture}";
            if (!Translations.TryGetValue(key, out var values))
            {
                Translations[key] = values = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            foreach (var (field, value) in fields)
            {
                values[field] = value;
            }
        }

        Set("airport", Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ("Name", "Beijing Capital International Airport"), ("City", "Beijing"));
        Set("airport", Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ("Name", "John F. Kennedy International Airport"), ("City", "New York"));
        Set("airport", Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ("Name", "London Heathrow Airport"), ("City", "London"));
        Set("airport", Guid.Parse("44444444-4444-4444-4444-444444444444"),
            ("Name", "Narita International Airport"), ("City", "Tokyo"));

        // 行政区主数据已带 AsciiName，直接作为英文名，覆盖全部层级节点。
        foreach (var region in GeoDivisions)
        {
            if (!string.IsNullOrWhiteSpace(region.AsciiName))
            {
                Set("region", region.Id, ("Name", region.AsciiName));
            }
        }

        Set("news", Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            ("Title", "Axiom Platform 2026 Spring Release"),
            ("Summary", "This release adds the region module, internationalization and developer mock support."),
            ("Content", "Axiom shipped its 2026 spring release this month, covering region hierarchy browsing, multilingual UI and business data, and a mock authentication mode for developers."));
        Set("news", Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
            ("Title", "Airport Data Onboarding Guide"),
            ("Summary", "How to create airport records and maintain multilingual names."),
            ("Content", "This guide walks through airport onboarding: enter the IATA code, country and city coordinates, then add English names on the translation page for multilingual display."));
        Set("news", Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"),
            ("Title", "Global Region Dataset Upgraded to v2"),
            ("Summary", "The region dataset now includes population and geographic coordinates."),
            ("Content", "Region dataset v2 adds population estimates and coordinates for province and city nodes, and fixes several administrative code mappings."));

        // 枚举字典：按 Code 套英文显示名（与 GeoDivisionKinds.EnglishNames 对齐）。
        foreach (var item in Enums)
        {
            if (EnumEnglishNames.TryGetValue(item.Code, out var englishName))
            {
                Set("enum", item.Id, ("Name", englishName));
            }
        }
    }

    // 删除主数据时同步清理其全部语言的译文（调用方需持有 Gate 锁）。
    internal static void RemoveTranslations(string kind, Guid id)
    {
        var prefix = $"{kind}/{id}/";
        foreach (var key in Translations.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .ToList())
        {
            Translations.Remove(key);
        }
    }

    private static List<GeoDivisionView> LoadGeoDivisions()
    {
        Guid G(string tail) => Guid.Parse($"00000000-0000-0000-0000-{tail}");

        return
        [
            // 中国（34 个省级行政区 + 主要城市；直辖市/自治区/特别行政区按 Kind 区分，
            // 不再挂同名市级重复节点）
            new(G("a00000000001"), null, 1, GeoDivisionKinds.Country, "CN", null, "CN", "中国", "China", null, 35.0, 105.0, 1410000000, null),
            new(G("a00000000011"), G("a00000000001"), 2, GeoDivisionKinds.Municipality, "CN", "11", "110000", "北京市", "Beijing", null, 39.9, 116.4, 21890000, null),
            new(G("a00000000041"), G("a00000000001"), 2, GeoDivisionKinds.Municipality, "CN", "12", "120000", "天津市", "Tianjin", null, 39.1, 117.2, 13900000, null),
            new(G("a00000000031"), G("a00000000001"), 2, GeoDivisionKinds.Municipality, "CN", "31", "310000", "上海市", "Shanghai", null, 31.2, 121.5, 24800000, null),
            new(G("a00000000051"), G("a00000000001"), 2, GeoDivisionKinds.Municipality, "CN", "50", "500000", "重庆市", "Chongqing", null, 29.6, 106.5, 32100000, null),
            new(G("a00000000021"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "44", "440000", "广东省", "Guangdong", null, 23.4, 113.3, 126000000, null),
            new(G("a00000000022"), G("a00000000021"), 3, GeoDivisionKinds.PrefectureCity, "CN", "44", "440100", "广州市", "Guangzhou", null, 23.1, 113.3, 18700000, null),
            new(G("a00000000023"), G("a00000000021"), 3, GeoDivisionKinds.PrefectureCity, "CN", "44", "440300", "深圳市", "Shenzhen", null, 22.5, 114.1, 17600000, null),
            new(G("a00000000026"), G("a00000000021"), 3, GeoDivisionKinds.PrefectureCity, "CN", "44", "441900", "东莞市", "Dongguan", null, 23.0, 113.8, 10500000, null),
            new(G("a00000000024"), G("a00000000022"), 4, GeoDivisionKinds.District, "CN", "44", "440103", "荔湾区", "Liwan", null, 23.1, 113.2, 900000, null),
            new(G("a00000000025"), G("a00000000022"), 4, GeoDivisionKinds.District, "CN", "44", "440106", "天河区", "Tianhe", null, 23.1, 113.3, 2200000, null),
            new(G("a00000000101"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "13", "130000", "河北省", "Hebei", null, 38.0, 114.5, 74600000, null),
            new(G("a00000000111"), G("a00000000101"), 3, GeoDivisionKinds.PrefectureCity, "CN", "13", "130100", "石家庄市", "Shijiazhuang", null, 38.0, 114.5, 11200000, null),
            new(G("a00000000112"), G("a00000000101"), 3, GeoDivisionKinds.PrefectureCity, "CN", "13", "130200", "唐山市", "Tangshan", null, 39.6, 118.2, 7700000, null),
            new(G("a00000000121"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "14", "140000", "山西省", "Shanxi", null, 37.9, 112.5, 34900000, null),
            new(G("a00000000131"), G("a00000000121"), 3, GeoDivisionKinds.PrefectureCity, "CN", "14", "140100", "太原市", "Taiyuan", null, 37.9, 112.5, 5300000, null),
            new(G("a00000000141"), G("a00000000001"), 2, GeoDivisionKinds.AutonomousRegion, "CN", "15", "150000", "内蒙古自治区", "Inner Mongolia", null, 40.8, 111.7, 24000000, null),
            new(G("a00000000151"), G("a00000000141"), 3, GeoDivisionKinds.PrefectureCity, "CN", "15", "150100", "呼和浩特市", "Hohhot", null, 40.8, 111.7, 3400000, null),
            new(G("a00000000152"), G("a00000000141"), 3, GeoDivisionKinds.PrefectureCity, "CN", "15", "150200", "包头市", "Baotou", null, 40.7, 109.8, 2700000, null),
            new(G("a00000000061"), G("a00000000001"), 2, GeoDivisionKinds.AutonomousRegion, "CN", "45", "450000", "广西壮族自治区", "Guangxi", null, 22.8, 108.3, 50100000, null),
            new(G("a00000000062"), G("a00000000061"), 3, GeoDivisionKinds.PrefectureCity, "CN", "45", "450100", "南宁市", "Nanning", null, 22.8, 108.3, 8700000, null),
            new(G("a00000000161"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "21", "210000", "辽宁省", "Liaoning", null, 41.8, 123.4, 42600000, null),
            new(G("a00000000171"), G("a00000000161"), 3, GeoDivisionKinds.PrefectureCity, "CN", "21", "210100", "沈阳市", "Shenyang", null, 41.8, 123.4, 9100000, null),
            new(G("a00000000172"), G("a00000000161"), 3, GeoDivisionKinds.PrefectureCity, "CN", "21", "210200", "大连市", "Dalian", null, 38.9, 121.6, 7500000, null),
            new(G("a00000000181"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "22", "220000", "吉林省", "Jilin", null, 43.9, 125.3, 24100000, null),
            new(G("a00000000191"), G("a00000000181"), 3, GeoDivisionKinds.PrefectureCity, "CN", "22", "220100", "长春市", "Changchun", null, 43.9, 125.3, 9100000, null),
            new(G("a00000000201"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "23", "230000", "黑龙江省", "Heilongjiang", null, 45.8, 126.5, 31900000, null),
            new(G("a00000000211"), G("a00000000201"), 3, GeoDivisionKinds.PrefectureCity, "CN", "23", "230100", "哈尔滨市", "Harbin", null, 45.8, 126.5, 10100000, null),
            new(G("a00000000221"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "32", "320000", "江苏省", "Jiangsu", null, 32.1, 118.8, 84700000, null),
            new(G("a00000000231"), G("a00000000221"), 3, GeoDivisionKinds.PrefectureCity, "CN", "32", "320100", "南京市", "Nanjing", null, 32.1, 118.8, 9400000, null),
            new(G("a00000000232"), G("a00000000221"), 3, GeoDivisionKinds.PrefectureCity, "CN", "32", "320500", "苏州市", "Suzhou", null, 31.3, 120.6, 12700000, null),
            new(G("a00000000233"), G("a00000000221"), 3, GeoDivisionKinds.PrefectureCity, "CN", "32", "320200", "无锡市", "Wuxi", null, 31.5, 120.3, 7500000, null),
            new(G("a00000000241"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "33", "330000", "浙江省", "Zhejiang", null, 30.3, 120.2, 64600000, null),
            new(G("a00000000251"), G("a00000000241"), 3, GeoDivisionKinds.PrefectureCity, "CN", "33", "330100", "杭州市", "Hangzhou", null, 30.3, 120.2, 12200000, null),
            new(G("a00000000252"), G("a00000000241"), 3, GeoDivisionKinds.PrefectureCity, "CN", "33", "330200", "宁波市", "Ningbo", null, 29.9, 121.5, 9400000, null),
            new(G("a00000000261"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "34", "340000", "安徽省", "Anhui", null, 31.9, 117.3, 61000000, null),
            new(G("a00000000271"), G("a00000000261"), 3, GeoDivisionKinds.PrefectureCity, "CN", "34", "340100", "合肥市", "Hefei", null, 31.8, 117.2, 9400000, null),
            new(G("a00000000281"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "35", "350000", "福建省", "Fujian", null, 26.1, 119.3, 41500000, null),
            new(G("a00000000291"), G("a00000000281"), 3, GeoDivisionKinds.PrefectureCity, "CN", "35", "350100", "福州市", "Fuzhou", null, 26.1, 119.3, 8400000, null),
            new(G("a00000000292"), G("a00000000281"), 3, GeoDivisionKinds.PrefectureCity, "CN", "35", "350200", "厦门市", "Xiamen", null, 24.5, 118.1, 5200000, null),
            new(G("a00000000301"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "36", "360000", "江西省", "Jiangxi", null, 28.7, 115.9, 45200000, null),
            new(G("a00000000311"), G("a00000000301"), 3, GeoDivisionKinds.PrefectureCity, "CN", "36", "360100", "南昌市", "Nanchang", null, 28.7, 115.9, 6400000, null),
            new(G("a00000000321"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "37", "370000", "山东省", "Shandong", null, 36.7, 117.1, 101500000, null),
            new(G("a00000000331"), G("a00000000321"), 3, GeoDivisionKinds.PrefectureCity, "CN", "37", "370100", "济南市", "Jinan", null, 36.7, 117.1, 9200000, null),
            new(G("a00000000332"), G("a00000000321"), 3, GeoDivisionKinds.PrefectureCity, "CN", "37", "370200", "青岛市", "Qingdao", null, 36.1, 120.4, 10100000, null),
            new(G("a00000000341"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "41", "410000", "河南省", "Henan", null, 34.7, 113.6, 99400000, null),
            new(G("a00000000351"), G("a00000000341"), 3, GeoDivisionKinds.PrefectureCity, "CN", "41", "410100", "郑州市", "Zhengzhou", null, 34.7, 113.6, 12600000, null),
            new(G("a00000000361"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "42", "420000", "湖北省", "Hubei", null, 30.6, 114.3, 57700000, null),
            new(G("a00000000371"), G("a00000000361"), 3, GeoDivisionKinds.PrefectureCity, "CN", "42", "420100", "武汉市", "Wuhan", null, 30.6, 114.3, 12300000, null),
            new(G("a00000000381"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "43", "430000", "湖南省", "Hunan", null, 28.2, 112.9, 66400000, null),
            new(G("a00000000391"), G("a00000000381"), 3, GeoDivisionKinds.PrefectureCity, "CN", "43", "430100", "长沙市", "Changsha", null, 28.2, 112.9, 10000000, null),
            new(G("a00000000401"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "46", "460000", "海南省", "Hainan", null, 20.0, 110.3, 10100000, null),
            new(G("a00000000411"), G("a00000000401"), 3, GeoDivisionKinds.PrefectureCity, "CN", "46", "460100", "海口市", "Haikou", null, 20.0, 110.3, 2900000, null),
            new(G("a00000000421"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "51", "510000", "四川省", "Sichuan", null, 30.7, 104.1, 83700000, null),
            new(G("a00000000431"), G("a00000000421"), 3, GeoDivisionKinds.PrefectureCity, "CN", "51", "510100", "成都市", "Chengdu", null, 30.6, 104.1, 20900000, null),
            new(G("a00000000441"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "52", "520000", "贵州省", "Guizhou", null, 26.6, 106.6, 38600000, null),
            new(G("a00000000451"), G("a00000000441"), 3, GeoDivisionKinds.PrefectureCity, "CN", "52", "520100", "贵阳市", "Guiyang", null, 26.6, 106.6, 6000000, null),
            new(G("a00000000461"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "53", "530000", "云南省", "Yunnan", null, 25.0, 102.7, 47200000, null),
            new(G("a00000000471"), G("a00000000461"), 3, GeoDivisionKinds.PrefectureCity, "CN", "53", "530100", "昆明市", "Kunming", null, 24.9, 102.8, 8500000, null),
            new(G("a00000000481"), G("a00000000001"), 2, GeoDivisionKinds.AutonomousRegion, "CN", "54", "540000", "西藏自治区", "Tibet", null, 29.7, 91.1, 3600000, null),
            new(G("a00000000491"), G("a00000000481"), 3, GeoDivisionKinds.PrefectureCity, "CN", "54", "540100", "拉萨市", "Lhasa", null, 29.7, 91.1, 900000, null),
            new(G("a00000000501"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "61", "610000", "陕西省", "Shaanxi", null, 34.3, 108.9, 39500000, null),
            new(G("a00000000511"), G("a00000000501"), 3, GeoDivisionKinds.PrefectureCity, "CN", "61", "610100", "西安市", "Xi'an", null, 34.3, 108.9, 12900000, null),
            new(G("a00000000521"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "62", "620000", "甘肃省", "Gansu", null, 36.1, 103.8, 25000000, null),
            new(G("a00000000531"), G("a00000000521"), 3, GeoDivisionKinds.PrefectureCity, "CN", "62", "620100", "兰州市", "Lanzhou", null, 36.1, 103.8, 4400000, null),
            new(G("a00000000541"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "63", "630000", "青海省", "Qinghai", null, 36.6, 101.8, 5900000, null),
            new(G("a00000000551"), G("a00000000541"), 3, GeoDivisionKinds.PrefectureCity, "CN", "63", "630100", "西宁市", "Xining", null, 36.6, 101.8, 2500000, null),
            new(G("a00000000561"), G("a00000000001"), 2, GeoDivisionKinds.AutonomousRegion, "CN", "64", "640000", "宁夏回族自治区", "Ningxia", null, 38.5, 106.3, 7200000, null),
            new(G("a00000000571"), G("a00000000561"), 3, GeoDivisionKinds.PrefectureCity, "CN", "64", "640100", "银川市", "Yinchuan", null, 38.5, 106.3, 3000000, null),
            new(G("a00000000581"), G("a00000000001"), 2, GeoDivisionKinds.AutonomousRegion, "CN", "65", "650000", "新疆维吾尔自治区", "Xinjiang", null, 43.8, 87.6, 25900000, null),
            new(G("a00000000591"), G("a00000000581"), 3, GeoDivisionKinds.PrefectureCity, "CN", "65", "650100", "乌鲁木齐市", "Urumqi", null, 43.8, 87.6, 4100000, null),
            new(G("a00000000601"), G("a00000000001"), 2, GeoDivisionKinds.Province, "CN", "71", "710000", "台湾省", "Taiwan", null, 25.0, 121.5, 23500000, null),
            new(G("a00000000611"), G("a00000000601"), 3, GeoDivisionKinds.PrefectureCity, "CN", "71", "710100", "台北市", "Taipei", null, 25.0, 121.6, 2600000, null),
            new(G("a00000000071"), G("a00000000001"), 2, GeoDivisionKinds.SpecialAdministrativeRegion, "CN", "81", "810000", "香港特别行政区", "Hong Kong", null, 22.3, 114.2, 7500000, null),
            new(G("a00000000081"), G("a00000000001"), 2, GeoDivisionKinds.SpecialAdministrativeRegion, "CN", "82", "820000", "澳门特别行政区", "Macao", null, 22.2, 113.5, 700000, null),
            new(G("b00000000001"), null, 1, GeoDivisionKinds.Country, "US", null, "US", "美国", "United States", null, 39.8, -98.6, 331000000, null),
            new(G("b00000000011"), G("b00000000001"), 2, GeoDivisionKinds.State, "US", "06", "060000", "加利福尼亚州", "California", null, 36.8, -119.4, 39000000, null),
            new(G("b00000000012"), G("b00000000011"), 3, GeoDivisionKinds.County, "US", "06", "060400", "洛杉矶县", "Los Angeles", null, 34.1, -118.2, 9900000, null),
            new(G("b00000000021"), G("b00000000001"), 2, GeoDivisionKinds.State, "US", "48", "480000", "得克萨斯州", "Texas", null, 31.5, -99.3, 29100000, null),
            new(G("b00000000022"), G("b00000000021"), 3, GeoDivisionKinds.County, "US", "48", "482000", "哈里斯县", "Harris", null, 29.8, -95.4, 4700000, null),
            new(G("b00000000031"), G("b00000000001"), 2, GeoDivisionKinds.State, "US", "36", "360000", "纽约州", "New York", null, 42.9, -75.5, 19700000, null),
            new(G("b00000000032"), G("b00000000031"), 3, GeoDivisionKinds.County, "US", "36", "360400", "纽约县", "New York County", null, 40.8, -74.0, 1600000, null),
            new(G("c00000000001"), null, 1, GeoDivisionKinds.Country, "GB", null, "GB", "英国", "United Kingdom", null, 54.0, -2.0, 67000000, null),
            new(G("c00000000011"), G("c00000000001"), 2, GeoDivisionKinds.ConstituentCountry, "GB", "ENG", "ENG", "英格兰", "England", null, 52.4, -1.5, 56000000, null),
            new(G("c00000000012"), G("c00000000011"), 3, GeoDivisionKinds.City, "GB", "ENG", "LND", "伦敦市", "London", null, 51.5, -0.1, 8900000, null),
            new(G("c00000000021"), G("c00000000001"), 2, GeoDivisionKinds.ConstituentCountry, "GB", "SCT", "SCT", "苏格兰", "Scotland", null, 56.8, -4.2, 5500000, null),
            new(G("c00000000022"), G("c00000000021"), 3, GeoDivisionKinds.City, "GB", "SCT", "EDH", "爱丁堡", "Edinburgh", null, 56.0, -3.2, 550000, null),
            new(G("c00000000031"), G("c00000000001"), 2, GeoDivisionKinds.ConstituentCountry, "GB", "WLS", "WLS", "威尔士", "Wales", null, 52.1, -3.8, 3100000, null),
            new(G("c00000000032"), G("c00000000031"), 3, GeoDivisionKinds.City, "GB", "WLS", "CDF", "加的夫", "Cardiff", null, 51.5, -3.2, 370000, null),
            new(G("d00000000001"), null, 1, GeoDivisionKinds.Country, "JP", null, "JP", "日本", "Japan", null, 36.2, 138.3, 125000000, null),
            new(G("d00000000011"), G("d00000000001"), 2, GeoDivisionKinds.Prefecture, "JP", "13", "130000", "东京都", "Tokyo", null, 35.7, 139.7, 14000000, null),
            new(G("d00000000012"), G("d00000000011"), 3, GeoDivisionKinds.SpecialWard, "JP", "13", "130010", "东京特别区", "Tokyo Special Wards", null, 35.7, 139.7, 9700000, null),
            new(G("d00000000021"), G("d00000000001"), 2, GeoDivisionKinds.Prefecture, "JP", "27", "270000", "大阪府", "Osaka", null, 34.7, 135.5, 8800000, null),
            new(G("d00000000022"), G("d00000000021"), 3, GeoDivisionKinds.City, "JP", "27", "271000", "大阪市", "Osaka City", null, 34.7, 135.5, 2700000, null),
            new(G("d00000000031"), G("d00000000001"), 2, GeoDivisionKinds.Prefecture, "JP", "26", "260000", "京都府", "Kyoto", null, 35.0, 135.8, 2500000, null),
            new(G("d00000000032"), G("d00000000031"), 3, GeoDivisionKinds.City, "JP", "26", "261000", "京都市", "Kyoto City", null, 35.0, 135.8, 1500000, null)
        ];
    }
}
