// 启动 Web 客户端，处理 SSO 会话并通过网关访问基础数据。
using Axiom.Observability;
using Axiom.SharedKernel.Localization;
using Axiom.SharedKernel.Security;
using Axiom.Web.Authentication;
using Axiom.Web.Clients;
using Axiom.Web.Health;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.AddAxiomObservability("Axiom.Web");

// 国际化：界面文案与校验消息按请求语言协商（显式选择 → Accept-Language → 默认语言）。
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
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});
builder.Services.AddRazorPages()
    .AddDataAnnotationsLocalization(options =>
    {
        // 表单模型上的 ErrorMessage / Display(Name) 都是 SharedResource 的资源键。
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource));
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ReferenceDataClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["ApiGateway:BaseAddress"] ?? "http://localhost:5100"));
builder.Services.AddHttpClient<NewsClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["ApiGateway:BaseAddress"] ?? "http://localhost:5100"));
builder.Services.AddHttpClient<RegionClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["ApiGateway:BaseAddress"] ?? "http://localhost:5100"));
builder.Services.AddScoped<RefreshCookieEvents>();

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
        options.EventsType = typeof(RefreshCookieEvents);
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

app.UseRequestLocalization();

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

// 语言切换：写入 ASP.NET Core 约定的语言 Cookie 后回到原页面。
app.MapGet("/culture", (HttpContext context, string culture, string? returnUrl) =>
{
    if (!localizationOptions.IsSupported(culture))
    {
        return Results.BadRequest();
    }

    context.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(365),
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps
        });

    var target = returnUrl is { Length: > 0 }
                 && returnUrl.StartsWith('/')
                 && !returnUrl.StartsWith("//")
        ? returnUrl
        : "/";
    return Results.LocalRedirect(target);
}).AllowAnonymous();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name == "authhub" })
    .AllowAnonymous();

await app.RunAsync();
