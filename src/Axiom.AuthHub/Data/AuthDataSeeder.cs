// 初始化可重复的 OIDC 客户端和可选演示用户数据。
using Axiom.SharedKernel.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Axiom.AuthHub.Data;

public static class AuthDataSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var dbContext = services.GetRequiredService<AuthDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        var applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var clientId = configuration["Oidc:ClientId"] ?? "axiom-web";
        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            await applicationManager.CreateAsync(CreateClientDescriptor(configuration, clientId), cancellationToken);
        }
        else
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(descriptor, application, cancellationToken);
            if (EnsureRefreshTokenPermissions(descriptor))
            {
                await applicationManager.UpdateAsync(application, descriptor, cancellationToken);
            }
        }

        await SeedUserAsync(services, configuration);
    }

    private static OpenIddictApplicationDescriptor CreateClientDescriptor(
        IConfiguration configuration,
        string clientId)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "Axiom Web"
        };

        var redirectUris = configuration.GetSection("Oidc:RedirectUris").Get<string[]>()
            ?? ["http://localhost:3000/callback"];
        foreach (var redirectUri in redirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(redirectUri));
        }

        descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
        descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
        descriptor.Permissions.Add(Permissions.Prefixes.Scope + Scopes.OpenId);
        descriptor.Permissions.Add(Permissions.Prefixes.Scope + Scopes.OfflineAccess);
        descriptor.Permissions.Add(Permissions.Scopes.Profile);
        descriptor.Permissions.Add(Permissions.Prefixes.Scope + ReferenceDataPermissions.READ);
        descriptor.Permissions.Add(Permissions.Prefixes.Scope + ReferenceDataPermissions.MANAGE);
        descriptor.Permissions.Add(Permissions.Prefixes.Scope + NewsPermissions.READ);
        descriptor.Permissions.Add(Permissions.Prefixes.Scope + NewsPermissions.MANAGE);
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        return descriptor;
    }

    // 已有客户端需要补齐续期权限，避免重建数据库才能生效。
    private static bool EnsureRefreshTokenPermissions(OpenIddictApplicationDescriptor descriptor)
    {
        var required = new[]
        {
            Permissions.GrantTypes.RefreshToken,
            Permissions.Prefixes.Scope + Scopes.OfflineAccess
        };
        var added = false;
        foreach (var permission in required.Where(permission => !descriptor.Permissions.Contains(permission)))
        {
            descriptor.Permissions.Add(permission);
            added = true;
        }

        return added;
    }

    private static async Task SeedUserAsync(IServiceProvider services, IConfiguration configuration)
    {
        var password = configuration["SeedUser:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var userName = configuration["SeedUser:UserName"] ?? "demo@axiom.local";
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = userName,
                Email = userName,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"无法创建演示用户：{errors}");
            }
        }

        var claims = await userManager.GetClaimsAsync(user);
        var permissions = new[] { ReferenceDataPermissions.READ, ReferenceDataPermissions.MANAGE, NewsPermissions.READ, NewsPermissions.MANAGE };
        foreach (var permission in permissions.Where(permission =>
                     claims.All(claim => claim.Type != AxiomClaimTypes.PERMISSION || claim.Value != permission)))
        {
            var result = await userManager.AddClaimAsync(user, new System.Security.Claims.Claim(
                AxiomClaimTypes.PERMISSION,
                permission));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("无法为演示用户分配基础数据权限。");
            }
        }
    }
}
