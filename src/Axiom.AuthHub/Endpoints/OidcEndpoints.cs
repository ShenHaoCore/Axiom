// 实现 OpenIddict 授权码与 PKCE 流程的应用侧端点。
using System.Security.Claims;
using Axiom.SharedKernel.Security;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Axiom.AuthHub.Endpoints;

public static class OidcEndpoints
{
    public static void MapOidcEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/connect/authorize", AuthorizeAsync).AllowAnonymous();
        app.MapPost("/connect/token", TokenAsync).AllowAnonymous();
    }

    private static async Task<IResult> AuthorizeAsync(
        HttpContext httpContext,
        UserManager<IdentityUser> userManager,
        CancellationToken cancellationToken)
    {
        var request = httpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("无法读取 OIDC 授权请求。");
        var authenticateResult = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (authenticateResult.Principal is null)
        {
            return ChallengeToLogin(httpContext);
        }

        var user = await userManager.GetUserAsync(authenticateResult.Principal);
        if (user is null)
        {
            await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return ChallengeToLogin(httpContext);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name,
            Claims.Role);
        identity.AddClaim(new Claim(Claims.Subject, user.Id)
            .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));
        identity.AddClaim(new Claim(Claims.Name, user.UserName ?? user.Id)
            .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));

        var userClaims = await userManager.GetClaimsAsync(user);
        foreach (var claim in userClaims.Where(claim => claim.Type == AxiomClaimTypes.PERMISSION))
        {
            identity.AddClaim(new Claim(AxiomClaimTypes.PERMISSION, claim.Value)
                .SetDestinations(Destinations.AccessToken));
        }

        var scopes = request.GetScopes();
        var resources = new List<string>();
        if (scopes.Contains(ReferenceDataPermissions.READ) || scopes.Contains(ReferenceDataPermissions.MANAGE))
        {
            resources.Add("axiom-reference-data");
        }

        if (scopes.Contains(NewsPermissions.READ) || scopes.Contains(NewsPermissions.MANAGE))
        {
            resources.Add("axiom-news");
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        principal.SetResources(resources);
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> TokenAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        cancellationToken.ThrowIfCancellationRequested();
        if (result.Principal is null)
        {
            return Results.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        return Results.SignIn(
            result.Principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IResult ChallengeToLogin(HttpContext httpContext) =>
        Results.Challenge(
            new AuthenticationProperties { RedirectUri = httpContext.Request.GetEncodedPathAndQuery() },
            [IdentityConstants.ApplicationScheme]);
}
