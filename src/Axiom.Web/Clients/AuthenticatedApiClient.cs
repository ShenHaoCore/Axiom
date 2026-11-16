// 网关 API 客户端基类：统一注入 SSO 访问令牌、语言协商头与 If-Match 并发令牌。
using System.Globalization;
using System.Net.Http.Headers;
using Axiom.Web.Models;
using Microsoft.AspNetCore.Authentication;

namespace Axiom.Web.Clients;

public abstract class AuthenticatedApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    protected async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken,
        string? eTag = null,
        string? acceptLanguage = null)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("当前请求缺少 HTTP 上下文。");
        var accessToken = await httpContext.GetTokenAsync(ApiClientHeaders.AccessToken)
            ?? throw new InvalidOperationException("当前登录会话不包含访问令牌。");

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(ApiClientHeaders.BearerScheme, accessToken);
        request.Headers.TryAddWithoutValidation(
            ApiClientHeaders.AcceptLanguage,
            acceptLanguage ?? CultureInfo.CurrentUICulture.Name);
        if (!string.IsNullOrWhiteSpace(eTag))
        {
            request.Headers.TryAddWithoutValidation(ApiClientHeaders.IfMatch, eTag);
        }

        request.Content = content;
        return await httpClient.SendAsync(request, cancellationToken);
    }
}
