// 启动统一认证中心，提供 Identity 登录和 OIDC 授权码流程。
using Axiom.AuthHub.Data;
using Axiom.AuthHub.Endpoints;
using Axiom.Observability;
using Axiom.SharedKernel.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.AddAxiomObservability("Axiom.AuthHub");
builder.Services.AddRazorPages();
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Auth"))
        .UseOpenIddict());

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/Account/Login";
});

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AuthDbContext>())
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(builder.Configuration["OpenIddict:Issuer"] ?? "http://localhost:5101"));
        options.SetAuthorizationEndpointUris("connect/authorize");
        options.SetTokenEndpointUris("connect/token");
        options.AllowAuthorizationCodeFlow();
        options.RequireProofKeyForCodeExchange();
        options.RegisterScopes(
            Scopes.OpenId,
            Scopes.Profile,
            ReferenceDataPermissions.READ,
            ReferenceDataPermissions.MANAGE,
            NewsPermissions.READ,
            NewsPermissions.MANAGE);
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.DisableAccessTokenEncryption();

        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate();
            options.AddDevelopmentSigningCertificate();
            options.UseAspNetCore()
                .EnableAuthorizationEndpointPassthrough()
                .EnableTokenEndpointPassthrough()
                .DisableTransportSecurityRequirement();
        }
        else
        {
            throw new InvalidOperationException("非开发环境必须配置 OpenIddict 签名和加密证书。");
        }

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough();
    });

builder.Services.AddAuthorization();
builder.Services.AddHealthChecks().AddDbContextCheck<AuthDbContext>("database");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await AuthDataSeeder.SeedAsync(scope.ServiceProvider, builder.Configuration);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/Account/Login"));
app.MapRazorPages();
app.MapOidcEndpoints();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name == "database" })
    .AllowAnonymous();

await app.RunAsync();

public partial class Program;
