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
        foreach (var client in ResolveClients(configuration))
        {
            var application = await applicationManager.FindByClientIdAsync(client.ClientId, cancellationToken);
            if (application is null)
            {
                await applicationManager.CreateAsync(CreateClientDescriptor(client), cancellationToken);
                continue;
            }

            var descriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(descriptor, application, cancellationToken);
            if (EnsureDescriptor(descriptor, client))
            {
                await applicationManager.UpdateAsync(application, descriptor, cancellationToken);
            }
        }

        await SeedUserAsync(services, configuration);
    }

    // 支持 Oidc:Clients 数组配置多客户端，同时兼容旧的单客户端配置键。
    private static IReadOnlyList<OidcClientConfiguration> ResolveClients(IConfiguration configuration)
    {
        var configured = configuration.GetSection("Oidc:Clients").Get<OidcClientConfiguration[]>();
        if (configured is { Length: > 0 })
        {
            return configured;
        }

        var clientId = configuration["Oidc:ClientId"] ?? "axiom-web";
        var redirectUris = configuration.GetSection("Oidc:RedirectUris").Get<string[]>()
            ?? ["http://localhost:3000/callback"];
        return
        [
            new OidcClientConfiguration
            {
                ClientId = clientId,
                DisplayName = "Axiom Web",
                RedirectUris = redirectUris
            }
        ];
    }

    private static OpenIddictApplicationDescriptor CreateClientDescriptor(OidcClientConfiguration client)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = client.DisplayName ?? client.ClientId
        };

        foreach (var redirectUri in client.RedirectUris)
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

    // 已有客户端需要补齐续期权限与新增回调地址，避免重建数据库才能生效。
    private static bool EnsureDescriptor(
        OpenIddictApplicationDescriptor descriptor,
        OidcClientConfiguration client)
    {
        var changed = false;
        var requiredPermissions = new[]
        {
            Permissions.GrantTypes.RefreshToken,
            Permissions.Prefixes.Scope + Scopes.OfflineAccess
        };
        foreach (var permission in requiredPermissions.Where(permission => !descriptor.Permissions.Contains(permission)))
        {
            descriptor.Permissions.Add(permission);
            changed = true;
        }

        foreach (var redirectUri in client.RedirectUris.Select(uri => new Uri(uri)))
        {
            if (descriptor.RedirectUris.All(existing => existing != redirectUri))
            {
                descriptor.RedirectUris.Add(redirectUri);
                changed = true;
            }
        }

        return changed;
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

// 描述一个需要由 AuthHub 初始化或补齐的 OIDC 客户端。
public sealed class OidcClientConfiguration
{
    public string ClientId { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string[] RedirectUris { get; set; } = [];
}
