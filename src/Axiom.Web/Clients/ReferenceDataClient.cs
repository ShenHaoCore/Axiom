// 使用当前 SSO 会话的访问令牌调用基础数据网关。
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Axiom.Web.Models;

namespace Axiom.Web.Clients;

public sealed class ReferenceDataClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    private const string AIRPORTS_PATH = "/api/reference-data/airports";

    public Task<HttpResponseMessage> GetAirportsAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, AIRPORTS_PATH, null, cancellationToken);

    public Task<HttpResponseMessage> GetAirportAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"{AIRPORTS_PATH}/{id}", null, cancellationToken);

    public Task<HttpResponseMessage> CreateAirportAsync(
        AirportInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, AIRPORTS_PATH, JsonContent.Create(input), cancellationToken);

    public Task<HttpResponseMessage> GetAirportTranslationsAsync(
        Guid id,
        string culture,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, TranslationPath(id, culture), null, cancellationToken);

    public Task<HttpResponseMessage> SaveAirportTranslationsAsync(
        Guid id,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Put,
            TranslationPath(id, culture),
            JsonContent.Create(new { values }),
            cancellationToken);

    public Task<HttpResponseMessage> DeleteAirportTranslationsAsync(
        Guid id,
        string culture,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, TranslationPath(id, culture), null, cancellationToken);

    private static string TranslationPath(Guid id, string culture) =>
        $"{AIRPORTS_PATH}/{id}/translations/{Uri.EscapeDataString(culture)}";

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("当前请求缺少 HTTP 上下文。");
        var accessToken = await httpContext.GetTokenAsync("access_token")
            ?? throw new InvalidOperationException("当前登录会话不包含访问令牌。");

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Accept-Language", CultureInfo.CurrentUICulture.Name);
        request.Content = content;
        return await httpClient.SendAsync(request, cancellationToken);
    }
}
