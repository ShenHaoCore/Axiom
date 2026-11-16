// 机场 Mock：/api/reference-data/airports 的列表、CRUD（ETag 乐观并发、代码唯一）与翻译读写。
using System.Net;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using static Axiom.Web.Mocking.MockHttp;

namespace Axiom.Web.Mocking;

internal sealed class MockAirportHandler(MockLocalizer localizer)
{
    // 返回 null 仅发生在翻译子资源方法不受支持时，由入口回落真实网关。
    internal async Task<HttpResponseMessage?> HandleAsync(
        HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var rest = path[ReferenceDataClient.AIRPORTS_PATH.Length..].Trim('/');

        if (rest.Length == 0)
        {
            if (request.Method == HttpMethod.Get)
            {
                var requestCulture = localizer.ResolveCulture(request);
                List<AirportResponse> snapshot;
                lock (MockDataStore.Gate) { snapshot = [.. MockDataStore.Airports]; }
                return Json(Paged(snapshot.Select(airport => localizer.Localize(airport, requestCulture)).ToList()), HttpStatusCode.OK);
            }

            if (request.Method == HttpMethod.Post)
            {
                using var body = await ReadJsonAsync(request, cancellationToken);
                var code = Body(body, GatewayFields.CODE);
                if (string.IsNullOrWhiteSpace(code))
                {
                    return Empty(HttpStatusCode.BadRequest);
                }

                lock (MockDataStore.Gate)
                {
                    if (MockDataStore.Airports.Any(a => string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase)))
                    {
                        return Problem(HttpStatusCode.Conflict, "机场代码已存在。");
                    }

                    var created = new AirportResponse(
                        Guid.NewGuid(),
                        code.ToUpperInvariant(),
                        Body(body, GatewayFields.NAME) ?? code,
                        Body(body, GatewayFields.COUNTRY_CODE) ?? "CN",
                        Body(body, "city") ?? string.Empty,
                        Number(body, GatewayFields.LATITUDE),
                        Number(body, GatewayFields.LONGITUDE));
                    MockDataStore.Airports.Add(created);
                    MockDataStore.AirportETags[created.Id] = Guid.NewGuid();
                    return Json(created, HttpStatusCode.Created, MockDataStore.AirportETags[created.Id].ToString("N"));
                }
            }
        }

        if (TryParseSubResource(rest, out var id, out var culture))
        {
            if (culture is not null)
            {
                return await MockTranslationHandler.HandleAsync(request, $"airport/{id}/{culture}", culture, cancellationToken);
            }

            if (request.Method == HttpMethod.Get)
            {
                var requestCulture = localizer.ResolveCulture(request);
                lock (MockDataStore.Gate)
                {
                    var airport = MockDataStore.Airports.FirstOrDefault(a => a.Id == id);
                    return airport is null
                        ? Problem(HttpStatusCode.NotFound, "未找到指定机场。")
                        : Json(localizer.Localize(airport, requestCulture), HttpStatusCode.OK, MockDataStore.AirportETags[id].ToString("N"));
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

                lock (MockDataStore.Gate)
                {
                    var index = MockDataStore.Airports.FindIndex(a => a.Id == id);
                    if (index < 0)
                    {
                        return Problem(HttpStatusCode.NotFound, "未找到指定机场。");
                    }

                    if (MockDataStore.AirportETags[id] != expectedETag)
                    {
                        return Problem(HttpStatusCode.Conflict, "机场数据已被其他操作修改，请刷新后重试。");
                    }

                    if (request.Method == HttpMethod.Put)
                    {
                        var original = MockDataStore.Airports[index];
                        var code = Body(putBody!, GatewayFields.CODE) ?? original.Code;
                        if (string.IsNullOrWhiteSpace(code))
                        {
                            return Empty(HttpStatusCode.BadRequest);
                        }

                        if (MockDataStore.Airports.Any(a => a.Id != id
                            && string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase)))
                        {
                            return Problem(HttpStatusCode.Conflict, "机场代码已存在。");
                        }

                        var updated = new AirportResponse(
                            id,
                            code.ToUpperInvariant(),
                            Body(putBody!, GatewayFields.NAME) ?? original.Name,
                            Body(putBody!, GatewayFields.COUNTRY_CODE) ?? original.CountryCode,
                            Body(putBody!, "city"),
                            Number(putBody!, GatewayFields.LATITUDE),
                            Number(putBody!, GatewayFields.LONGITUDE));
                        MockDataStore.Airports[index] = updated;
                        MockDataStore.AirportETags[id] = Guid.NewGuid();
                        return Json(updated, HttpStatusCode.OK, MockDataStore.AirportETags[id].ToString("N"));
                    }

                    MockDataStore.Airports.RemoveAt(index);
                    MockDataStore.AirportETags.Remove(id);
                    MockDataStore.RemoveTranslations("airport", id);
                    return Empty(HttpStatusCode.NoContent);
                }
            }
        }

        // 机场写请求全部由 Mock 接管，未知路径也不回落真实网关。
        return Empty(HttpStatusCode.NotFound);
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
}
