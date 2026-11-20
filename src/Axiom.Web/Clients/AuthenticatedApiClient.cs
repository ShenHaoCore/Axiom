// 网关 API 客户端基类：统一注入 SSO 访问令牌、语言协商头与 If-Match 并发令牌，
// 并提供 GetAsync 重载、翻译子资源的标准路径与读写方法——子类只需声明 EntityPath 和差异化方法。
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Axiom.Web.Models;
using Microsoft.AspNetCore.Authentication;

namespace Axiom.Web.Clients;

public abstract class AuthenticatedApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    /// <summary>子类提供的实体根路径，如 "/api/reference-data/airports"。</summary>
    protected abstract string EntityPath { get; }

    // ---------- 统一的 HTTP 发送 ----------

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
        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 调用方主动取消：原样抛出，由调用管线处理。
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException
                or TaskCanceledException // HttpClient 超时也以 TaskCanceledException 形态出现
                or ProtocolViolationException)
        {
            // 传输层故障（网关宕机/网络中断/超时）：统一转成可识别异常，
            // 由 GatewayUnavailablePageFilter 呈现内联错误，而不是丢给异常页丢表单。
            throw new GatewayUnavailableException($"网关调用失败：{method.Method} {path}", exception);
        }
    }

    // ---------- 统一的实体读取 ----------

    /// <summary>读取实体；覆盖 Accept-Language 时用于取默认语言原文（绕开界面语言本地化）。</summary>
    public Task<HttpResponseMessage> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync(id, culture: null, cancellationToken);

    /// <summary>读取实体；culture 非空时覆盖 Accept-Language，用于取默认语言原文。</summary>
    public Task<HttpResponseMessage> GetAsync(Guid id, string? culture, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"{EntityPath}/{id}", null, cancellationToken, acceptLanguage: culture);

    // ---------- 统一的翻译子资源 ----------

    protected static string BuildTranslationPath(string entityPath, Guid id, string culture) =>
        $"{entityPath}/{id}/{GatewayFields.TRANSLATIONS}/{Uri.EscapeDataString(culture)}";

    public Task<HttpResponseMessage> GetTranslationsAsync(
        Guid id,
        string culture,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, BuildTranslationPath(EntityPath, id, culture), null, cancellationToken);

    public Task<HttpResponseMessage> SaveTranslationsAsync(
        Guid id,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Put,
            BuildTranslationPath(EntityPath, id, culture),
            JsonContent.Create(new { values }),
            cancellationToken);

    public Task<HttpResponseMessage> DeleteTranslationsAsync(
        Guid id,
        string culture,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, BuildTranslationPath(EntityPath, id, culture), null, cancellationToken);
}
