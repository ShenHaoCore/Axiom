// 为 API 集成测试替换数据库和外部 JWT 认证边界。
using Axiom.ReferenceData.Api;
using Axiom.ReferenceData.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Axiom.IntegrationTests;

public sealed class ReferenceDataApiFactory : WebApplicationFactory<ReferenceDataApplicationMarker>
{
    private readonly PostgresTestDatabase? database;

    public ReferenceDataApiFactory()
    {
        database = PostgresTestDatabase.CreateAsync().GetAwaiter().GetResult();
        if (database is not null)
        {
            using var context = new ReferenceDataDbContext(
                new DbContextOptionsBuilder<ReferenceDataDbContext>()
                    .UseNpgsql(database.ConnectionString)
                    .Options);
            context.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(ReferenceDataDbContext));
            services.RemoveAll(typeof(DbContextOptions<ReferenceDataDbContext>));
            services.RemoveAll(typeof(DbContextOptions));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<ReferenceDataDbContext>));

            if (database is null)
            {
                services.AddDbContext<ReferenceDataDbContext>(options =>
                    options.UseInMemoryDatabase("AxiomReferenceDataTests"));
            }
            else
            {
                services.AddDbContext<ReferenceDataDbContext>(options =>
                    options.UseNpgsql(database.ConnectionString));
            }

            services.AddAuthentication(TestAuthHandler.SCHEME)
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SCHEME,
                    _ => { });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            database?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
