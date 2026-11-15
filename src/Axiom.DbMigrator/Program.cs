// 独立数据库迁移工具（DbMigrator）。
// 由它负责在服务启动前执行 EF Core 迁移，服务进程本身不再自动迁移，
// 从而避免多副本同时启动时产生迁移竞争。
using Axiom.AuthHub.Data;
using Axiom.News.Infrastructure.EfCore;
using Axiom.ReferenceData.Infrastructure.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var requested = (args.FirstOrDefault() ?? "all").Trim().ToLowerInvariant();
var targets = requested switch
{
    "all" => new[] { "news", "reference-data", "auth" },
    "news" or "reference-data" or "auth" => new[] { requested },
    _ => throw new ArgumentException($"未知的迁移目标：{requested}。可用值为 all、news、reference-data、auth。")
};

foreach (var target in targets)
{
    Console.WriteLine($"开始迁移 {target} ...");
    switch (target)
    {
        case "news":
            await MigrateAsync<NewsDbContext>(configuration, "News");
            break;
        case "reference-data":
            await MigrateAsync<ReferenceDataDbContext>(configuration, "ReferenceData");
            break;
        case "auth":
            await MigrateAsync<AuthDbContext>(configuration, "Auth");
            break;
    }

    Console.WriteLine($"{target} 迁移完成。");
}

return 0;

static async Task MigrateAsync<TContext>(IConfiguration configuration, string connectionName)
    where TContext : DbContext
{
    var connectionString = configuration.GetConnectionString(connectionName)
        ?? throw new InvalidOperationException($"缺少连接字符串 ConnectionStrings:{connectionName}。");

    var options = new DbContextOptionsBuilder<TContext>()
        .UseNpgsql(connectionString)
        .Options;

    await using var context = (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    await context.Database.MigrateAsync();
}
