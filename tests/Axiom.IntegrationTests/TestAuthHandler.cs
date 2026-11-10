// 在集成测试中按请求头生成可控的权限声明。
using System.Security.Claims;
using System.Text.Encodings.Web;
using Axiom.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axiom.IntegrationTests;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SCHEME = "IntegrationTest";
    public const string PERMISSIONS_HEADER = "X-Test-Permissions";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(PERMISSIONS_HEADER, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var permissions = values.ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var claims = permissions
            .Select(permission => new Claim(AxiomClaimTypes.PERMISSION, permission))
            .ToList();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, "integration-test-user"));

        var identity = new ClaimsIdentity(claims, SCHEME);
        var properties = new AuthenticationProperties();
        var accessToken = permissions.Contains(ReferenceDataPermissions.MANAGE)
            ? "test-access-token"
            : "test-read-access-token";
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = accessToken }]);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), properties, SCHEME);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
