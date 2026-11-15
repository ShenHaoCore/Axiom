// Infrastructure 层依赖注入注册（ABP 约定：基础设施自行描述其服务注册）。
// HttpApi 层只调用 AddNewsInfrastructure，不直接感知 DbContext 与 Repository 实现。
using Axiom.News.Domain.Repositories;
using Axiom.News.Infrastructure.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Axiom.News.Infrastructure;

public static class NewsInfrastructureExtensions
{
    /// <summary>注册新闻模块的 EF Core DbContext 与仓储实现。</summary>
    public static IServiceCollection AddNewsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<NewsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("News")));
        services.AddScoped<INewsArticleRepository, NewsArticleRepository>();
        return services;
    }
}
