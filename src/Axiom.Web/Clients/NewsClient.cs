// 使用当前 SSO 会话的访问令牌调用新闻网关。
using System.Net.Http.Json;
using Axiom.Web.Models;

namespace Axiom.Web.Clients;

public sealed class NewsClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    : AuthenticatedApiClient(httpClient, httpContextAccessor)
{
    internal const string ARTICLES_PATH = "/api/news/articles";

    protected override string EntityPath => ARTICLES_PATH;

    public Task<HttpResponseMessage> ListAsync(
        string? status,
        int page,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"{GatewayFields.PAGE}={page}" };
        if (pageSize.HasValue)
        {
            query.Add($"{GatewayFields.PAGE_SIZE}={pageSize.Value}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"{GatewayFields.STATUS}={Uri.EscapeDataString(status)}");
        }

        return SendAsync(HttpMethod.Get, $"{ARTICLES_PATH}?{string.Join('&', query)}", null, cancellationToken);
    }

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
