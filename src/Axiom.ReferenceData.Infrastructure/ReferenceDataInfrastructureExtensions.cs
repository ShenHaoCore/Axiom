// Infrastructure 层依赖注入注册（ABP 约定：基础设施自行描述其服务注册）。
// HttpApi 层只调用 AddReferenceDataInfrastructure，不直接感知 DbContext、仓储与消息基础设施实现。
using Axiom.ReferenceData.Domain.Repositories;
using Axiom.ReferenceData.Infrastructure.EfCore;
using Axiom.ReferenceData.Infrastructure.Messaging;
using Axiom.ReferenceData.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Axiom.ReferenceData.Infrastructure;

public static class ReferenceDataInfrastructureExtensions
{
    /// <summary>注册基础数据模块的 EF Core、仓储、事务性消息与种子服务。</summary>
    public static IServiceCollection AddReferenceDataInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDbContext<ReferenceDataDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("ReferenceData")));

        services.AddScoped<IRegionRepository, RegionRepository>();
        services.AddScoped<IReferenceLocationRepository, ReferenceLocationRepository>();

        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
        services.AddScoped<OutboxPublishProcessor>();
        services.AddScoped<InboxProcessor>();
        services.AddHttpClient<GeoNamesDownloader>(client =>
            client.Timeout = TimeSpan.FromSeconds(15));

        if (!environment.IsEnvironment("Testing"))
        {
            services.AddHostedService<OutboxPublisherWorker>();
            services.AddHostedService<RabbitMqConsumerWorker>();
            services.AddHostedService<RegionSeedService>();
        }

        return services;
    }
}
