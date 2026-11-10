// 仅开发 Mock 模式启用：拦截常用网关请求并维护进程内示例数据，
// 使机场、新闻、行政区增删改与翻译读写在 Mock 下形成完整交互闭环。
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Axiom.SharedKernel.Models;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Axiom.Web.Mocking;

public sealed class MockGatewayHandler : DelegatingHandler
{
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
                List<AirportResponse> snapshot;
                lock (Gate) { snapshot = [.. Airports]; }
                return Json(Paged(snapshot), HttpStatusCode.OK);
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
                lock (Gate)
                {
                    var airport = Airports.FirstOrDefault(a => a.Id == id);
                    return airport is null
                        ? Problem(HttpStatusCode.NotFound, "未找到指定机场。")
                        : Json(airport, HttpStatusCode.OK, AirportETags[id].ToString("N"));
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
                List<NewsArticleView> snapshot;
                lock (Gate) { snapshot = [.. Articles]; }
                return Json(Paged(snapshot), HttpStatusCode.OK);
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
                    if (index < 0)
                    {
                        return Json(SampleArticle(id), HttpStatusCode.OK, etag: $"mock-{id.ToString("N")[..8]}");
                    }

                    return Json(Articles[index], HttpStatusCode.OK, etag: $"mock-{id.ToString("N")[..8]}");
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

                return Json(Paged(matches), HttpStatusCode.OK);
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
            return Json(BuildTree(country), HttpStatusCode.OK);
        }

        if (TryParseSubResource(rest, out var id, out var culture))
        {
            if (culture is not null)
            {
                return await HandleTranslationAsync(request, $"region/{id}/{culture}", culture, cancellationToken);
            }

            if (request.Method == HttpMethod.Get)
            {
                lock (Gate)
                {
                    var region = Regions.FirstOrDefault(r => r.Id == id);
                    return region is null
                        ? Problem(HttpStatusCode.NotFound, "未找到指定行政区。")
                        : Json(region, HttpStatusCode.OK);
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

    private static List<RegionView> LoadRegions()
    {
        Guid G(string tail) => Guid.Parse($"00000000-0000-0000-0000-{tail}");

        return
        [
            // 中国
            new(G("a00000000001"), null, 1, "CN", null, "CN", "中国", "China", null, 35.0, 105.0, 1_410_000_000, null),
            new(G("a00000000011"), G("a00000000001"), 2, "CN", "11", "110000", "北京市", "Beijing", null, 39.9, 116.4, 21_890_000, null),
            new(G("a00000000012"), G("a00000000011"), 3, "CN", "11", "110100", "北京市", "Beijing", null, 39.9, 116.4, 21_890_000, null),
            new(G("a00000000021"), G("a00000000001"), 2, "CN", "44", "440000", "广东省", "Guangdong", null, 23.4, 113.3, 126_000_000, null),
            new(G("a00000000022"), G("a00000000021"), 3, "CN", "44", "440100", "广州市", "Guangzhou", null, 23.1, 113.3, 18_700_000, null),
            new(G("a00000000023"), G("a00000000021"), 3, "CN", "44", "440300", "深圳市", "Shenzhen", null, 22.5, 114.1, 17_600_000, null),
            new(G("a00000000031"), G("a00000000001"), 2, "CN", "31", "310000", "上海市", "Shanghai", null, 31.2, 121.5, 24_800_000, null),
            new(G("a00000000032"), G("a00000000031"), 3, "CN", "31", "310100", "上海市", "Shanghai", null, 31.2, 121.5, 24_800_000, null),
            // 美国
            new(G("b00000000001"), null, 1, "US", null, "US", "美国", "United States", null, 39.8, -98.6, 331_000_000, null),
            new(G("b00000000011"), G("b00000000001"), 2, "US", "06", "060000", "加利福尼亚州", "California", null, 36.8, -119.4, 39_000_000, null),
            new(G("b00000000012"), G("b00000000011"), 3, "US", "06", "060400", "洛杉矶县", "Los Angeles", null, 34.1, -118.2, 9_900_000, null),
            // 英国
            new(G("c00000000001"), null, 1, "GB", null, "GB", "英国", "United Kingdom", null, 54.0, -2.0, 67_000_000, null),
            new(G("c00000000011"), G("c00000000001"), 2, "GB", "ENG", "ENG", "英格兰", "England", null, 52.4, -1.5, 56_000_000, null),
            new(G("c00000000012"), G("c00000000011"), 3, "GB", "ENG", "LND", "伦敦市", "London", null, 51.5, -0.1, 8_900_000, null),
            // 日本
            new(G("d00000000001"), null, 1, "JP", null, "JP", "日本", "Japan", null, 36.2, 138.3, 125_000_000, null),
            new(G("d00000000011"), G("d00000000001"), 2, "JP", "13", "130000", "东京都", "Tokyo", null, 35.7, 139.7, 14_000_000, null),
            new(G("d00000000012"), G("d00000000011"), 3, "JP", "13", "130010", "东京特别区", "Tokyo Special Wards", null, 35.7, 139.7, 9_700_000, null)
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

    private static object Paged<T>(IReadOnlyList<T> items) =>
        new PagedResponse<T>(items, items.Count, Page: 1, PageSize: Math.Max(items.Count, 1));

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
