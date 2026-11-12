// 启动基础数据 API，配置 ABP 风格分层依赖注入、JWT 鉴权、OpenAPI 和健康检查（HttpApi 层）。
using Axiom.Observability;
using Axiom.ReferenceData.Application.Services;
using Axiom.ReferenceData.Infrastructure;
using Axiom.ReferenceData.Infrastructure.EfCore;
using Axiom.ReferenceData.Infrastructure.Health;
using Axiom.ReferenceData.HttpApi;
using Axiom.ReferenceData.HttpApi.Endpoints;
using Axiom.SharedKernel.Localization;
using Axiom.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddAxiomObservability("Axiom.ReferenceData.Api");

// 国际化：默认语言与受支持语言来自 Localization 配置节，供语言协商与 AppService 回退链使用。
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

// Infrastructure：DbContext + 仓储 + 事务性消息 + 种子（由 Infrastructure 层自身注册）
builder.Services.AddReferenceDataInfrastructure(builder.Configuration, builder.Environment);

// Application：AppService
builder.Services.AddScoped<IGeoDivisionAppService, GeoDivisionAppService>();
builder.Services.AddScoped<IReferenceLocationAppService, ReferenceLocationAppService>();
builder.Services.AddScoped<IReferenceEnumAppService, ReferenceEnumAppService>();

builder.Services.AddGrpc();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Axiom ReferenceData API",
            Version = "v1",
            Description = "机场、港口和火车站基础档案 + 全球行政区 API。"
        };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "使用 AuthHub 签发的 JWT Bearer 令牌。"
            }
        };
        document.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            }
        ];
        return Task.CompletedTask;
    });
});

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
            ValidAudience = "axiom-reference-data",
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(ReferenceLocationEndpoints.READ_POLICY, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(AxiomClaimTypes.PERMISSION, ReferenceDataPermissions.READ))
    .AddPolicy(ReferenceLocationEndpoints.MANAGE_POLICY, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(AxiomClaimTypes.PERMISSION, ReferenceDataPermissions.MANAGE));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ReferenceDataDbContext>("database")
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["broker"]);

var app = builder.Build();

app.UseRequestLocalization();
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var problemDetailsService = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
    await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
    {
        HttpContext = context.HttpContext,
        ProblemDetails = new ProblemDetails { Status = context.HttpContext.Response.StatusCode }
    });
});
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi("/openapi/v1.json").AllowAnonymous();
app.MapReferenceLocationEndpoints();
app.MapReferenceEnumEndpoints();
app.MapGeoDivisionEndpoints();
app.MapGrpcService<ReferenceDataQueryGrpcService>().RequireAuthorization(ReferenceLocationEndpoints.READ_POLICY);
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Name is "database" or "rabbitmq"
})
    .AllowAnonymous();

await app.RunAsync();

public partial class Program;
