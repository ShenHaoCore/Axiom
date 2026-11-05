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
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(ReferenceDataDbContext));
            services.RemoveAll(typeof(DbContextOptions<ReferenceDataDbContext>));
            services.RemoveAll(typeof(DbContextOptions));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<ReferenceDataDbContext>));
            services.AddDbContext<ReferenceDataDbContext>(options =>
                options.UseInMemoryDatabase("AxiomReferenceDataTests"));

            services.AddAuthentication(TestAuthHandler.SCHEME)
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SCHEME,
                    _ => { });
        });
    }
}
