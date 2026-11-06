// 启动基础数据 API，并配置独立数据库、JWT 鉴权与健康检查。
using Axiom.Observability;
using Axiom.ReferenceData.Api;
using Axiom.ReferenceData.Api.Data;
using Axiom.ReferenceData.Api.Endpoints;
using Axiom.ReferenceData.Api.Health;
using Axiom.ReferenceData.Api.Messaging;
using Axiom.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddAxiomObservability("Axiom.ReferenceData.Api");
builder.Services.AddDbContext<ReferenceDataDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ReferenceData")));
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
            Description = "机场、港口和火车站基础档案 API。"
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
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
builder.Services.AddScoped<OutboxPublishProcessor>();
builder.Services.AddScoped<InboxProcessor>();
builder.Services.AddHttpClient<GeoNamesDownloader>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<OutboxPublisherWorker>();
    builder.Services.AddHostedService<RabbitMqConsumerWorker>();
    builder.Services.AddHostedService<RegionSeedService>();
}

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

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>().Database.MigrateAsync();
}

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
app.MapRegionEndpoints();
app.MapGrpcService<ReferenceDataQueryGrpcService>().RequireAuthorization(ReferenceLocationEndpoints.READ_POLICY);
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Name is "database" or "rabbitmq"
})
    .AllowAnonymous();

await app.RunAsync();

public partial class Program;
