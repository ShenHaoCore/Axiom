// 使用当前 SSO 会话的访问令牌调用行政区网关。
using System.Net.Http.Json;
using Axiom.Web.Models;

namespace Axiom.Web.Clients;

public sealed class GeoDivisionClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    : AuthenticatedApiClient(httpClient, httpContextAccessor)
{
    internal const string GEO_DIVISIONS_PATH = "/api/geo-divisions";

    public Task<HttpResponseMessage> ListAsync(
        int page,
        int pageSize,
        int? level,
        string? countryCode,
        string? subdivisionCode,
        string? q,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"{GatewayFields.PAGE}={page}", $"{GatewayFields.PAGE_SIZE}={pageSize}" };
        if (level.HasValue) query.Add($"{GatewayFields.LEVEL}={level}");
        if (!string.IsNullOrWhiteSpace(countryCode)) query.Add($"{GatewayFields.COUNTRY_CODE}={Uri.EscapeDataString(countryCode)}");
        if (!string.IsNullOrWhiteSpace(subdivisionCode)) query.Add($"{GatewayFields.SUBDIVISION_CODE}={Uri.EscapeDataString(subdivisionCode)}");
        if (!string.IsNullOrWhiteSpace(q)) query.Add($"{GatewayFields.KEYWORD}={Uri.EscapeDataString(q)}");
        return SendAsync(HttpMethod.Get, $"{GEO_DIVISIONS_PATH}/?{string.Join('&', query)}", null, cancellationToken);
    }

    public Task<HttpResponseMessage> GetTreeAsync(
        string countryCode,
        int depth,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Get,
            $"{GEO_DIVISIONS_PATH}/{GatewayFields.TREE}?{GatewayFields.COUNTRY_CODE}={Uri.EscapeDataString(countryCode)}&depth={depth}",
            null,
            cancellationToken);

    public Task<HttpResponseMessage> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"{GEO_DIVISIONS_PATH}/{parentId}/{GatewayFields.CHILDREN}", null, cancellationToken);

    public Task<HttpResponseMessage> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync(id, culture: null, cancellationToken);

    /// <summary>读取行政区；culture 非空时覆盖 Accept-Language，用于取默认语言原文（绕开界面语言本地化）。</summary>
    public Task<HttpResponseMessage> GetAsync(Guid id, string? culture, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"{GEO_DIVISIONS_PATH}/{id}", null, cancellationToken, acceptLanguage: culture);

    public Task<HttpResponseMessage> CreateAsync(GeoDivisionInput input, CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            GEO_DIVISIONS_PATH,
            JsonContent.Create(new
            {
                level = input.Level,
                kind = input.Kind,
                countryCode = input.CountryCode,
                subdivisionCode = input.SubdivisionCode,
                code = input.Code,
                name = input.Name,
                asciiName = input.AsciiName,
                localName = input.LocalName,
                latitude = input.Latitude,
                longitude = input.Longitude,
                population = input.Population,
                parentId = input.ParentId
            }),
            cancellationToken);

    public Task<HttpResponseMessage> UpdateAsync(
        Guid id,
        GeoDivisionInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Put,
            $"{GEO_DIVISIONS_PATH}/{id}",
            JsonContent.Create(new
            {
                name = input.Name,
                asciiName = input.AsciiName,
                localName = input.LocalName,
                code = input.Code,
                latitude = input.Latitude,
                longitude = input.Longitude,
                population = input.Population,
                parentId = input.ParentId,
                kind = input.Kind
            }),
            cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{GEO_DIVISIONS_PATH}/{id}", null, cancellationToken);

    public Task<HttpResponseMessage> GetTranslationsAsync(
        Guid id,
        string culture,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, TranslationPath(id, culture), null, cancellationToken);

    public Task<HttpResponseMessage> SaveTranslationsAsync(
        Guid id,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Put,
            TranslationPath(id, culture),
            JsonContent.Create(new { values }),
            cancellationToken);

    public Task<HttpResponseMessage> DeleteTranslationsAsync(
        Guid id,
        string culture,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, TranslationPath(id, culture), null, cancellationToken);

    private static string TranslationPath(Guid id, string culture) =>
        $"{GEO_DIVISIONS_PATH}/{id}/{GatewayFields.TRANSLATIONS}/{Uri.EscapeDataString(culture)}";
}
