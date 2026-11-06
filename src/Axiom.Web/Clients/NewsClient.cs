// 使用当前 SSO 会话的访问令牌调用新闻网关。
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Axiom.Web.Models;

namespace Axiom.Web.Clients;

public sealed class NewsClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    private const string ARTICLES_PATH = "/api/news/articles";

    public Task<HttpResponseMessage> ListAsync(
        string? status,
        int page,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"page={page}" };
        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status)}");
        }

        return SendAsync(HttpMethod.Get, $"{ARTICLES_PATH}?{string.Join('&', query)}", null, null, cancellationToken);
    }

    public Task<HttpResponseMessage> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"{ARTICLES_PATH}/{id}", null, null, cancellationToken);

    public Task<HttpResponseMessage> CreateAsync(NewsInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, ARTICLES_PATH, JsonContent.Create(ToPayload(input)), null, cancellationToken);

    public Task<HttpResponseMessage> UpdateAsync(
        Guid id,
        string eTag,
        NewsInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"{ARTICLES_PATH}/{id}", JsonContent.Create(ToPayload(input)), eTag, cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(
        Guid id,
        string eTag,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{ARTICLES_PATH}/{id}", null, eTag, cancellationToken);

    private static object ToPayload(NewsInput input) => new
    {
        title = input.Title,
        summary = input.Summary,
        content = input.Content,
        category = input.Category,
        author = input.Author,
        status = input.Status
    };

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        string? eTag,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("当前请求缺少 HTTP 上下文。");
        var accessToken = await httpContext.GetTokenAsync("access_token")
            ?? throw new InvalidOperationException("当前登录会话不包含访问令牌。");

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (eTag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", eTag);
        }

        request.Content = content;
        return await httpClient.SendAsync(request, cancellationToken);
    }
}
