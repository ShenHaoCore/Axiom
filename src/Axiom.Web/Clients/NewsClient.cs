// 使用当前 SSO 会话的访问令牌调用新闻网关。
using System.Net.Http.Json;
using Axiom.Web.Models;

namespace Axiom.Web.Clients;

public sealed class NewsClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    : AuthenticatedApiClient(httpClient, httpContextAccessor)
{
    internal const string ARTICLES_PATH = "/api/news/articles";

    public Task<HttpResponseMessage> ListAsync(
        string? status,
        int page,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"{GatewayFields.PAGE}={page}" };
        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"{GatewayFields.STATUS}={Uri.EscapeDataString(status)}");
        }

        return SendAsync(HttpMethod.Get, $"{ARTICLES_PATH}?{string.Join('&', query)}", null, cancellationToken);
    }

    public Task<HttpResponseMessage> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync(id, culture: null, cancellationToken);

    /// <summary>读取新闻；culture 非空时覆盖 Accept-Language，用于取默认语言原文。</summary>
    public Task<HttpResponseMessage> GetAsync(
        Guid id,
        string? culture,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"{ARTICLES_PATH}/{id}", null, cancellationToken, acceptLanguage: culture);

    public Task<HttpResponseMessage> CreateAsync(NewsInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, ARTICLES_PATH, JsonContent.Create(ToPayload(input)), cancellationToken);

    public Task<HttpResponseMessage> UpdateAsync(
        Guid id,
        string eTag,
        NewsInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Put,
            $"{ARTICLES_PATH}/{id}",
            JsonContent.Create(ToPayload(input)),
            cancellationToken,
            eTag);

    public Task<HttpResponseMessage> DeleteAsync(
        Guid id,
        string eTag,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{ARTICLES_PATH}/{id}", null, cancellationToken, eTag);

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
        $"{ARTICLES_PATH}/{id}/{GatewayFields.TRANSLATIONS}/{Uri.EscapeDataString(culture)}";

    private static object ToPayload(NewsInput input) => new
    {
        title = input.Title,
        summary = input.Summary,
        content = input.Content,
        category = input.Category,
        author = input.Author,
        status = input.Status
    };
}
