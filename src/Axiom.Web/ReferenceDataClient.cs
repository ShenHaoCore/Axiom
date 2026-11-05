// 使用当前 SSO 会话的访问令牌调用基础数据网关。
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;

namespace Axiom.Web;

public sealed class ReferenceDataClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    private const string AIRPORTS_PATH = "/api/reference-data/airports";

    public Task<HttpResponseMessage> GetAirportsAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, null, cancellationToken);

    public Task<HttpResponseMessage> CreateAirportAsync(
        AirportInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, JsonContent.Create(input), cancellationToken);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("当前请求缺少 HTTP 上下文。");
        var accessToken = await httpContext.GetTokenAsync("access_token")
            ?? throw new InvalidOperationException("当前登录会话不包含访问令牌。");

        using var request = new HttpRequestMessage(method, AIRPORTS_PATH);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;
        return await httpClient.SendAsync(request, cancellationToken);
    }
}
