// 仅开发 Mock 模式启用：拦截常用网关请求并维护进程内示例数据，
// 使机场、新闻、行政区增删改与翻译读写在 Mock 下形成完整交互闭环。
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Axiom.SharedKernel.Localization;
using Axiom.SharedKernel.Models;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Axiom.Web.Mocking;

public sealed class MockGatewayHandler : DelegatingHandler
{
    // 与真实网关一致的语言协商规则，供列表/详情套用已保存的译文。
    private readonly AxiomLocalizationOptions localization;

    public MockGatewayHandler(AxiomLocalizationOptions localization)
    {
        this.localization = localization;
    }

    // 路径与各网关客户端共享同一常量，避免双份维护。
    private static readonly string AIRPORTS_PATH = ReferenceDataClient.AIRPORTS_PATH;
    private static readonly string ARTICLES_PATH = NewsClient.ARTICLES_PATH;
    private static readonly string REGIONS_PATH = RegionClient.REGIONS_PATH;

    // 进程内示例数据（仅开发 Mock 用），应用重启即重置。
    private static readonly object Gate = new();
    private static readonly List<AirportResponse> Airports =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "PEK", "北京首都国际机场", "CN", "北京", 40.0801, 116.5846),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "JFK", "纽约肯尼迪国际机场", "US", "纽约", 40.6413, -73.7781),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "LHR", "伦敦希思罗机场", "GB", "伦敦", 51.4700, -0.4543),
        new(Guid.Parse("44444444-4444-4444-4444-444444444444"), "NRT", "东京成田国际机场", "JP", "东京", 35.7720, 140.3929)
    ];

    // 机场响应模型不含并发令牌，因此在 Mock 中与主数据分开维护。
    private static readonly Dictionary<Guid, Guid> AirportETags =
        Airports.ToDictionary(airport => airport.Id, _ => Guid.NewGuid());

    private static readonly List<NewsArticleView> Articles =
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

    private static readonly List<RegionView> Regions = LoadRegions();

    // 译文存储：key = "{kind}/{id}/{culture}"，kind ∈ airport | news | region。
    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new();

    // 本地 Mock 没有数据库，预置英文译文，保证切换语言后能直接看到业务数据切换。
    static MockGatewayHandler()
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
        foreach (var region in Regions)
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
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath.TrimEnd('/');
        if (path is not null)
        {
            if (path.StartsWith(AIRPORTS_PATH, StringComparison.OrdinalIgnoreCase))
            {
                return await HandleAirportsAsync(request, path, cancellationToken);
            }

            if (path.StartsWith(ARTICLES_PATH, StringComparison.OrdinalIgnoreCase))
            {
                return await HandleNewsAsync(request, path, cancellationToken);
            }

            if (path.StartsWith(REGIONS_PATH, StringComparison.OrdinalIgnoreCase))
            {
                return await HandleRegionsAsync(request, path, cancellationToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    // ---------- 机场 ----------

    private async Task<HttpResponseMessage> HandleAirportsAsync(
        HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var rest = path[AIRPORTS_PATH.Length..].Trim('/');

        if (rest.Length == 0)
        {
            if (request.Method == HttpMethod.Get)
            {
                var requestCulture = ResolveCulture(request);
                List<AirportResponse> snapshot;
                lock (Gate) { snapshot = [.. Airports]; }
                return Json(Paged(snapshot.Select(airport => Localize(airport, requestCulture))), HttpStatusCode.OK);
            }

            if (request.Method == HttpMethod.Post)
            {
                using var body = await ReadJsonAsync(request, cancellationToken);
                var code = Body(body, "code");
                if (string.IsNullOrWhiteSpace(code))
                {
                    return Empty(HttpStatusCode.BadRequest);
                }

                lock (Gate)
                {
                    if (Airports.Any(a => string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase)))
                    {
                        return Problem(HttpStatusCode.Conflict, "机场代码已存在。");
                    }

                    var created = new AirportResponse(
                        Guid.NewGuid(),
                        code.ToUpperInvariant(),
                        Body(body, "name") ?? code,
                        Body(body, "countryCode") ?? "CN",
                        Body(body, "city") ?? string.Empty,
                        Number(body, "latitude"),
                        Number(body, "longitude"));
                    Airports.Add(created);
                    AirportETags[created.Id] = Guid.NewGuid();
                    return Json(created, HttpStatusCode.Created, AirportETags[created.Id].ToString("N"));
                }
            }
        }

        if (TryParseSubResource(rest, out var id, out var culture))
        {
            if (culture is not null)
            {
                return await HandleTranslationAsync(request, $"airport/{id}/{culture}", culture, cancellationToken);
            }

            if (request.Method == HttpMethod.Get)
            {
                var requestCulture = ResolveCulture(request);
                lock (Gate)
                {
                    var airport = Airports.FirstOrDefault(a => a.Id == id);
                    return airport is null
                        ? Problem(HttpStatusCode.NotFound, "未找到指定机场。")
                        : Json(Localize(airport, requestCulture), HttpStatusCode.OK, AirportETags[id].ToString("N"));
                }
            }

            if (request.Method == HttpMethod.Put || request.Method == HttpMethod.Delete)
            {
                if (!TryParseIfMatch(request, out var expectedETag, out var error))
                {
                    return error!;
                }

                // PUT 请求体需在 lock 外读取，避免在 lock 语句体内 await。
                using var putBody = request.Method == HttpMethod.Put
                    ? await ReadJsonAsync(request, cancellationToken)
                    : null;

                lock (Gate)
                {
                    var index = Airports.FindIndex(a => a.Id == id);
                    if (index < 0)
                    {
                        return Problem(HttpStatusCode.NotFound, "未找到指定机场。");
                    }

                    if (AirportETags[id] != expectedETag)
                    {
                        return Problem(HttpStatusCode.Conflict, "机场数据已被其他操作修改，请刷新后重试。");
                    }

                    if (request.Method == HttpMethod.Put)
                    {
                        var original = Airports[index];
                        var code = Body(putBody!, "code") ?? original.Code;
                        if (string.IsNullOrWhiteSpace(code))
                        {
                            return Empty(HttpStatusCode.BadRequest);
                        }

                        if (Airports.Any(a => a.Id != id
                            && string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase)))
                        {
                            return Problem(HttpStatusCode.Conflict, "机场代码已存在。");
                        }

                        var updated = new AirportResponse(
                            id,
                            code.ToUpperInvariant(),
                            Body(putBody!, "name") ?? original.Name,
                            Body(putBody!, "countryCode") ?? original.CountryCode,
                            Body(putBody!, "city"),
                            Number(putBody!, "latitude"),
                            Number(putBody!, "longitude"));
                        Airports[index] = updated;
                        AirportETags[id] = Guid.NewGuid();
                        return Json(updated, HttpStatusCode.OK, AirportETags[id].ToString("N"));
                    }

                    Airports.RemoveAt(index);
                    AirportETags.Remove(id);
                    RemoveTranslations("airport", id);
                    return Empty(HttpStatusCode.NoContent);
                }
            }
        }

        // 机场写请求全部由 Mock 接管，未知路径也不回落真实网关。
        return Empty(HttpStatusCode.NotFound);
    }

    // ---------- 新闻 ----------

    private async Task<HttpResponseMessage> HandleNewsAsync(
        HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var rest = path[ARTICLES_PATH.Length..].Trim('/');

        if (rest.Length == 0)
        {
            if (request.Method == HttpMethod.Get)
            {
                var requestCulture = ResolveCulture(request);
                List<NewsArticleView> snapshot;
                lock (Gate) { snapshot = [.. Articles]; }
                return Json(Paged(snapshot.Select(article => Localize(article, requestCulture))), HttpStatusCode.OK);
            }

            if (request.Method == HttpMethod.Post)
            {
                using var body = await ReadJsonAsync(request, cancellationToken);
                var now = DateTimeOffset.UtcNow;
                var status = Body(body, "status") ?? "Draft";
                var created = new NewsArticleView(
                    Guid.NewGuid(),
                    Body(body, "title") ?? "（无标题）",
                    Body(body, "summary"),
                    Body(body, "content") ?? string.Empty,
                    Body(body, "category") ?? string.Empty,
                    status,
                    Body(body, "author") ?? string.Empty,
                    string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase) ? now : null,
                    now,
                    now);
                lock (Gate) { Articles.Add(created); }
                return Json(created, HttpStatusCode.Created);
            }
        }

        if (TryParseSubResource(rest, out var id, out var culture))
        {
            if (culture is not null)
            {
                return await HandleTranslationAsync(request, $"news/{id}/{culture}", culture, cancellationToken);
            }

            // PUT 请求体需在 lock 外读取，避免在 lock 语句体内 await。
            using var putBody = request.Method == HttpMethod.Put
                ? await ReadJsonAsync(request, cancellationToken)
                : null;

            lock (Gate)
            {
                var index = Articles.FindIndex(a => a.Id == id);
                if (request.Method == HttpMethod.Get)
                {
                    var requestCulture = ResolveCulture(request);
                    var article = index < 0 ? SampleArticle(id) : Articles[index];
                    return Json(Localize(article, requestCulture), HttpStatusCode.OK, etag: $"mock-{id.ToString("N")[..8]}");
                }

                if (request.Method == HttpMethod.Put)
                {
                    var body = putBody!;
                    var original = index >= 0 ? Articles[index] : SampleArticle(id);
                    var status = Body(body, "status") ?? original.Status;
                    var updated = new NewsArticleView(
                        id,
                        Body(body, "title") ?? original.Title,
                        Body(body, "summary"),
                        Body(body, "content") ?? original.Content,
                        Body(body, "category") ?? original.Category,
                        status,
                        Body(body, "author") ?? original.Author,
                        string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase)
                            ? (original.PublishedAt ?? DateTimeOffset.UtcNow)
                            : original.PublishedAt,
                        original.CreatedAt,
                        DateTimeOffset.UtcNow);

                    if (index < 0)
                    {
                        Articles.Add(updated);
                    }
                    else
                    {
                        Articles[index] = updated;
                    }

                    return Empty(HttpStatusCode.NoContent);
                }

                if (request.Method == HttpMethod.Delete)
                {
                    if (index >= 0)
                    {
                        Articles.RemoveAt(index);
                    }

                    return Empty(HttpStatusCode.NoContent);
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static NewsArticleView SampleArticle(Guid id) => new(
        id,
        "示例新闻标题",
        "Mock 模式返回的示例摘要。",
        "这是开发 Mock 模式生成的示例正文，用于本地预览新闻详情与编辑页面。",
        "平台动态",
        "Draft",
        "开发用户",
        null,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);

    // ---------- 行政区 ----------

    private async Task<HttpResponseMessage> HandleRegionsAsync(
        HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var rest = path[REGIONS_PATH.Length..].Trim('/');

        if (rest.Length == 0)
        {
            if (request.Method == HttpMethod.Get)
            {
                var query = ParseQuery(request.RequestUri?.Query);
                var requestCulture = ResolveCulture(request);
                var level = int.TryParse(query.GetValueOrDefault("level"), out var lv) ? lv : (int?)null;
                var country = query.GetValueOrDefault("countryCode");
                var keyword = query.GetValueOrDefault("q");

                List<RegionView> matches;
                lock (Gate)
                {
                    matches = Regions
                        .Where(r => !level.HasValue || r.Level == level.Value)
                        .Where(r => string.IsNullOrWhiteSpace(country)
                            || string.Equals(r.CountryCode, country, StringComparison.OrdinalIgnoreCase))
                        .Where(r => string.IsNullOrWhiteSpace(keyword)
                            || r.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(r => r.CountryCode).ThenBy(r => r.Code)
                        .ToList();
                }

                return Json(Paged(matches.Select(region => Localize(region, requestCulture))), HttpStatusCode.OK);
            }

            if (request.Method == HttpMethod.Post)
            {
                using var body = await ReadJsonAsync(request, cancellationToken);
                var countryCode = Text(body, "countryCode")?.ToUpperInvariant();
                var name = Text(body, "name");
                if (!TryParseRegionLevel(Text(body, "level"), out var level)
                    || countryCode is null || name is null
                    || !TryReadParentId(body, out var parentId))
                {
                    return Problem(HttpStatusCode.BadRequest, "行政区创建请求无效。");
                }

                var created = new RegionView(
                    Guid.NewGuid(),
                    level == 1 ? null : parentId,
                    level,
                    countryCode,
                    Text(body, "admin1Code"),
                    Text(body, "code")?.ToUpperInvariant() ?? name,
                    name,
                    Text(body, "asciiName"),
                    Text(body, "localName"),
                    Number(body, "latitude"),
                    Number(body, "longitude"),
                    Integer(body, "population"),
                    null);

                lock (Gate)
                {
                    if (RegionKeyExists(null, created.Level, created.CountryCode, created.Code))
                    {
                        return Problem(HttpStatusCode.Conflict, "行政区唯一键已存在。");
                    }

                    Regions.Add(created);
                    return Json(created, HttpStatusCode.Created);
                }
            }
        }

        if (rest.Equals("tree", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
        {
            var country = ParseQuery(request.RequestUri?.Query).GetValueOrDefault("countryCode") ?? "CN";
            return Json(Localize(BuildTree(country), ResolveCulture(request)), HttpStatusCode.OK);
        }

        // 单级子级：{id}/children，供行政区树按级惰性加载。
        if (rest.EndsWith("/children", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
        {
            var idPart = rest[..^"/children".Length];
            if (!Guid.TryParse(idPart, out var parentId))
            {
                return Problem(HttpStatusCode.BadRequest, "行政区子级请求无效。");
            }

            List<RegionView> snapshot;
            lock (Gate) { snapshot = [.. Regions]; }

            var parent = snapshot.FirstOrDefault(r => r.Id == parentId);
            if (parent is null)
            {
                return Problem(HttpStatusCode.NotFound, "未找到指定行政区。");
            }

            var children = snapshot
                .Where(r => r.ParentId == parentId)
                .OrderBy(r => r.Code)
                .ToList();
            var requestCulture = ResolveCulture(request);
            var items = children.Select(child =>
            {
                var grandchildren = snapshot.Where(r => r.ParentId == child.Id).ToList();
                return new
                {
                    child.Id,
                    child.Level,
                    child.CountryCode,
                    child.Admin1Code,
                    child.Code,
                    Name = Localize(child, requestCulture).Name,
                    child.Population,
                    childCount = grandchildren.Count,
                    // 同名下级数量：直辖市（省、市同名）在展示时合并，前端据此识别。
                    sameNameChildCount = grandchildren.Count(g =>
                        string.Equals(g.Name?.Trim(), child.Name?.Trim(), StringComparison.Ordinal))
                };
            }).ToList();

            return Json(items, HttpStatusCode.OK);
        }

        if (TryParseSubResource(rest, out var id, out var culture))
        {
            if (culture is not null)
            {
                return await HandleTranslationAsync(request, $"region/{id}/{culture}", culture, cancellationToken);
            }

            if (request.Method == HttpMethod.Get)
            {
                var requestCulture = ResolveCulture(request);
                lock (Gate)
                {
                    var region = Regions.FirstOrDefault(r => r.Id == id);
                    return region is null
                        ? Problem(HttpStatusCode.NotFound, "未找到指定行政区。")
                        : Json(Localize(region, requestCulture), HttpStatusCode.OK);
                }
            }

            if (request.Method == HttpMethod.Put)
            {
                using var body = await ReadJsonAsync(request, cancellationToken);
                if (!TryReadParentId(body, out var parentId))
                {
                    return Problem(HttpStatusCode.BadRequest, "行政区更新请求无效。");
                }

                lock (Gate)
                {
                    var index = Regions.FindIndex(r => r.Id == id);
                    if (index < 0)
                    {
                        return Problem(HttpStatusCode.NotFound, "未找到指定行政区。");
                    }

                    var original = Regions[index];
                    var updated = new RegionView(
                        id,
                        original.Level == 1 ? null : parentId ?? original.ParentId,
                        original.Level,
                        original.CountryCode,
                        original.Admin1Code,
                        Text(body, "code")?.ToUpperInvariant() ?? original.Code,
                        Text(body, "name") ?? original.Name,
                        Text(body, "asciiName") ?? original.AsciiName,
                        Text(body, "localName") ?? original.LocalName,
                        Number(body, "latitude") ?? original.Latitude,
                        Number(body, "longitude") ?? original.Longitude,
                        Integer(body, "population") ?? original.Population,
                        original.GeoNameId);

                    if (RegionKeyExists(id, updated.Level, updated.CountryCode, updated.Code))
                    {
                        return Problem(HttpStatusCode.Conflict, "行政区唯一键已存在。");
                    }

                    Regions[index] = updated;
                    return Json(updated, HttpStatusCode.OK);
                }
            }

            if (request.Method == HttpMethod.Delete)
            {
                lock (Gate)
                {
                    var index = Regions.FindIndex(r => r.Id == id);
                    if (index < 0)
                    {
                        return Problem(HttpStatusCode.NotFound, "未找到指定行政区。");
                    }

                    if (Regions.Any(r => r.ParentId == id))
                    {
                        return Problem(HttpStatusCode.BadRequest, "行政区仍有子节点，不能删除。");
                    }

                    Regions.RemoveAt(index);
                    RemoveTranslations("region", id);
                    return Empty(HttpStatusCode.NoContent);
                }
            }
        }

        // 行政区写请求全部由 Mock 接管，未知路径也不回落真实网关。
        return Empty(HttpStatusCode.NotFound);
    }

    private static bool TryParseRegionLevel(string? value, out int level)
    {
        if (string.Equals(value, "Country", StringComparison.OrdinalIgnoreCase))
        {
            level = 1;
            return true;
        }

        if (string.Equals(value, "Admin1", StringComparison.OrdinalIgnoreCase))
        {
            level = 2;
            return true;
        }

        if (string.Equals(value, "City", StringComparison.OrdinalIgnoreCase))
        {
            level = 3;
            return true;
        }

        level = default;
        return false;
    }

    private static bool TryReadParentId(JsonDocument document, out Guid? parentId)
    {
        parentId = null;
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("parentId", out var element)
            || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String
            && Guid.TryParse(element.GetString(), out var parsed))
        {
            parentId = parsed;
            return true;
        }

        return false;
    }

    private static bool RegionKeyExists(Guid? excludedId, int level, string countryCode, string code) =>
        Regions.Any(region => (!excludedId.HasValue || region.Id != excludedId.Value)
            && region.Level == level
            && string.Equals(region.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(region.Code, code, StringComparison.OrdinalIgnoreCase));

    private static RegionTreeView BuildTree(string countryCode)
    {
        lock (Gate)
        {
            var root = Regions.FirstOrDefault(r => r.Level == 1
                && string.Equals(r.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
            if (root is null)
            {
                return new RegionTreeView(Guid.Empty, 1, countryCode, null, countryCode, countryCode, null, []);
            }

            var children = Regions
                .Where(r => r.Level == 2 && r.ParentId == root.Id)
                .OrderBy(r => r.Code)
                .Select(child => new RegionTreeView(
                    child.Id,
                    child.Level,
                    child.CountryCode,
                    child.Admin1Code,
                    child.Code,
                    child.Name,
                    child.Population,
                    Regions
                        .Where(r => r.Level == 3 && r.ParentId == child.Id)
                        .OrderBy(r => r.Code)
                        .Select(grand => new RegionTreeView(
                            grand.Id, grand.Level, grand.CountryCode, grand.Admin1Code,
                            grand.Code, grand.Name, grand.Population, []))
                        .ToList()))
                .ToList();

            return new RegionTreeView(
                root.Id, root.Level, root.CountryCode, root.Admin1Code,
                root.Code, root.Name, root.Population, children);
        }
    }

    // 行政区种子：中国 34 个省级行政区 + 主要城市，美/英/日示例省州。
    // 直辖市（北京/上海/天津/重庆）按 GeoNames 模式存省、市两条同名记录，
    // 前端层级树据 sameNameChildCount 合并展示为「直辖市」。
    private static List<RegionView> LoadRegions()
    {
        Guid G(string tail) => Guid.Parse($"00000000-0000-0000-0000-{tail}");

        return
        [
            // 中国：国家
            new(G("a00000000001"), null, 1, "CN", null, "CN", "中国", "China", null, 35.0, 105.0, 1_410_000_000, null),
            // 直辖市（省、市同名两条记录）
            new(G("a00000000011"), G("a00000000001"), 2, "CN", "11", "110000", "北京市", "Beijing", null, 39.9, 116.4, 21_890_000, null),
            new(G("a00000000012"), G("a00000000011"), 3, "CN", "11", "110100", "北京市", "Beijing", null, 39.9, 116.4, 21_890_000, null),
            new(G("a00000000031"), G("a00000000001"), 2, "CN", "31", "310000", "上海市", "Shanghai", null, 31.2, 121.5, 24_800_000, null),
            new(G("a00000000032"), G("a00000000031"), 3, "CN", "31", "310100", "上海市", "Shanghai", null, 31.2, 121.5, 24_800_000, null),
            new(G("a00000000041"), G("a00000000001"), 2, "CN", "12", "120000", "天津市", "Tianjin", null, 39.1, 117.2, 13_900_000, null),
            new(G("a00000000042"), G("a00000000041"), 3, "CN", "12", "120100", "天津市", "Tianjin", null, 39.1, 117.2, 13_900_000, null),
            new(G("a00000000051"), G("a00000000001"), 2, "CN", "50", "500000", "重庆市", "Chongqing", null, 29.6, 106.5, 32_100_000, null),
            new(G("a00000000052"), G("a00000000051"), 3, "CN", "50", "500100", "重庆市", "Chongqing", null, 29.6, 106.5, 32_100_000, null),
            // 省（每省按 10 个序号一段：省级一段首，城市随后）
            new(G("a00000000021"), G("a00000000001"), 2, "CN", "44", "440000", "广东省", "Guangdong", null, 23.4, 113.3, 126_000_000, null),
            new(G("a00000000022"), G("a00000000021"), 3, "CN", "44", "440100", "广州市", "Guangzhou", null, 23.1, 113.3, 18_700_000, null),
            new(G("a00000000023"), G("a00000000021"), 3, "CN", "44", "440300", "深圳市", "Shenzhen", null, 22.5, 114.1, 17_600_000, null),
            new(G("a00000000024"), G("a00000000021"), 3, "CN", "44", "441900", "东莞市", "Dongguan", null, 23.0, 113.8, 10_500_000, null),
            new(G("a00000000101"), G("a00000000001"), 2, "CN", "13", "130000", "河北省", "Hebei", null, 38.0, 114.5, 74_600_000, null),
            new(G("a00000000111"), G("a00000000101"), 3, "CN", "13", "130100", "石家庄市", "Shijiazhuang", null, 38.0, 114.5, 11_200_000, null),
            new(G("a00000000112"), G("a00000000101"), 3, "CN", "13", "130200", "唐山市", "Tangshan", null, 39.6, 118.2, 7_700_000, null),
            new(G("a00000000121"), G("a00000000001"), 2, "CN", "14", "140000", "山西省", "Shanxi", null, 37.9, 112.5, 34_900_000, null),
            new(G("a00000000131"), G("a00000000121"), 3, "CN", "14", "140100", "太原市", "Taiyuan", null, 37.9, 112.5, 5_300_000, null),
            new(G("a00000000141"), G("a00000000001"), 2, "CN", "15", "150000", "内蒙古自治区", "Inner Mongolia", null, 40.8, 111.7, 24_000_000, null),
            new(G("a00000000151"), G("a00000000141"), 3, "CN", "15", "150100", "呼和浩特市", "Hohhot", null, 40.8, 111.7, 3_400_000, null),
            new(G("a00000000152"), G("a00000000141"), 3, "CN", "15", "150200", "包头市", "Baotou", null, 40.7, 109.8, 2_700_000, null),
            new(G("a00000000161"), G("a00000000001"), 2, "CN", "21", "210000", "辽宁省", "Liaoning", null, 41.8, 123.4, 42_600_000, null),
            new(G("a00000000171"), G("a00000000161"), 3, "CN", "21", "210100", "沈阳市", "Shenyang", null, 41.8, 123.4, 9_100_000, null),
            new(G("a00000000172"), G("a00000000161"), 3, "CN", "21", "210200", "大连市", "Dalian", null, 38.9, 121.6, 7_500_000, null),
            new(G("a00000000181"), G("a00000000001"), 2, "CN", "22", "220000", "吉林省", "Jilin", null, 43.9, 125.3, 24_100_000, null),
            new(G("a00000000191"), G("a00000000181"), 3, "CN", "22", "220100", "长春市", "Changchun", null, 43.9, 125.3, 9_100_000, null),
            new(G("a00000000201"), G("a00000000001"), 2, "CN", "23", "230000", "黑龙江省", "Heilongjiang", null, 45.8, 126.5, 31_900_000, null),
            new(G("a00000000211"), G("a00000000201"), 3, "CN", "23", "230100", "哈尔滨市", "Harbin", null, 45.8, 126.5, 10_100_000, null),
            new(G("a00000000221"), G("a00000000001"), 2, "CN", "32", "320000", "江苏省", "Jiangsu", null, 32.1, 118.8, 84_700_000, null),
            new(G("a00000000231"), G("a00000000221"), 3, "CN", "32", "320100", "南京市", "Nanjing", null, 32.1, 118.8, 9_400_000, null),
            new(G("a00000000232"), G("a00000000221"), 3, "CN", "32", "320500", "苏州市", "Suzhou", null, 31.3, 120.6, 12_700_000, null),
            new(G("a00000000233"), G("a00000000221"), 3, "CN", "32", "320200", "无锡市", "Wuxi", null, 31.5, 120.3, 7_500_000, null),
            new(G("a00000000241"), G("a00000000001"), 2, "CN", "33", "330000", "浙江省", "Zhejiang", null, 30.3, 120.2, 64_600_000, null),
            new(G("a00000000251"), G("a00000000241"), 3, "CN", "33", "330100", "杭州市", "Hangzhou", null, 30.3, 120.2, 12_200_000, null),
            new(G("a00000000252"), G("a00000000241"), 3, "CN", "33", "330200", "宁波市", "Ningbo", null, 29.9, 121.5, 9_400_000, null),
            new(G("a00000000261"), G("a00000000001"), 2, "CN", "34", "340000", "安徽省", "Anhui", null, 31.9, 117.3, 61_000_000, null),
            new(G("a00000000271"), G("a00000000261"), 3, "CN", "34", "340100", "合肥市", "Hefei", null, 31.8, 117.2, 9_400_000, null),
            new(G("a00000000281"), G("a00000000001"), 2, "CN", "35", "350000", "福建省", "Fujian", null, 26.1, 119.3, 41_500_000, null),
            new(G("a00000000291"), G("a00000000281"), 3, "CN", "35", "350100", "福州市", "Fuzhou", null, 26.1, 119.3, 8_400_000, null),
            new(G("a00000000292"), G("a00000000281"), 3, "CN", "35", "350200", "厦门市", "Xiamen", null, 24.5, 118.1, 5_200_000, null),
            new(G("a00000000301"), G("a00000000001"), 2, "CN", "36", "360000", "江西省", "Jiangxi", null, 28.7, 115.9, 45_200_000, null),
            new(G("a00000000311"), G("a00000000301"), 3, "CN", "36", "360100", "南昌市", "Nanchang", null, 28.7, 115.9, 6_400_000, null),
            new(G("a00000000321"), G("a00000000001"), 2, "CN", "37", "370000", "山东省", "Shandong", null, 36.7, 117.1, 101_500_000, null),
            new(G("a00000000331"), G("a00000000321"), 3, "CN", "37", "370100", "济南市", "Jinan", null, 36.7, 117.1, 9_200_000, null),
            new(G("a00000000332"), G("a00000000321"), 3, "CN", "37", "370200", "青岛市", "Qingdao", null, 36.1, 120.4, 10_100_000, null),
            new(G("a00000000341"), G("a00000000001"), 2, "CN", "41", "410000", "河南省", "Henan", null, 34.7, 113.6, 99_400_000, null),
            new(G("a00000000351"), G("a00000000341"), 3, "CN", "41", "410100", "郑州市", "Zhengzhou", null, 34.7, 113.6, 12_600_000, null),
            new(G("a00000000361"), G("a00000000001"), 2, "CN", "42", "420000", "湖北省", "Hubei", null, 30.6, 114.3, 57_700_000, null),
            new(G("a00000000371"), G("a00000000361"), 3, "CN", "42", "420100", "武汉市", "Wuhan", null, 30.6, 114.3, 12_300_000, null),
            new(G("a00000000381"), G("a00000000001"), 2, "CN", "43", "430000", "湖南省", "Hunan", null, 28.2, 112.9, 66_400_000, null),
            new(G("a00000000391"), G("a00000000381"), 3, "CN", "43", "430100", "长沙市", "Changsha", null, 28.2, 112.9, 10_000_000, null),
            new(G("a00000000651"), G("a00000000001"), 2, "CN", "45", "450000", "广西壮族自治区", "Guangxi", null, 22.8, 108.3, 50_100_000, null),
            new(G("a00000000661"), G("a00000000651"), 3, "CN", "45", "450100", "南宁市", "Nanning", null, 22.8, 108.3, 8_700_000, null),
            new(G("a00000000401"), G("a00000000001"), 2, "CN", "46", "460000", "海南省", "Hainan", null, 20.0, 110.3, 10_100_000, null),
            new(G("a00000000411"), G("a00000000401"), 3, "CN", "46", "460100", "海口市", "Haikou", null, 20.0, 110.3, 2_900_000, null),
            new(G("a00000000421"), G("a00000000001"), 2, "CN", "51", "510000", "四川省", "Sichuan", null, 30.7, 104.1, 83_700_000, null),
            new(G("a00000000431"), G("a00000000421"), 3, "CN", "51", "510100", "成都市", "Chengdu", null, 30.6, 104.1, 20_900_000, null),
            new(G("a00000000441"), G("a00000000001"), 2, "CN", "52", "520000", "贵州省", "Guizhou", null, 26.6, 106.6, 38_600_000, null),
            new(G("a00000000451"), G("a00000000441"), 3, "CN", "52", "520100", "贵阳市", "Guiyang", null, 26.6, 106.6, 6_000_000, null),
            new(G("a00000000461"), G("a00000000001"), 2, "CN", "53", "530000", "云南省", "Yunnan", null, 25.0, 102.7, 47_200_000, null),
            new(G("a00000000471"), G("a00000000461"), 3, "CN", "53", "530100", "昆明市", "Kunming", null, 24.9, 102.8, 8_500_000, null),
            new(G("a00000000481"), G("a00000000001"), 2, "CN", "54", "540000", "西藏自治区", "Tibet", null, 29.7, 91.1, 3_600_000, null),
            new(G("a00000000491"), G("a00000000481"), 3, "CN", "54", "540100", "拉萨市", "Lhasa", null, 29.7, 91.1, 900_000, null),
            new(G("a00000000501"), G("a00000000001"), 2, "CN", "61", "610000", "陕西省", "Shaanxi", null, 34.3, 108.9, 39_500_000, null),
            new(G("a00000000511"), G("a00000000501"), 3, "CN", "61", "610100", "西安市", "Xi'an", null, 34.3, 108.9, 12_900_000, null),
            new(G("a00000000521"), G("a00000000001"), 2, "CN", "62", "620000", "甘肃省", "Gansu", null, 36.1, 103.8, 25_000_000, null),
            new(G("a00000000531"), G("a00000000521"), 3, "CN", "62", "620100", "兰州市", "Lanzhou", null, 36.1, 103.8, 4_400_000, null),
            new(G("a00000000541"), G("a00000000001"), 2, "CN", "63", "630000", "青海省", "Qinghai", null, 36.6, 101.8, 5_900_000, null),
            new(G("a00000000551"), G("a00000000541"), 3, "CN", "63", "630100", "西宁市", "Xining", null, 36.6, 101.8, 2_500_000, null),
            new(G("a00000000561"), G("a00000000001"), 2, "CN", "64", "640000", "宁夏回族自治区", "Ningxia", null, 38.5, 106.3, 7_200_000, null),
            new(G("a00000000571"), G("a00000000561"), 3, "CN", "64", "640100", "银川市", "Yinchuan", null, 38.5, 106.3, 3_000_000, null),
            new(G("a00000000581"), G("a00000000001"), 2, "CN", "65", "650000", "新疆维吾尔自治区", "Xinjiang", null, 43.8, 87.6, 25_900_000, null),
            new(G("a00000000591"), G("a00000000581"), 3, "CN", "65", "650100", "乌鲁木齐市", "Urumqi", null, 43.8, 87.6, 4_100_000, null),
            new(G("a00000000601"), G("a00000000001"), 2, "CN", "71", "710000", "台湾省", "Taiwan", null, 25.0, 121.5, 23_500_000, null),
            new(G("a00000000611"), G("a00000000601"), 3, "CN", "71", "710100", "台北市", "Taipei", null, 25.0, 121.6, 2_600_000, null),
            // 特别区：本身即城市，无下级
            new(G("a00000000621"), G("a00000000001"), 2, "CN", "81", "810000", "香港特别行政区", "Hong Kong", null, 22.3, 114.2, 7_300_000, null),
            new(G("a00000000641"), G("a00000000001"), 2, "CN", "82", "820000", "澳门特别行政区", "Macao", null, 22.2, 113.5, 700_000, null),
            // 美国
            new(G("b00000000001"), null, 1, "US", null, "US", "美国", "United States", null, 39.8, -98.6, 331_000_000, null),
            new(G("b00000000011"), G("b00000000001"), 2, "US", "06", "060000", "加利福尼亚州", "California", null, 36.8, -119.4, 39_000_000, null),
            new(G("b00000000012"), G("b00000000011"), 3, "US", "06", "060400", "洛杉矶县", "Los Angeles", null, 34.1, -118.2, 9_900_000, null),
            new(G("b00000000021"), G("b00000000001"), 2, "US", "48", "480000", "得克萨斯州", "Texas", null, 31.5, -99.3, 29_100_000, null),
            new(G("b00000000022"), G("b00000000021"), 3, "US", "48", "482000", "哈里斯县", "Harris", null, 29.8, -95.4, 4_700_000, null),
            new(G("b00000000031"), G("b00000000001"), 2, "US", "36", "360000", "纽约州", "New York", null, 42.9, -75.5, 19_700_000, null),
            new(G("b00000000032"), G("b00000000031"), 3, "US", "36", "360400", "纽约县", "New York County", null, 40.8, -74.0, 1_600_000, null),
            // 英国
            new(G("c00000000001"), null, 1, "GB", null, "GB", "英国", "United Kingdom", null, 54.0, -2.0, 67_000_000, null),
            new(G("c00000000011"), G("c00000000001"), 2, "GB", "ENG", "ENG", "英格兰", "England", null, 52.4, -1.5, 56_000_000, null),
            new(G("c00000000012"), G("c00000000011"), 3, "GB", "ENG", "LND", "伦敦市", "London", null, 51.5, -0.1, 8_900_000, null),
            new(G("c00000000021"), G("c00000000001"), 2, "GB", "SCT", "SCT", "苏格兰", "Scotland", null, 56.8, -4.2, 5_500_000, null),
            new(G("c00000000022"), G("c00000000021"), 3, "GB", "SCT", "EDH", "爱丁堡", "Edinburgh", null, 56.0, -3.2, 550_000, null),
            new(G("c00000000031"), G("c00000000001"), 2, "GB", "WLS", "WLS", "威尔士", "Wales", null, 52.1, -3.8, 3_100_000, null),
            new(G("c00000000032"), G("c00000000031"), 3, "GB", "WLS", "CDF", "加的夫", "Cardiff", null, 51.5, -3.2, 370_000, null),
            // 日本
            new(G("d00000000001"), null, 1, "JP", null, "JP", "日本", "Japan", null, 36.2, 138.3, 125_000_000, null),
            new(G("d00000000011"), G("d00000000001"), 2, "JP", "13", "130000", "东京都", "Tokyo", null, 35.7, 139.7, 14_000_000, null),
            new(G("d00000000012"), G("d00000000011"), 3, "JP", "13", "130010", "东京特别区", "Tokyo Special Wards", null, 35.7, 139.7, 9_700_000, null),
            new(G("d00000000021"), G("d00000000001"), 2, "JP", "27", "270000", "大阪府", "Osaka", null, 34.7, 135.5, 8_800_000, null),
            new(G("d00000000022"), G("d00000000021"), 3, "JP", "27", "271000", "大阪市", "Osaka City", null, 34.7, 135.5, 2_700_000, null),
            new(G("d00000000031"), G("d00000000001"), 2, "JP", "26", "260000", "京都府", "Kyoto", null, 35.0, 135.8, 2_500_000, null),
            new(G("d00000000032"), G("d00000000031"), 3, "JP", "26", "261000", "京都市", "Kyoto City", null, 35.0, 135.8, 1_500_000, null)
        ];
    }

    // ---------- 翻译子资源 ----------

    private async Task<HttpResponseMessage> HandleTranslationAsync(
        HttpRequestMessage request, string key, string culture, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Get)
        {
            lock (Gate)
            {
                return Translations.TryGetValue(key, out var values)
                    ? Json(new TranslationView(culture, values), HttpStatusCode.OK)
                    : Empty(HttpStatusCode.NotFound);
            }
        }

        if (request.Method == HttpMethod.Put)
        {
            using var body = await ReadJsonAsync(request, cancellationToken);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (body.RootElement.TryGetProperty("values", out var element) && element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        values[property.Name] = property.Value.GetString() ?? string.Empty;
                    }
                }
            }

            lock (Gate)
            {
                Translations[key] = values;
            }

            return Empty(HttpStatusCode.NoContent);
        }

        if (request.Method == HttpMethod.Delete)
        {
            lock (Gate)
            {
                Translations.Remove(key);
            }

            return Empty(HttpStatusCode.NoContent);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static void RemoveTranslations(string kind, Guid id)
    {
        var prefix = $"{kind}/{id}/";
        foreach (var key in Translations.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .ToList())
        {
            Translations.Remove(key);
        }
    }

    // ---------- 多语言 ----------

    // 与真实网关一致：按 Accept-Language 协商语言，候选链首项即当前语言。
    private string ResolveCulture(HttpRequestMessage request)
    {
        var requested = request.Headers.AcceptLanguage
            .Select(language => language.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return localization.CultureChain(requested)[0];
    }

    // 按语言链合并译文，先命中的语言优先；未命中的字段由调用方回退主表值。
    private Dictionary<string, string> FindTranslations(string kind, Guid id, string culture)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        lock (Gate)
        {
            foreach (var candidate in localization.CultureChain(culture))
            {
                if (Translations.TryGetValue($"{kind}/{id}/{candidate}", out var values))
                {
                    foreach (var (field, value) in values)
                    {
                        merged.TryAdd(field, value);
                    }
                }
            }
        }

        return merged;
    }

    // 空译文视为未翻译，保持与真实网关"回退主表"一致的行为。
    private static string? Pick(Dictionary<string, string> values, string field) =>
        values.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private AirportResponse Localize(AirportResponse airport, string culture)
    {
        var values = FindTranslations("airport", airport.Id, culture);
        return airport with
        {
            Name = Pick(values, "Name") ?? airport.Name,
            City = Pick(values, "City") ?? airport.City
        };
    }

    private NewsArticleView Localize(NewsArticleView article, string culture)
    {
        var values = FindTranslations("news", article.Id, culture);
        return article with
        {
            Title = Pick(values, "Title") ?? article.Title,
            Summary = Pick(values, "Summary") ?? article.Summary,
            Content = Pick(values, "Content") ?? article.Content
        };
    }

    private RegionView Localize(RegionView region, string culture)
    {
        var values = FindTranslations("region", region.Id, culture);
        return region with { Name = Pick(values, "Name") ?? region.Name };
    }

    private RegionTreeView Localize(RegionTreeView node, string culture)
    {
        var values = FindTranslations("region", node.Id, culture);
        return node with
        {
            Name = Pick(values, "Name") ?? node.Name,
            Children = node.Children.Select(child => Localize(child, culture)).ToList()
        };
    }

    // ---------- 工具 ----------

    private static bool TryParseSubResource(string rest, out Guid id, out string? culture)
    {
        culture = null;
        id = Guid.Empty;
        var parts = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !Guid.TryParse(parts[0], out id))
        {
            return false;
        }

        if (parts.Length >= 3 && parts[1].Equals("translations", StringComparison.OrdinalIgnoreCase))
        {
            culture = parts[2];
        }

        return true;
    }

    private static bool TryParseIfMatch(
        HttpRequestMessage request, out Guid expectedETag, out HttpResponseMessage? error)
    {
        expectedETag = default;
        error = null;
        var etag = request.Headers.TryGetValues(ApiClientHeaders.IfMatch, out var values)
            ? string.Join(",", values).Trim()
            : string.Empty;

        if (etag.Length == 0)
        {
            error = Problem(HttpStatusCode.PreconditionRequired, "请求缺少 If-Match 头。");
            return false;
        }

        // 与真实端点一致，只接受带引号的 N 格式 GUID。
        if (etag.Length < 3 || etag[0] != '"' || etag[^1] != '"'
            || !Guid.TryParseExact(etag[1..^1], "N", out expectedETag))
        {
            error = Problem(HttpStatusCode.BadRequest, "If-Match 头格式无效。");
            return false;
        }

        return true;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var raw = request.Content is null
            ? "{}"
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
    }

    private static string? Body(JsonDocument document, string field) =>
        document.RootElement.ValueKind == JsonValueKind.Object
        && document.RootElement.TryGetProperty(field, out var element)
        && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static string? Text(JsonDocument document, string field) =>
        Body(document, field) is { } value && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static double? Number(JsonDocument document, string field) =>
        document.RootElement.ValueKind == JsonValueKind.Object
        && document.RootElement.TryGetProperty(field, out var element)
        && element.ValueKind == JsonValueKind.Number
        && element.TryGetDouble(out var value)
            ? value
            : null;

    private static long? Integer(JsonDocument document, string field) =>
        document.RootElement.ValueKind == JsonValueKind.Object
        && document.RootElement.TryGetProperty(field, out var element)
        && element.ValueKind == JsonValueKind.Number
        && element.TryGetInt64(out var value)
            ? value
            : null;

    private static Dictionary<string, string> ParseQuery(string? query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = pair.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..index]);
            var value = Uri.UnescapeDataString(pair[(index + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private static object Paged<T>(IEnumerable<T> source)
    {
        var items = source as IReadOnlyList<T> ?? source.ToList();
        return new PagedResponse<T>(items, items.Count, Page: 1, PageSize: Math.Max(items.Count, 1));
    }

    private static HttpResponseMessage Json(object payload, HttpStatusCode statusCode, string? etag = null)
    {
        var response = new HttpResponseMessage(statusCode) { Content = JsonContent.Create(payload) };
        if (etag is not null)
        {
            response.Headers.ETag = new EntityTagHeaderValue($"\"{etag}\"");
        }

        return response;
    }

    private static HttpResponseMessage Problem(HttpStatusCode statusCode, string title)
    {
        var response = Json(
            new ProblemDetails { Status = (int)statusCode, Title = title },
            statusCode);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json");
        return response;
    }

    private static HttpResponseMessage Empty(HttpStatusCode statusCode) => new(statusCode);
}
