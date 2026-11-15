// 启动统一认证中心，提供 Identity 登录和 OIDC 授权码流程。
using System.Security.Cryptography.X509Certificates;
using Axiom.AuthHub.Data;
using Axiom.AuthHub.Endpoints;
using Axiom.AuthHub.Localization;
using Axiom.AuthHub.Resources;
using Axiom.Observability;
using Axiom.SharedKernel.Localization;
using Axiom.SharedKernel.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.EntityFrameworkCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.AddAxiomObservability("Axiom.AuthHub");

// 国际化：登录页按 OIDC 的 ui_locales（Web 语言选择）→ 查询串 → Cookie → Accept-Language 协商。
var localizationOptions = builder.Configuration.GetSection(AxiomLocalizationOptions.SECTION)
    .Get<AxiomLocalizationOptions>() ?? new AxiomLocalizationOptions();
builder.Services.AddSingleton(localizationOptions);
builder.Services.AddLocalization();
builder.Services.AddRequestLocalization(options =>
{
    options.SetDefaultCulture(localizationOptions.DefaultCulture);
    options.AddSupportedCultures(localizationOptions.SupportedCultures);
    options.AddSupportedUICultures(localizationOptions.SupportedCultures);
    options.FallBackToParentCultures = true;
    options.FallBackToParentUICultures = true;
    options.RequestCultureProviders =
    [
        new UiLocalesRequestCultureProvider(),
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});
builder.Services.AddRazorPages()
    .AddDataAnnotationsLocalization(options =>
    {
        // 登录表单模型上的 ErrorMessage / Display(Name) 都是 AuthResource 的资源键。
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(AuthResource));
    });
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
        options.AllowRefreshTokenFlow();
        options.RequireProofKeyForCodeExchange();
        options.RegisterScopes(
            Scopes.OpenId,
            Scopes.Profile,
            Scopes.OfflineAccess,
            ReferenceDataPermissions.READ,
            ReferenceDataPermissions.MANAGE,
            NewsPermissions.READ,
            NewsPermissions.MANAGE);
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));
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
            // 非开发环境从挂载的 PFX 证书加载签名与加密证书，证书密码只通过环境变量注入。
            options.AddSigningCertificate(LoadCertificate(builder.Configuration, "Signing"));
            options.AddEncryptionCertificate(LoadCertificate(builder.Configuration, "Encryption"));
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

app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/Account/Login"));
app.MapRazorPages();
app.MapOidcEndpoints();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name == "database" })
    .AllowAnonymous();

await app.RunAsync();

static X509Certificate2 LoadCertificate(IConfiguration configuration, string usage)
{
    var section = configuration.GetSection($"OpenIddict:Certificates:{usage}");
    var path = section["Path"];
    if (string.IsNullOrWhiteSpace(path))
    {
        throw new InvalidOperationException(
            $"非开发环境必须配置 OpenIddict:Certificates:{usage}:Path（PFX 证书路径）。");
    }

    return X509CertificateLoader.LoadPkcs12FromFile(path, section["Password"]);
}

public partial class Program;
