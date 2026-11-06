// 提供登录、登出与当前用户信息，所有响应保持 JSON 语义。
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Antiforgery;

namespace Axiom.Bff.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/bff");

        group.MapGet("/login", async (HttpContext context, string? returnUrl) =>
        {
            await context.ChallengeAsync(
                OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties { RedirectUri = NormalizeReturnUrl(returnUrl) });
        }).AllowAnonymous();

        group.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .AddEndpointFilter<RequireAntiforgeryFilter>();

        group.MapGet("/me", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            var name = context.User.Identity?.Name ?? "unknown";
            return Results.Ok(new { authenticated = true, name, csrfToken = tokens.RequestToken });
        })
        .RequireAuthorization();
    }

    // 只允许站内相对路径，避免登录回跳被用于开放重定向。
    private static string NormalizeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
            ? returnUrl
            : "/app/";
}
