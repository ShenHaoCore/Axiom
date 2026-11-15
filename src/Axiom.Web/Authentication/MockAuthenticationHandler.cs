// 仅开发环境启用：跳过 SSO 登录，直接注入模拟用户与可配置的访问令牌。
using System.Security.Claims;
using System.Text.Encodings.Web;
using Axiom.Web.Clients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Axiom.Web.Authentication;

public sealed class MockAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SCHEME = "Mock";
    private const string DEFAULT_ACCESS_TOKEN = "dev-access-token";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "dev-user"),
                new Claim("name", "开发用户"),
                new Claim("preferred_username", "dev-user")
            ],
            SCHEME,
            "name",
            "role");

        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken
            {
                Name = ApiClientHeaders.AccessToken,
                Value = configuration["MockAuth:AccessToken"] is { Length: > 0 } token
                    ? token
                    : DEFAULT_ACCESS_TOKEN
            }
        ]);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), properties, SCHEME);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
