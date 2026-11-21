// 启动 Web 客户端，处理 SSO 会话并通过网关访问基础数据。
using Axiom.Observability;
using Axiom.SharedKernel.Localization;
using Axiom.Web.Authentication;
using Axiom.Web.Clients;
using Axiom.Web.Health;
using Axiom.Web.Infrastructure;
using Axiom.Web.Mocking;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

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
// 全局页面过滤器：网关传输层故障（宕机/超时）转成页面内错误——POST 保留表单，GET 走错误页。
builder.Services.Configure<MvcOptions>(options =>
{
    options.Filters.Add<GatewayUnavailablePageFilter>();
});
builder.Services.AddHttpContextAccessor();

// Data Protection：认证 Cookie 与防伪造令牌的密钥持久化到磁盘（容器内挂卷），
// 重启不掉线、多副本可共享；路径可按部署环境覆盖。
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
        ? null
        : "/keys");
if (dataProtectionKeysPath is not null)
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

// 开发环境可通过 MockAuth:Enabled 跳过 SSO 登录，并用示例数据替代网关调用。
var useMockAuth = builder.Environment.IsDevelopment() && builder.Configuration.GetValue("MockAuth:Enabled", false);

builder.Services.AddTransient<MockGatewayHandler>();
// 网关地址只读一次：配置缺失时回退到本地网关默认端口。
// 统一超时与韧性策略见 GatewayResilienceExtensions（10s 超时 + 有界重试，禁用写操作并发竞速）。
var gatewayAddress = new Uri(builder.Configuration["ApiGateway:BaseAddress"] ?? "http://localhost:5100");
var referenceDataClient = builder.Services.AddAxiomGatewayDefaults<ReferenceDataClient>(gatewayAddress);
var newsClient = builder.Services.AddAxiomGatewayDefaults<NewsClient>(gatewayAddress);
var regionClient = builder.Services.AddAxiomGatewayDefaults<GeoDivisionClient>(gatewayAddress);
var enumClient = builder.Services.AddAxiomGatewayDefaults<ReferenceEnumClient>(gatewayAddress);
if (useMockAuth)
{
    referenceDataClient.AddHttpMessageHandler<MockGatewayHandler>();
    newsClient.AddHttpMessageHandler<MockGatewayHandler>();
    regionClient.AddHttpMessageHandler<MockGatewayHandler>();
    enumClient.AddHttpMessageHandler<MockGatewayHandler>();
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

// 404 等非异常状态码也走 /Error 页渲染：此前 404 返回完全空白页（Content-Length: 0），用户被困住。
// 例外：/health/* 等基础设施路径由后续短路中间件保持原状（状态码页只接管无响应体的状态码）。
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().RequireAuthorization().WithStaticAssets();

// 语言切换：写入 ASP.NET Core 约定的语言 Cookie 后回到原页面。
app.MapGet("/culture", (HttpContext context, string culture, string? returnUrl) =>
{
    if (!localizationOptions.IsSupported(culture))
    {
        // 带响应体的 Problem：空体 400 会被状态码页接管重执行 /Error，这里直接给可读错误。
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: $"Unsupported UI culture: {culture}.");
    }
    context.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)), new CookieOptions
    {
        HttpOnly = true,
        IsEssential = true,
        MaxAge = TimeSpan.FromDays(365),
        Path = "/",
        SameSite = SameSiteMode.Lax,
        Secure = context.Request.IsHttps
    });
    var target = returnUrl is { Length: > 0 } && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal) ? returnUrl : "/";
    return Results.LocalRedirect(target);
}).AllowAnonymous();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name == "authhub" }).AllowAnonymous();

await app.RunAsync();
