// Infrastructure 层 EF Core DbContext。
// 从 Domain 层引用 Entity，保持依赖方向。
using Axiom.ReferenceData.Infrastructure.Messaging; // OutboxMessage / InboxMessage（待迁移）
using Axiom.ReferenceData.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Axiom.ReferenceData.Infrastructure.EfCore;

public sealed class ReferenceDataDbContext(DbContextOptions<ReferenceDataDbContext> options) : DbContext(options)
{
    public DbSet<ReferenceLocation> ReferenceLocations => Set<ReferenceLocation>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<ReferenceLocationTranslation> ReferenceLocationTranslations => Set<ReferenceLocationTranslation>();
    public DbSet<RegionTranslation> RegionTranslations => Set<RegionTranslation>();

    // Messaging：Outbox 消息，由 Messaging 基础设施层定义，这里注册 DbSet。
    // OutboxMessage/InboxMessage 类型在 Messaging 基础设施层，但我们需要 Set<> 来访问。
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
            entity.HasIndex(region => region.GeoNameId).IsUnique();
            entity.HasIndex(region => new { region.CountryCode, region.Level, region.Code }).IsUnique();
            entity.HasIndex(region => new { region.CountryCode, region.Level });
        });

        modelBuilder.Entity<RegionTranslation>(entity =>
        {
            entity.ToTable("region_translations");
            entity.HasKey(translation => translation.Id);
            entity.Property(translation => translation.Culture).HasMaxLength(16).IsRequired();
            entity.Property(translation => translation.Field).HasMaxLength(32).IsRequired();
            entity.Property(translation => translation.Value).HasMaxLength(Region.MaxNameLength).IsRequired();
            entity.HasIndex(translation => new { translation.RegionId, translation.Culture, translation.Field })
                .IsUnique();
            entity.HasOne<Region>()
                .WithMany()
                .HasForeignKey(translation => translation.RegionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReferenceLocationTranslation>(entity =>
        {
            entity.ToTable("reference_location_translations");
            entity.HasKey(translation => translation.Id);
            entity.Property(translation => translation.Culture).HasMaxLength(16).IsRequired();
            entity.Property(translation => translation.Field).HasMaxLength(32).IsRequired();
            entity.Property(translation => translation.Value)
                .HasMaxLength(ReferenceLocation.MaxNameLength)
                .IsRequired();
            entity.HasIndex(translation => new { translation.ReferenceLocationId, translation.Culture, translation.Field })
                .IsUnique();
            entity.HasOne<ReferenceLocation>()
                .WithMany()
                .HasForeignKey(translation => translation.ReferenceLocationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.EventType).HasMaxLength(128).IsRequired();
            entity.Property(message => message.Payload).IsRequired();
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages");
            entity.HasKey(message => new { message.EventId, message.Consumer });
            entity.Property(message => message.Consumer).HasMaxLength(128).IsRequired();
        });
    }
}
