// 开发 Mock 各资源处理器共用的静态辅助：请求体读取、JSON 取值、查询串解析与统一响应构造。
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Axiom.SharedKernel.Models;
using Axiom.Web.Clients;
using Microsoft.AspNetCore.Mvc;

namespace Axiom.Web.Mocking;

internal static class MockHttp
{
    // ---------- 请求解析 ----------

    internal static async Task<JsonDocument> ReadJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var raw = request.Content is null
            ? "{}"
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
    }

    internal static string? Body(JsonDocument document, string field) =>
        document.RootElement.ValueKind == JsonValueKind.Object
        && document.RootElement.TryGetProperty(field, out var element)
        && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    internal static string? Text(JsonDocument document, string field) =>
        Body(document, field) is { } value && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    internal static double? Number(JsonDocument document, string field) =>
        document.RootElement.ValueKind == JsonValueKind.Object
        && document.RootElement.TryGetProperty(field, out var element)
        && element.ValueKind == JsonValueKind.Number
        && element.TryGetDouble(out var value)
            ? value
            : null;

    internal static long? Integer(JsonDocument document, string field) =>
        document.RootElement.ValueKind == JsonValueKind.Object
        && document.RootElement.TryGetProperty(field, out var element)
        && element.ValueKind == JsonValueKind.Number
        && element.TryGetInt64(out var value)
            ? value
            : null;

    internal static Dictionary<string, string> ParseQuery(string? query)
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

    // "{id}" 或 "{id}/translations/{culture}" 形式的子资源路径解析。
    internal static bool TryParseSubResource(string rest, out Guid id, out string? culture)
    {
        culture = null;
        id = Guid.Empty;
        var parts = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !Guid.TryParse(parts[0], out id))
        {
            return false;
        }

        if (parts.Length >= 3 && parts[1].Equals(GatewayFields.TRANSLATIONS, StringComparison.OrdinalIgnoreCase))
        {
            culture = parts[2];
        }

        return true;
    }

    // ---------- 响应构造 ----------

    internal static object Paged<T>(IReadOnlyList<T> items) =>
        new PagedResponse<T>(items, items.Count, Page: 1, PageSize: Math.Max(items.Count, 1));

    internal static HttpResponseMessage Json(object payload, HttpStatusCode statusCode, string? etag = null)
    {
        var response = new HttpResponseMessage(statusCode) { Content = JsonContent.Create(payload) };
        if (etag is not null)
        {
            response.Headers.ETag = new EntityTagHeaderValue($"\"{etag}\"");
        }

        return response;
    }

    internal static HttpResponseMessage Problem(HttpStatusCode statusCode, string title, string? errorCode = null)
    {
        var problem = new ProblemDetails { Status = (int)statusCode, Title = title };
        if (errorCode is not null)
        {
            // 与真实端点一致：让客户端能区分同为 409 的唯一键冲突与并发冲突。
            problem.Extensions["errorCode"] = errorCode;
        }

        var response = Json(problem, statusCode);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json");
        return response;
    }

    // 解析 If-Match：缺失 428，格式错误 400；与真实端点一致，只接受带引号的 N 格式 GUID。
    internal static bool TryParseIfMatch(
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

        if (etag.Length < 3 || etag[0] != '"' || etag[^1] != '"'
            || !Guid.TryParseExact(etag[1..^1], "N", out expectedETag))
        {
            error = Problem(HttpStatusCode.BadRequest, "If-Match 头格式无效。");
            return false;
        }

        return true;
    }

    internal static HttpResponseMessage Empty(HttpStatusCode statusCode) => new(statusCode);
}
