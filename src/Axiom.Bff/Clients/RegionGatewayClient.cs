// 调网关后端的行政区层级接口，附加 Bearer token。
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Axiom.Bff.Clients;

public sealed class RegionGatewayClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task<HttpResponseMessage> ListAsync(
        int page,
        int pageSize,
        int? level,
        string? countryCode,
        string? admin1Code,
        string? q,
        CancellationToken cancellationToken)
    {
        var query = $"page={page}&pageSize={pageSize}";
        if (level.HasValue) query += $"&level={level}";
        if (!string.IsNullOrWhiteSpace(countryCode)) query += $"&countryCode={Uri.EscapeDataString(countryCode)}";
        if (!string.IsNullOrWhiteSpace(admin1Code)) query += $"&admin1Code={Uri.EscapeDataString(admin1Code)}";
        if (!string.IsNullOrWhiteSpace(q)) query += $"&q={Uri.EscapeDataString(q)}";
        return SendAsync(HttpMethod.Get, $"/api/regions/?{query}", null, cancellationToken);
    }

    public Task<HttpResponseMessage> GetTreeAsync(
        string countryCode,
        int depth,
        CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, $"/api/regions/tree?countryCode={Uri.EscapeDataString(countryCode)}&depth={depth}", null, cancellationToken);

    public Task<HttpResponseMessage> GetAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, $"/api/regions/{id}", null, cancellationToken);

    public Task<HttpResponseMessage> CreateAsync<T>(T input, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, "/api/regions/", CreateContent(input), cancellationToken);

    public Task<HttpResponseMessage> UpdateAsync<T>(Guid id, T input, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, $"/api/regions/{id}", CreateContent(input), cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, $"/api/regions/{id}", null, cancellationToken);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
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
        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static StringContent CreateContent<T>(T input) =>
        new(JsonSerializer.Serialize(input, SerializerOptions), Encoding.UTF8, "application/json");
}
