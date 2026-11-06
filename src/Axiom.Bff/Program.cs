// 启动独立 BFF：持有 OIDC 会话、提供 /bff JSON 接口并托管 SPA 静态资源。
using Axiom.Bff;
using Axiom.Bff.Authentication;
using Axiom.Bff.Clients;
using Axiom.Bff.Endpoints;
using Axiom.Bff.Health;
using Axiom.Observability;
using Axiom.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.AddAxiomObservability("Axiom.Bff");
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<NewsGatewayClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["ApiGateway:BaseAddress"] ?? "http://localhost:5100"));
builder.Services.AddScoped<RefreshCookieEvents>();

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "Axiom.Bff.Csrf";
    options.Cookie.HttpOnly = false;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.Path = "/bff";
    options.HeaderName = "X-CSRF-Token";
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "Axiom.Bff.Session";
        options.Cookie.Path = "/bff";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.EventsType = typeof(RefreshCookieEvents);
    })
    .AddOpenIdConnect(options =>
    {
        var authority = (builder.Configuration["Oidc:Authority"] ?? "http://localhost:5101").TrimEnd('/');
        var issuer = builder.Configuration["Oidc:ValidIssuer"] ?? $"{authority}/";
        options.Authority = authority;
        options.ClientId = builder.Configuration["Oidc:ClientId"] ?? "axiom-bff";
        options.CallbackPath = "/bff/signin-oidc";
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.RequireHttpsMetadata = builder.Configuration.GetValue(
            "Oidc:RequireHttpsMetadata",
            !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"));
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.ValidIssuer = issuer;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("offline_access");
        options.Scope.Add(ReferenceDataPermissions.READ);
        options.Scope.Add(ReferenceDataPermissions.MANAGE);
        options.Scope.Add(NewsPermissions.READ);
        options.Scope.Add(NewsPermissions.MANAGE);

        var backchannelAuthority = builder.Configuration["Oidc:BackchannelAuthority"]?.TrimEnd('/');
        if (!string.IsNullOrEmpty(backchannelAuthority))
        {
            var metadataAddress = $"{backchannelAuthority}/.well-known/openid-configuration";
            options.MetadataAddress = metadataAddress;
            var documentRetriever = new HttpDocumentRetriever { RequireHttps = options.RequireHttpsMetadata };
            options.ConfigurationManager = new PublicAuthorizationEndpointConfigurationManager(
                new ConfigurationManager<OpenIdConnectConfiguration>(
                    metadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    documentRetriever),
                authority);
        }
    });

builder.Services.AddAuthorization();

var healthChecks = builder.Services.AddHealthChecks();
if (!builder.Environment.IsEnvironment("Testing"))
{
    healthChecks.AddCheck<AuthHubHealthCheck>("authhub");
}

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/app/"));
app.MapAuthEndpoints();
app.MapNewsEndpoints();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name == "authhub" })
    .AllowAnonymous();
app.MapFallbackToFile("/app/{*path:nonfile}", "app/index.html");

await app.RunAsync();
