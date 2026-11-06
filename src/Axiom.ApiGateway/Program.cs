// 启动统一 API 网关，在转发请求前验证访问令牌。
using Axiom.ApiGateway;
using Axiom.ApiGateway.Health;
using Axiom.Contracts.ReferenceData.V1;
using Axiom.Observability;
using Axiom.SharedKernel.Localization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddAxiomObservability("Axiom.ApiGateway");

// 国际化：错误消息按请求语言协商输出，转发时 Bearer/Accept-Language 原样透传。
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
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddGrpcClient<ReferenceDataQuery.ReferenceDataQueryClient>(options =>
    options.Address = new Uri(builder.Configuration["ReferenceDataGrpc:Address"] ?? "http://localhost:5103"));

var authority = builder.Configuration["Authentication:Authority"] ?? "http://localhost:5101";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.RequireHttpsMetadata = builder.Configuration.GetValue(
            "Authentication:RequireHttpsMetadata",
            !builder.Environment.IsDevelopment());
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = builder.Configuration["Authentication:ValidIssuer"] ?? authority,
            ValidAudiences = ["axiom-reference-data", "axiom-news"],
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks().AddCheck<UpstreamHealthCheck>("upstreams");

var app = builder.Build();

app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

app.MapReferenceDataGrpcEndpoints();
app.MapReverseProxy().RequireAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name == "upstreams" })
    .AllowAnonymous();

await app.RunAsync();

public partial class Program;
