// 启动新闻 API，配置分层依赖注入、JWT 鉴权、OpenAPI 和健康检查（HttpApi 层）。
using Axiom.News.Application.Services;
using Axiom.News.HttpApi;
using Axiom.News.HttpApi.Endpoints;
using Axiom.News.Infrastructure;
using Axiom.News.Infrastructure.EfCore;
using Axiom.Observability;
using Axiom.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddAxiomObservability("Axiom.News.Api");

// Infrastructure：DbContext + Repository（由 Infrastructure 层自身注册）
builder.Services.AddNewsInfrastructure(builder.Configuration);

// Application：AppService
builder.Services.AddScoped<INewsArticleAppService, NewsArticleAppService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Axiom News API",
            Version = "v1",
            Description = "新闻文章发布和管理 API。"
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
            ValidAudience = "axiom-news",
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(NewsArticleEndpoints.READ_POLICY, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(AxiomClaimTypes.PERMISSION, NewsPermissions.READ))
    .AddPolicy(NewsArticleEndpoints.MANAGE_POLICY, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(AxiomClaimTypes.PERMISSION, NewsPermissions.MANAGE));

builder.Services.AddHealthChecks().AddDbContextCheck<NewsDbContext>("database");

var app = builder.Build();

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
app.MapNewsArticleEndpoints();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name == "database" })
    .AllowAnonymous();

await app.RunAsync();

public partial class Program;
