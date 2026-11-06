// 集成测试数据库：配置 AXIOM_TEST_POSTGRES 时创建独立 PostgreSQL 库，否则回退内存库。
using Axiom.ReferenceData.Infrastructure.EfCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Axiom.IntegrationTests;

internal sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly string adminConnectionString;
    private readonly string databaseName;

    private PostgresTestDatabase(string adminConnectionString, string databaseName)
    {
        this.adminConnectionString = adminConnectionString;
        this.databaseName = databaseName;
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName };
        ConnectionString = builder.ConnectionString;
    }

    public string ConnectionString { get; }

    public static async Task<PostgresTestDatabase?> CreateAsync(CancellationToken cancellationToken = default)
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("AXIOM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            return null;
        }

        var databaseName = $"axiom_test_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new PostgresTestDatabase(adminConnectionString, databaseName);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
            await command.ExecuteNonQueryAsync();
        }
        catch (NpgsqlException)
        {
            // 清理失败不影响测试结论。
        }
    }
}

internal sealed class ReferenceDataTestContext : IAsyncDisposable
{
    private readonly PostgresTestDatabase? database;

    private ReferenceDataTestContext(
        DbContextOptions<ReferenceDataDbContext> options,
        PostgresTestDatabase? database)
    {
        Options = options;
        this.database = database;
    }

    public DbContextOptions<ReferenceDataDbContext> Options { get; }

    public static async Task<ReferenceDataTestContext> CreateAsync(CancellationToken cancellationToken = default)
    {
        var database = await PostgresTestDatabase.CreateAsync(cancellationToken);
        var builder = new DbContextOptionsBuilder<ReferenceDataDbContext>();
        if (database is null)
        {
            builder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }
        else
        {
            builder.UseNpgsql(database.ConnectionString);
            await using var context = new ReferenceDataDbContext(builder.Options);
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        return new ReferenceDataTestContext(builder.Options, database);
    }

    public async ValueTask DisposeAsync()
    {
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }
}
