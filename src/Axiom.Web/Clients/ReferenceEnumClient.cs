// 使用当前 SSO 会话的访问令牌调用枚举字典网关。
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Axiom.Web.Models;
using Microsoft.AspNetCore.Authentication;

namespace Axiom.Web.Clients;

public sealed class ReferenceEnumClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    internal const string ENUMS_PATH = "/api/reference-data/enums";
    public const string GeoDivisionKindCategory = "geo_division.kind";
    public const string GeoDivisionKindCategoryPrefix = "geo_division.kind.";

    /// <summary>Kind 字典按国家分域：geo_division.kind.{country}；国家代码无效时回退通用域。</summary>
    public static string GeoDivisionKindCategoryFor(string? countryCode)
    {
        var normalized = (countryCode ?? string.Empty).Trim();
        return normalized.Length == 2 && normalized.All(char.IsAsciiLetter)
            ? $"{GeoDivisionKindCategory}.{normalized.ToLowerInvariant()}"
            : GeoDivisionKindCategory;
    }

    public Task<HttpResponseMessage> ListAsync(
        string? category,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(category))
        {
            query.Add($"category={Uri.EscapeDataString(category)}");
        }

        return SendAsync(HttpMethod.Get, $"{ENUMS_PATH}/?{string.Join('&', query)}", null, cancellationToken);
    }

    public Task<HttpResponseMessage> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"{ENUMS_PATH}/{id}", null, cancellationToken);

    public Task<HttpResponseMessage> CreateAsync(ReferenceEnumInput input, CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            ENUMS_PATH,
            JsonContent.Create(new
            {
                category = input.Category,
                code = input.Code,
                name = input.Name,
                sortOrder = input.SortOrder
            }),
            cancellationToken);

    public Task<HttpResponseMessage> UpdateAsync(
        Guid id,
        ReferenceEnumInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Put,
            $"{ENUMS_PATH}/{id}",
            JsonContent.Create(new
            {
                name = input.Name,
                sortOrder = input.SortOrder
            }),
            cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{ENUMS_PATH}/{id}", null, cancellationToken);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("当前请求缺少 HTTP 上下文。");
        var accessToken = await httpContext.GetTokenAsync(ApiClientHeaders.AccessToken)
            ?? throw new InvalidOperationException("当前登录会话不包含访问令牌。");

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(ApiClientHeaders.BearerScheme, accessToken);
        request.Headers.TryAddWithoutValidation(ApiClientHeaders.AcceptLanguage, CultureInfo.CurrentUICulture.Name);
        request.Content = content;
        return await httpClient.SendAsync(request, cancellationToken);
    }
}
