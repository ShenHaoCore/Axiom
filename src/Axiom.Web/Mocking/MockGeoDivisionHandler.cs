// 行政区 Mock：/api/geo-divisions 的列表/树/子级、CRUD（唯一键冲突、子节点删除保护）与翻译读写。
using System.Net;
using System.Text.Json;
using Axiom.ReferenceData.Domain.Constants;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using static Axiom.Web.Mocking.MockHttp;

namespace Axiom.Web.Mocking;

internal sealed class MockGeoDivisionHandler(MockLocalizer localizer)
{
    // 返回 null 仅发生在翻译子资源方法不受支持时，由入口回落真实网关。
    internal async Task<HttpResponseMessage?> HandleAsync(
        HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var rest = path[GeoDivisionClient.GEO_DIVISIONS_PATH.Length..].Trim('/');

        if (rest.Length == 0)
        {
            if (request.Method == HttpMethod.Get)
            {
                var query = ParseQuery(request.RequestUri?.Query);
                var requestCulture = localizer.ResolveCulture(request);
                var level = int.TryParse(query.GetValueOrDefault(GatewayFields.LEVEL), out var lv) ? lv : (int?)null;
                var country = query.GetValueOrDefault(GatewayFields.COUNTRY_CODE);
                var keyword = query.GetValueOrDefault(GatewayFields.KEYWORD);

                List<GeoDivisionView> matches;
                lock (MockDataStore.Gate)
                {
                    matches = MockDataStore.GeoDivisions
                        .Where(r => !level.HasValue || r.Level == level.Value)
                        .Where(r => string.IsNullOrWhiteSpace(country)
                            || string.Equals(r.CountryCode, country, StringComparison.OrdinalIgnoreCase))
                        .Where(r => string.IsNullOrWhiteSpace(keyword)
                            || r.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                            || r.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                            || (r.LocalName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                            || (r.AsciiName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
                        .OrderBy(r => r.CountryCode).ThenBy(r => r.Code)
                        .ToList();
                }

                return Json(Paged(matches.Select(region => localizer.Localize(region, requestCulture)).ToList()), HttpStatusCode.OK);
            }

            if (request.Method == HttpMethod.Post)
            {
                using var body = await ReadJsonAsync(request, cancellationToken);
                if (!TryReadLevel(body, out var level))
                {
                    return Problem(HttpStatusCode.BadRequest, "行政区创建请求无效。");
                }

                // Level 1（国家行）未单独给国家代码时，以区划代码兼任国家代码。
                var countryCode = Text(body, GatewayFields.COUNTRY_CODE)?.ToUpperInvariant()
                    ?? (level == 1 ? Text(body, GatewayFields.CODE)?.ToUpperInvariant() : null);
                var name = Text(body, GatewayFields.NAME);
                if (countryCode is null || name is null
                    || !TryReadParentId(body, out var parentId))
                {
                    return Problem(HttpStatusCode.BadRequest, "行政区创建请求无效。");
                }

                var kind = ParseGeoDivisionKind(Text(body, GatewayFields.KIND), countryCode, level);
                if (kind is null)
                {
                    return Problem(HttpStatusCode.BadRequest, "kind 必须是已维护的 geo_division.kind 枚举值。");
                }

                var created = new GeoDivisionView(
                    Guid.NewGuid(),
                    level == 1 ? null : parentId,
                    level,
                    kind,
                    countryCode,
                    Text(body, GatewayFields.SUBDIVISION_CODE),
                    Text(body, GatewayFields.CODE)?.ToUpperInvariant() ?? name,
                    name,
                    Text(body, GatewayFields.ASCII_NAME),
                    Text(body, GatewayFields.LOCAL_NAME),
                    Number(body, GatewayFields.LATITUDE),
                    Number(body, GatewayFields.LONGITUDE),
                    Integer(body, GatewayFields.POPULATION),
                    null);

                lock (MockDataStore.Gate)
                {
                    if (GeoDivisionKeyExists(null, created.Level, created.CountryCode, created.Code))
                    {
                        return Problem(HttpStatusCode.Conflict, "行政区唯一键已存在。");
                    }

                    MockDataStore.GeoDivisions.Add(created);
                    return Json(created, HttpStatusCode.Created);
                }
            }
        }

        if (rest.Equals(GatewayFields.TREE, StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
        {
            var country = ParseQuery(request.RequestUri?.Query).GetValueOrDefault(GatewayFields.COUNTRY_CODE) ?? "CN";
            return Json(localizer.Localize(BuildTree(country), localizer.ResolveCulture(request)), HttpStatusCode.OK);
        }

        var restParts = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (restParts.Length == 2
            && restParts[1].Equals(GatewayFields.CHILDREN, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(restParts[0], out var childrenParentId)
            && request.Method == HttpMethod.Get)
        {
            lock (MockDataStore.Gate)
            {
                if (!MockDataStore.GeoDivisions.Any(r => r.Id == childrenParentId))
                {
                    return Problem(HttpStatusCode.NotFound, "未找到指定行政区。");
                }

                var requestCulture = localizer.ResolveCulture(request);
                var children = MockDataStore.GeoDivisions
                    .Where(r => r.ParentId == childrenParentId)
                    .OrderBy(r => r.Code)
                    .Select(child =>
                    {
                        var grandchildren = MockDataStore.GeoDivisions.Where(g => g.ParentId == child.Id).ToList();
                        var sameNameChildCount = grandchildren.Count(g =>
                            string.Equals(g.Name.Trim(), child.Name.Trim(), StringComparison.Ordinal));
                        return new GeoDivisionChildView(
                            child.Id,
                            child.Level,
                            child.Kind,
                            child.CountryCode,
                            child.SubdivisionCode,
                            child.Code,
                            Name: localizer.Localize(child, requestCulture).Name,
                            child.Population,
                            grandchildren.Count,
                            sameNameChildCount);
                    })
                    .ToList();
                return Json(children, HttpStatusCode.OK);
            }
        }

        if (TryParseSubResource(rest, out var id, out var culture))
        {
            if (culture is not null)
            {
                return await MockTranslationHandler.HandleAsync(request, $"region/{id}/{culture}", culture, cancellationToken);
            }

            if (request.Method == HttpMethod.Get)
            {
                var requestCulture = localizer.ResolveCulture(request);
                lock (MockDataStore.Gate)
                {
                    var region = MockDataStore.GeoDivisions.FirstOrDefault(r => r.Id == id);
                    return region is null
                        ? Problem(HttpStatusCode.NotFound, "未找到指定行政区。")
                        : Json(localizer.Localize(region, requestCulture), HttpStatusCode.OK);
                }
            }

            if (request.Method == HttpMethod.Put)
            {
                using var body = await ReadJsonAsync(request, cancellationToken);
                if (!TryReadParentId(body, out var parentId))
                {
                    return Problem(HttpStatusCode.BadRequest, "行政区更新请求无效。");
                }

                lock (MockDataStore.Gate)
                {
                    var index = MockDataStore.GeoDivisions.FindIndex(r => r.Id == id);
                    if (index < 0)
                    {
                        return Problem(HttpStatusCode.NotFound, "未找到指定行政区。");
                    }

                    var original = MockDataStore.GeoDivisions[index];
                    var kind = original.Kind;
                    if (body.RootElement.TryGetProperty(GatewayFields.KIND, out _))
                    {
                        var parsed = ParseGeoDivisionKind(Text(body, GatewayFields.KIND), original.CountryCode, original.Level);
                        if (parsed is null)
                        {
                            return Problem(HttpStatusCode.BadRequest, "kind 必须是已维护的 geo_division.kind 枚举值。");
                        }

                        kind = parsed;
                    }

                    var updated = new GeoDivisionView(
                        id,
                        original.Level == 1 ? null : parentId ?? original.ParentId,
                        original.Level,
                        kind,
                        original.CountryCode,
                        original.SubdivisionCode,
                        Text(body, GatewayFields.CODE)?.ToUpperInvariant() ?? original.Code,
                        Text(body, GatewayFields.NAME) ?? original.Name,
                        Text(body, GatewayFields.ASCII_NAME) ?? original.AsciiName,
                        Text(body, GatewayFields.LOCAL_NAME) ?? original.LocalName,
                        Number(body, GatewayFields.LATITUDE) ?? original.Latitude,
                        Number(body, GatewayFields.LONGITUDE) ?? original.Longitude,
                        Integer(body, GatewayFields.POPULATION) ?? original.Population,
                        original.GeoNameId);

                    if (GeoDivisionKeyExists(id, updated.Level, updated.CountryCode, updated.Code))
                    {
                        return Problem(HttpStatusCode.Conflict, "行政区唯一键已存在。");
                    }

                    MockDataStore.GeoDivisions[index] = updated;
                    return Json(updated, HttpStatusCode.OK);
                }
            }

            if (request.Method == HttpMethod.Delete)
            {
                lock (MockDataStore.Gate)
                {
                    var index = MockDataStore.GeoDivisions.FindIndex(r => r.Id == id);
                    if (index < 0)
                    {
                        return Problem(HttpStatusCode.NotFound, "未找到指定行政区。");
                    }

                    if (MockDataStore.GeoDivisions.Any(r => r.ParentId == id))
                    {
                        return Problem(HttpStatusCode.BadRequest, "行政区仍有子节点，不能删除。");
                    }

                    MockDataStore.GeoDivisions.RemoveAt(index);
                    MockDataStore.RemoveTranslations("region", id);
                    return Empty(HttpStatusCode.NoContent);
                }
            }
        }

        // 行政区写请求全部由 Mock 接管，未知路径也不回落真实网关。
        return Empty(HttpStatusCode.NotFound);
    }

    private static bool TryReadLevel(JsonDocument document, out int level)
    {
        level = default;
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(GatewayFields.LEVEL, out var element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out level) && level >= 1)
        {
            return true;
        }

        return element.ValueKind == JsonValueKind.String
            && int.TryParse(element.GetString(), out level)
            && level >= 1;
    }

    private static string? ParseGeoDivisionKind(string? value, string countryCode, int level)
    {
        // Kind 留空时按国家与层级推导默认类型（复用 Domain 层 DeriveDefault）。
        var kind = string.IsNullOrWhiteSpace(value)
            ? GeoDivisionKinds.DeriveDefault(countryCode, level, null)
            : value.Trim().ToLowerInvariant();
        lock (MockDataStore.Gate)
        {
            return MockDataStore.Enums.Any(e => e.Category.StartsWith(ReferenceEnumClient.GeoDivisionKindCategory, StringComparison.Ordinal) && e.Code == kind)
                ? kind
                : null;
        }
    }

    private static bool TryReadParentId(JsonDocument document, out Guid? parentId)
    {
        parentId = null;
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(GatewayFields.PARENT_ID, out var element)
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

    private static bool GeoDivisionKeyExists(Guid? excludedId, int level, string countryCode, string code) =>
        MockDataStore.GeoDivisions.Any(region => (!excludedId.HasValue || region.Id != excludedId.Value)
            && region.Level == level
            && string.Equals(region.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(region.Code, code, StringComparison.OrdinalIgnoreCase));

    private static GeoDivisionTreeView BuildTree(string countryCode)
    {
        lock (MockDataStore.Gate)
        {
            var root = MockDataStore.GeoDivisions.FirstOrDefault(r => r.Level == 1
                && string.Equals(r.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
            return root is null
                ? new GeoDivisionTreeView(Guid.Empty, 1, GeoDivisionKinds.Country, countryCode, null, countryCode, countryCode, null, [])
                : BuildTreeNode(root);
        }
    }

    private static GeoDivisionTreeView BuildTreeNode(GeoDivisionView node) => new(
        node.Id,
        node.Level,
        node.Kind,
        node.CountryCode,
        node.SubdivisionCode,
        node.Code,
        node.Name,
        node.Population,
        MockDataStore.GeoDivisions
            .Where(child => child.ParentId == node.Id)
            .OrderBy(child => child.Code)
            .Select(BuildTreeNode)
            .ToList());
}
