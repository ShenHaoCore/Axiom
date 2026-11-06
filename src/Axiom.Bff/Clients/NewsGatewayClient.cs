// 服务端携带访问令牌调用网关上的新闻接口，原样透传 ETag 与 If-Match。
using System.Text;
using System.Text.Json;
using Axiom.Bff.Contracts;
using Microsoft.AspNetCore.Authentication;

namespace Axiom.Bff.Clients;

public sealed class NewsGatewayClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task<HttpResponseMessage> ListAsync(
        int page,
        int pageSize,
        string? status,
        CancellationToken cancellationToken)
    {
        var path = $"/api/news/articles?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(status))
        {
            path += $"&status={Uri.EscapeDataString(status)}";
        }

        return SendAsync(HttpMethod.Get, path, ifMatch: null, content: null, cancellationToken);
    }

    public Task<HttpResponseMessage> GetAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, $"/api/news/articles/{id}", ifMatch: null, content: null, cancellationToken);

    public Task<HttpResponseMessage> CreateAsync(NewsInput input, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, "/api/news/articles", ifMatch: null, content: CreateContent(input), cancellationToken);

    public Task<HttpResponseMessage> UpdateAsync(
        Guid id,
        string? ifMatch,
        NewsInput input,
        CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, $"/api/news/articles/{id}", ifMatch, CreateContent(input), cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(Guid id, string? ifMatch, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, $"/api/news/articles/{id}", ifMatch, content: null, cancellationToken);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? ifMatch,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var accessToken = await httpContextAccessor.HttpContext!.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("当前会话缺少访问令牌。");
        }

        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static StringContent CreateContent(NewsInput input) =>
        new(JsonSerializer.Serialize(input, SerializerOptions), Encoding.UTF8, "application/json");
}
