// 启动 Web 客户端，处理 SSO 会话并通过网关访问基础数据。
using Axiom.Observability;
using Axiom.SharedKernel.Localization;
using Axiom.Web.Authentication;
using Axiom.Web.Clients;
using Axiom.Web.Health;
using Axiom.Web.Mocking;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.AddAxiomObservability("Axiom.Web");

// 国际化：界面文案与校验消息按请求语言协商（显式选择 → Accept-Language → 默认语言）。
var localizationOptions = builder.Configuration.GetSection(AxiomLocalizationOptions.SECTION).Get<AxiomLocalizationOptions>() ?? new AxiomLocalizationOptions();
// 配置绑定会把配置项追加到带初始值的数组中，去重避免语言列表重复。
localizationOptions.SupportedCultures = localizationOptions.SupportedCultures.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
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
builder.Services.AddRazorPages().AddDataAnnotationsLocalization(options =>
{
    // 表单模型上的 ErrorMessage / Display(Name) 都是 SharedResource 的资源键。
    options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource));
});
builder.Services.AddHttpContextAccessor();

// 开发环境可通过 MockAuth:Enabled 跳过 SSO 登录，并用示例数据替代网关调用。
var useMockAuth = builder.Environment.IsDevelopment() && builder.Configuration.GetValue("MockAuth:Enabled", false);

// 网关地址只读一次：配置缺失时回退到本地网关默认端口。
builder.Services.AddTransient<MockGatewayHandler>();
var gatewayAddress = new Uri(builder.Configuration["ApiGateway:BaseAddress"] ?? "http://localhost:5100");
var referenceDataClient = builder.Services.AddHttpClient<ReferenceDataClient>(client => client.BaseAddress = gatewayAddress);
var newsClient = builder.Services.AddHttpClient<NewsClient>(client => client.BaseAddress = gatewayAddress);
var regionClient = builder.Services.AddHttpClient<RegionClient>(client => client.BaseAddress = gatewayAddress);
if (useMockAuth)
{
    referenceDataClient.AddHttpMessageHandler<MockGatewayHandler>();
    newsClient.AddHttpMessageHandler<MockGatewayHandler>();
    regionClient.AddHttpMessageHandler<MockGatewayHandler>();
}
builder.AddWebAuthentication(useMockAuth);
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
app.MapRazorPages().RequireAuthorization().WithStaticAssets();

// 语言切换：写入 ASP.NET Core 约定的语言 Cookie 后回到原页面。
app.MapGet("/culture", (HttpContext context, string culture, string? returnUrl) =>
{
    if (!localizationOptions.IsSupported(culture)) { return Results.BadRequest(); }
    context.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)), new CookieOptions
    {
        HttpOnly = true,
        IsEssential = true,
        MaxAge = TimeSpan.FromDays(365),
        Path = "/",
        SameSite = SameSiteMode.Lax,
        Secure = context.Request.IsHttps
    });
    var target = returnUrl is { Length: > 0 } && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") ? returnUrl : "/";
    return Results.LocalRedirect(target);
}).AllowAnonymous();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name == "authhub" }).AllowAnonymous();

await app.RunAsync();
