// 使用当前 SSO 会话的访问令牌调用行政区网关。
using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Axiom.Web.Models;

namespace Axiom.Web.Clients;

public sealed class RegionClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    private const string REGIONS_PATH = "/api/regions";

    public Task<HttpResponseMessage> ListAsync(
        int page,
        int pageSize,
        int? level,
        string? countryCode,
        string? admin1Code,
        string? q,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (level.HasValue) query.Add($"level={level}");
        if (!string.IsNullOrWhiteSpace(countryCode)) query.Add($"countryCode={Uri.EscapeDataString(countryCode)}");
        if (!string.IsNullOrWhiteSpace(admin1Code)) query.Add($"admin1Code={Uri.EscapeDataString(admin1Code)}");
        if (!string.IsNullOrWhiteSpace(q)) query.Add($"q={Uri.EscapeDataString(q)}");
        return SendAsync(HttpMethod.Get, $"{REGIONS_PATH}/?{string.Join('&', query)}", cancellationToken);
    }

    public Task<HttpResponseMessage> GetTreeAsync(
        string countryCode,
        int depth,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Get,
            $"{REGIONS_PATH}/tree?countryCode={Uri.EscapeDataString(countryCode)}&depth={depth}",
            cancellationToken);

    public Task<HttpResponseMessage> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"{REGIONS_PATH}/{id}", cancellationToken);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("当前请求缺少 HTTP 上下文。");
        var accessToken = await httpContext.GetTokenAsync("access_token")
            ?? throw new InvalidOperationException("当前登录会话不包含访问令牌。");

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Accept-Language", CultureInfo.CurrentUICulture.Name);
        return await httpClient.SendAsync(request, cancellationToken);
    }
}
