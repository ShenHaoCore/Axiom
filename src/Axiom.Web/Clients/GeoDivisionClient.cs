// 使用当前 SSO 会话的访问令牌调用行政区网关。
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Axiom.Web.Models;

namespace Axiom.Web.Clients;

public sealed class GeoDivisionClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
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
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (level.HasValue) query.Add($"level={level}");
        if (!string.IsNullOrWhiteSpace(countryCode)) query.Add($"countryCode={Uri.EscapeDataString(countryCode)}");
        if (!string.IsNullOrWhiteSpace(subdivisionCode)) query.Add($"subdivisionCode={Uri.EscapeDataString(subdivisionCode)}");
        if (!string.IsNullOrWhiteSpace(q)) query.Add($"q={Uri.EscapeDataString(q)}");
        return SendAsync(HttpMethod.Get, $"{GEO_DIVISIONS_PATH}/?{string.Join('&', query)}", null, cancellationToken);
    }

    public Task<HttpResponseMessage> GetTreeAsync(
        string countryCode,
        int depth,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Get,
            $"{GEO_DIVISIONS_PATH}/tree?countryCode={Uri.EscapeDataString(countryCode)}&depth={depth}",
            null,
            cancellationToken);

    public Task<HttpResponseMessage> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"{GEO_DIVISIONS_PATH}/{parentId}/children", null, cancellationToken);

    public Task<HttpResponseMessage> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"{GEO_DIVISIONS_PATH}/{id}", null, cancellationToken);

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
        $"{GEO_DIVISIONS_PATH}/{id}/translations/{Uri.EscapeDataString(culture)}";

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
