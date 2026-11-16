// 使用当前 SSO 会话的访问令牌调用基础数据网关。
using System.Net.Http.Json;
using Axiom.Web.Models;

namespace Axiom.Web.Clients;

public sealed class ReferenceDataClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    : AuthenticatedApiClient(httpClient, httpContextAccessor)
{
    internal const string AIRPORTS_PATH = "/api/reference-data/airports";

    public Task<HttpResponseMessage> GetAirportsAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, AIRPORTS_PATH, null, cancellationToken);

    public Task<HttpResponseMessage> GetAirportAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetAirportAsync(id, culture: null, cancellationToken);

    /// <summary>读取机场；culture 非空时覆盖 Accept-Language，用于取默认语言原文。</summary>
    public Task<HttpResponseMessage> GetAirportAsync(
        Guid id,
        string? culture,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"{AIRPORTS_PATH}/{id}", null, cancellationToken, acceptLanguage: culture);

    public Task<HttpResponseMessage> CreateAirportAsync(
        AirportInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, AIRPORTS_PATH, JsonContent.Create(input), cancellationToken);

    public Task<HttpResponseMessage> UpdateAirportAsync(
        Guid id,
        string eTag,
        AirportInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"{AIRPORTS_PATH}/{id}", JsonContent.Create(input), cancellationToken, eTag);

    public Task<HttpResponseMessage> DeleteAirportAsync(
        Guid id,
        string eTag,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{AIRPORTS_PATH}/{id}", null, cancellationToken, eTag);

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
        $"{AIRPORTS_PATH}/{id}/{GatewayFields.TRANSLATIONS}/{Uri.EscapeDataString(culture)}";
}
