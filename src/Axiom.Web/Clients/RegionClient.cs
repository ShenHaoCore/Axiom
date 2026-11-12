// 使用当前 SSO 会话的访问令牌调用行政区网关：全部按 (country, code) 复合业务键寻址。
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Axiom.Web.Models;

namespace Axiom.Web.Clients;

public sealed class RegionClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    internal const string REGIONS_PATH = "/api/regions";

    public Task<HttpResponseMessage> ListAsync(
        int page,
        int pageSize,
        int? depth,
        string? countryCode,
        string? parentCode,
        string? q,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (depth.HasValue) query.Add($"depth={depth}");
        if (!string.IsNullOrWhiteSpace(countryCode)) query.Add($"countryCode={Uri.EscapeDataString(countryCode)}");
        if (!string.IsNullOrWhiteSpace(parentCode)) query.Add($"parentCode={Uri.EscapeDataString(parentCode)}");
        if (!string.IsNullOrWhiteSpace(q)) query.Add($"q={Uri.EscapeDataString(q)}");
        return SendAsync(HttpMethod.Get, $"{REGIONS_PATH}/?{string.Join('&', query)}", null, cancellationToken);
    }

    public Task<HttpResponseMessage> GetTreeAsync(
        string countryCode,
        int depth,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Get,
            $"{REGIONS_PATH}/tree?countryCode={Uri.EscapeDataString(countryCode)}&depth={depth}",
            null,
            cancellationToken);

    public Task<HttpResponseMessage> GetChildrenAsync(
        string countryCode,
        string code,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, ItemPath(countryCode, code) + "/children", null, cancellationToken);

    public Task<HttpResponseMessage> GetAsync(
        string countryCode,
        string code,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, ItemPath(countryCode, code), null, cancellationToken);

    public Task<HttpResponseMessage> CreateAsync(RegionInput input, CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            REGIONS_PATH,
            JsonContent.Create(new
            {
                depth = input.Depth,
                countryCode = input.CountryCode,
                code = input.Code,
                parentCode = input.ParentCode,
                type = input.Type,
                name = input.Name,
                asciiName = input.AsciiName,
                localName = input.LocalName,
                latitude = input.Latitude,
                longitude = input.Longitude,
                population = input.Population
            }),
            cancellationToken);

    public Task<HttpResponseMessage> UpdateAsync(
        string countryCode,
        string code,
        RegionInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Put,
            ItemPath(countryCode, code),
            JsonContent.Create(new
            {
                name = input.Name,
                asciiName = input.AsciiName,
                localName = input.LocalName,
                latitude = input.Latitude,
                longitude = input.Longitude,
                population = input.Population,
                codeIso = input.CodeIso
            }),
            cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(
        string countryCode,
        string code,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, ItemPath(countryCode, code), null, cancellationToken);

    public Task<HttpResponseMessage> GetTranslationsAsync(
        string countryCode,
        string code,
        string culture,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, TranslationPath(countryCode, code, culture), null, cancellationToken);

    public Task<HttpResponseMessage> SaveTranslationsAsync(
        string countryCode,
        string code,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Put,
            TranslationPath(countryCode, code, culture),
            JsonContent.Create(new { values }),
            cancellationToken);

    public Task<HttpResponseMessage> DeleteTranslationsAsync(
        string countryCode,
        string code,
        string culture,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, TranslationPath(countryCode, code, culture), null, cancellationToken);

    private static string ItemPath(string countryCode, string code) =>
        $"{REGIONS_PATH}/{Uri.EscapeDataString(countryCode)}/{Uri.EscapeDataString(code)}";

    private static string TranslationPath(string countryCode, string code, string culture) =>
        $"{ItemPath(countryCode, code)}/translations/{Uri.EscapeDataString(culture)}";

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
