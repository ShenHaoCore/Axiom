// 启动 Web 客户端，处理 SSO 会话并通过网关访问基础数据。
using Axiom.Observability;
using Axiom.SharedKernel.Security;
using Axiom.Web;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.AddAxiomObservability("Axiom.Web");
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ReferenceDataClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["ApiGateway:BaseAddress"] ?? "http://localhost:5100"));
builder.Services.AddHttpClient<NewsClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["ApiGateway:BaseAddress"] ?? "http://localhost:5100"));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    })
    .AddOpenIdConnect(options =>
    {
        var authority = (builder.Configuration["Oidc:Authority"] ?? "http://localhost:5101").TrimEnd('/');
        var issuer = builder.Configuration["Oidc:ValidIssuer"] ?? $"{authority}/";
        options.Authority = authority;
        options.ClientId = builder.Configuration["Oidc:ClientId"] ?? "axiom-web";
        options.CallbackPath = "/callback";
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment()
            && !builder.Environment.IsEnvironment("Testing");
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.ValidIssuer = issuer;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
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
builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
    .RequireAuthorization()
    .WithStaticAssets();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();

await app.RunAsync();
