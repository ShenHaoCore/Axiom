// 使用当前 SSO 会话的访问令牌调用基础数据网关。
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Axiom.Web.Models;

namespace Axiom.Web.Clients;

public sealed class ReferenceDataClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    internal const string AIRPORTS_PATH = "/api/reference-data/airports";

    public Task<HttpResponseMessage> GetAirportsAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, AIRPORTS_PATH, null, null, cancellationToken);

    public Task<HttpResponseMessage> GetAirportAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"{AIRPORTS_PATH}/{id}", null, null, cancellationToken);

    public Task<HttpResponseMessage> CreateAirportAsync(
        AirportInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, AIRPORTS_PATH, JsonContent.Create(input), null, cancellationToken);

    public Task<HttpResponseMessage> UpdateAirportAsync(
        Guid id,
        string eTag,
        AirportInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"{AIRPORTS_PATH}/{id}", JsonContent.Create(input), eTag, cancellationToken);

    public Task<HttpResponseMessage> DeleteAirportAsync(
        Guid id,
        string eTag,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{AIRPORTS_PATH}/{id}", null, eTag, cancellationToken);

    public Task<HttpResponseMessage> GetAirportTranslationsAsync(
        Guid id,
        string culture,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, TranslationPath(id, culture), null, null, cancellationToken);

    public Task<HttpResponseMessage> SaveAirportTranslationsAsync(
        Guid id,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Put,
            TranslationPath(id, culture),
            JsonContent.Create(new { values }),
            null,
            cancellationToken);

    public Task<HttpResponseMessage> DeleteAirportTranslationsAsync(
        Guid id,
        string culture,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, TranslationPath(id, culture), null, null, cancellationToken);

    private static string TranslationPath(Guid id, string culture) =>
        $"{AIRPORTS_PATH}/{id}/translations/{Uri.EscapeDataString(culture)}";

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        string? eTag,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("当前请求缺少 HTTP 上下文。");
        var accessToken = await httpContext.GetTokenAsync(ApiClientHeaders.AccessToken)
            ?? throw new InvalidOperationException("当前登录会话不包含访问令牌。");

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(ApiClientHeaders.BearerScheme, accessToken);
        request.Headers.TryAddWithoutValidation(ApiClientHeaders.AcceptLanguage, CultureInfo.CurrentUICulture.Name);
        if (!string.IsNullOrWhiteSpace(eTag))
        {
            request.Headers.TryAddWithoutValidation(ApiClientHeaders.IfMatch, eTag);
        }

        request.Content = content;
        return await httpClient.SendAsync(request, cancellationToken);
    }
}
