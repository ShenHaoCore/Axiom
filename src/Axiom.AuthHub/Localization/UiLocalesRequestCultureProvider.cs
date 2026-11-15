// 从 OIDC 的 ui_locales 参数读取请求语言。
// 未登录时登录页地址形如 /Account/Login?ReturnUrl=<编码后的授权地址>，ui_locales 保存在 ReturnUrl 里。
using Axiom.SharedKernel.Localization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.WebUtilities;

namespace Axiom.AuthHub.Localization;

public sealed class UiLocalesRequestCultureProvider : IRequestCultureProvider
{
    private const string UI_LOCALES = "ui_locales";
    private const string RETURN_URL = "ReturnUrl";

    public Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var culture = FirstCulture(httpContext.Request.Query[UI_LOCALES])
                      ?? FirstCulture(ReadReturnUrlUiLocales(httpContext.Request.Query[RETURN_URL].ToString()));
        return Task.FromResult(culture is null ? null : new ProviderCultureResult(culture, culture));
    }

    /// <summary>ui_locales 允许用空格分隔多个语言，取第一个能规范化的值。</summary>
    private static string? FirstCulture(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var candidate in value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            if (AxiomCultureResolver.TryNormalize(candidate) is { } normalized)
            {
                return normalized;
            }
        }

        return null;
    }

    private static string ReadReturnUrlUiLocales(string returnUrl)
    {
        var separator = returnUrl.IndexOf('?');
        if (separator < 0)
        {
            return string.Empty;
        }

        return QueryHelpers.ParseQuery(returnUrl[separator..]).TryGetValue(UI_LOCALES, out var value)
            ? value.ToString()
            : string.Empty;
    }
}
