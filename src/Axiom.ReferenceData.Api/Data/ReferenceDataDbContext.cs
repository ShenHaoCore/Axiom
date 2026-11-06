// 配置基础数据服务独立数据库中的地点档案与行政区层级模型。
using Axiom.ReferenceData.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Axiom.ReferenceData.Api.Data;

public sealed class ReferenceDataDbContext(DbContextOptions<ReferenceDataDbContext> options) : DbContext(options)
{
    public DbSet<ReferenceLocation> ReferenceLocations => Set<ReferenceLocation>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReferenceLocation>(entity =>
        {
            entity.ToTable("reference_locations");
            entity.HasKey(location => location.Id);
            entity.Property(location => location.ConcurrencyToken).IsConcurrencyToken().IsRequired();
            entity.Property(location => location.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(location => location.Code).HasMaxLength(20).IsRequired();
            entity.Property(location => location.Name).HasMaxLength(200).IsRequired();
            entity.Property(location => location.CountryCode).HasMaxLength(2).IsRequired();
            entity.Property(location => location.City).HasMaxLength(100);
            entity.HasIndex(location => new { location.Type, location.Code }).IsUnique();
        });

        modelBuilder.Entity<Region>(entity =>
        {
            entity.ToTable("regions");
            entity.HasKey(region => region.Id);
            entity.Property(region => region.Level).HasConversion<int>();
            entity.Property(region => region.CountryCode).HasMaxLength(2).IsRequired();
            entity.Property(region => region.Admin1Code).HasMaxLength(10);
            entity.Property(region => region.Code).HasMaxLength(50).IsRequired();
            entity.Property(region => region.Name).HasMaxLength(200).IsRequired();
            entity.Property(region => region.AsciiName).HasMaxLength(200);
            entity.Property(region => region.LocalName).HasMaxLength(200);
            entity.Property(region => region.Population);
            entity.Property(region => region.GeoNameId);

            // GeoNames 的 geonameid 保证幂等 seed；内置国家无 geonameid 用 countryCode+level 唯一。
            entity.HasIndex(region => region.GeoNameId).IsUnique();
            entity.HasIndex(region => new { region.CountryCode, region.Level, region.Code }).IsUnique();
            entity.HasIndex(region => new { region.CountryCode, region.Level });
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.EventType).HasMaxLength(150).IsRequired();
            entity.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
            entity.Property(message => message.LastError).HasMaxLength(500);
            entity.HasIndex(message => new { message.PublishedAtUtc, message.NextAttemptAtUtc });
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages");
            entity.HasKey(message => new { message.EventId, message.Consumer });
            entity.Property(message => message.Consumer).HasMaxLength(100).IsRequired();
        });
    }
}
