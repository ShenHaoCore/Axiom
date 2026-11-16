// 枚举字典 Mock：/api/reference-data/enums 的列表、CRUD 与按语言翻译读写。
using System.Net;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using static Axiom.Web.Mocking.MockHttp;

namespace Axiom.Web.Mocking;

internal sealed class MockEnumHandler(MockLocalizer localizer)
{
    // 返回 null 仅发生在翻译子资源方法不受支持时，由入口回落真实网关。
    internal async Task<HttpResponseMessage?> HandleAsync(
        HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var rest = path[ReferenceEnumClient.ENUMS_PATH.Length..].Trim('/');

        if (rest.Length == 0)
        {
            if (request.Method == HttpMethod.Get)
            {
                var query = ParseQuery(request.RequestUri?.Query);
                var requestCulture = localizer.ResolveCulture(request);
                query.TryGetValue(GatewayFields.CATEGORY, out var category);
                List<ReferenceEnumView> snapshot;
                lock (MockDataStore.Gate)
                {
                    snapshot = string.IsNullOrWhiteSpace(category)
                        ? [.. MockDataStore.Enums.OrderBy(e => e.Category).ThenBy(e => e.SortOrder).ThenBy(e => e.Code)]
                        : MockDataStore.Enums
                            .Where(e => e.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase))
                            .OrderBy(e => e.SortOrder)
                            .ThenBy(e => e.Code)
                            .ToList();
                }

                return Json(Paged(snapshot.Select(item => localizer.Localize(item, requestCulture)).ToList()), HttpStatusCode.OK);
            }

            if (request.Method == HttpMethod.Post)
            {
                using var body = await ReadJsonAsync(request, cancellationToken);
                var category = Text(body, GatewayFields.CATEGORY)?.Trim().ToLowerInvariant();
                var code = Text(body, GatewayFields.CODE)?.Trim().ToLowerInvariant();
                var name = Text(body, GatewayFields.NAME)?.Trim();
                if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                {
                    return Problem(HttpStatusCode.BadRequest, "枚举创建请求无效。");
                }

                var sortOrder = (int?)Integer(body, GatewayFields.SORT_ORDER) ?? 0;
                lock (MockDataStore.Gate)
                {
                    if (MockDataStore.Enums.Any(e => e.Category == category && e.Code == code))
                    {
                        return Problem(HttpStatusCode.Conflict, "相同分类+代码的枚举项已存在。");
                    }

                    var created = new ReferenceEnumView(Guid.NewGuid(), category, code, name, sortOrder);
                    MockDataStore.Enums.Add(created);
                    return Json(created, HttpStatusCode.Created);
                }
            }
        }

        if (TryParseSubResource(rest, out var id, out var culture))
        {
            if (culture is not null)
            {
                return await MockTranslationHandler.HandleAsync(request, $"enum/{id}/{culture}", culture, cancellationToken);
            }

            if (request.Method == HttpMethod.Get)
            {
                var requestCulture = localizer.ResolveCulture(request);
                lock (MockDataStore.Gate)
                {
                    var item = MockDataStore.Enums.FirstOrDefault(e => e.Id == id);
                    return item is null
                        ? Problem(HttpStatusCode.NotFound, "未找到指定枚举项。")
                        : Json(localizer.Localize(item, requestCulture), HttpStatusCode.OK);
                }
            }

            if (request.Method == HttpMethod.Put)
            {
                using var body = await ReadJsonAsync(request, cancellationToken);
                lock (MockDataStore.Gate)
                {
                    var index = MockDataStore.Enums.FindIndex(e => e.Id == id);
                    if (index < 0)
                    {
                        return Problem(HttpStatusCode.NotFound, "未找到指定枚举项。");
                    }

                    var original = MockDataStore.Enums[index];
                    var updated = original with
                    {
                        Name = Text(body, GatewayFields.NAME)?.Trim() ?? original.Name,
                        SortOrder = (int?)Integer(body, GatewayFields.SORT_ORDER) ?? original.SortOrder
                    };
                    MockDataStore.Enums[index] = updated;
                    return Json(updated, HttpStatusCode.OK);
                }
            }

            if (request.Method == HttpMethod.Delete)
            {
                lock (MockDataStore.Gate)
                {
                    var index = MockDataStore.Enums.FindIndex(e => e.Id == id);
                    if (index < 0)
                    {
                        return Problem(HttpStatusCode.NotFound, "未找到指定枚举项。");
                    }

                    var item = MockDataStore.Enums[index];
                    if (item.Category.StartsWith(ReferenceEnumClient.GeoDivisionKindCategory, StringComparison.Ordinal)
                        && MockDataStore.GeoDivisions.Any(r => r.Kind == item.Code))
                    {
                        return Problem(HttpStatusCode.Conflict, "枚举项仍被引用，无法删除。");
                    }

                    MockDataStore.Enums.RemoveAt(index);
                    MockDataStore.RemoveTranslations("enum", id);
                    return Empty(HttpStatusCode.NoContent);
                }
            }
        }

        return Empty(HttpStatusCode.NotFound);
    }
}
