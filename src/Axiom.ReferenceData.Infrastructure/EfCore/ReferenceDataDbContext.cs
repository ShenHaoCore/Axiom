// Infrastructure 层 EF Core DbContext。
// 从 Domain 层引用 Entity，保持依赖方向。
using Axiom.ReferenceData.Infrastructure.Messaging; // OutboxMessage / InboxMessage（待迁移）
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Axiom.ReferenceData.Infrastructure.EfCore;

public sealed class ReferenceDataDbContext(DbContextOptions<ReferenceDataDbContext> options) : DbContext(options)
{
    public DbSet<ReferenceLocation> ReferenceLocations => Set<ReferenceLocation>();
    public DbSet<GeoDivision> GeoDivisions => Set<GeoDivision>();
    public DbSet<ReferenceEnum> ReferenceEnums => Set<ReferenceEnum>();
    public DbSet<ReferenceLocationTranslation> ReferenceLocationTranslations => Set<ReferenceLocationTranslation>();
    public DbSet<GeoDivisionTranslation> GeoDivisionTranslations => Set<GeoDivisionTranslation>();

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

        modelBuilder.Entity<GeoDivision>(entity =>
        {
            entity.ToTable("geo_divisions");
            entity.HasKey(region => region.Id);
            entity.Property(region => region.Kind).HasMaxLength(GeoDivisionKinds.MaxLength).IsRequired();
            entity.Property(region => region.CountryCode).HasMaxLength(2).IsRequired();
            entity.Property(region => region.SubdivisionCode).HasMaxLength(GeoDivision.MaxSubdivisionCodeLength);
            entity.Property(region => region.Code).HasMaxLength(50).IsRequired();
            entity.Property(region => region.Name).HasMaxLength(200).IsRequired();
            entity.Property(region => region.AsciiName).HasMaxLength(200);
            entity.Property(region => region.LocalName).HasMaxLength(200);
            entity.Property(region => region.Population);
            entity.Property(region => region.GeoNameId);
            entity.HasIndex(region => region.GeoNameId).IsUnique();
            entity.HasIndex(region => new { region.CountryCode, region.Level, region.Code }).IsUnique();
            entity.HasIndex(region => new { region.CountryCode, region.Level });
            entity.HasIndex(region => region.ParentId);
            entity.HasOne<GeoDivision>()
                .WithMany()
                .HasForeignKey(region => region.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReferenceEnum>(entity =>
        {
            entity.ToTable("reference_enums");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Category).HasMaxLength(ReferenceEnumCategories.MaxLength).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(ReferenceEnum.MaxCodeLength).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(ReferenceEnum.MaxNameLength).IsRequired();
            entity.HasIndex(item => new { item.Category, item.Code }).IsUnique();
            entity.HasIndex(item => new { item.Category, item.SortOrder });
        });

        modelBuilder.Entity<GeoDivisionTranslation>(entity =>
        {
            entity.ToTable("geo_division_translations");
            entity.HasKey(translation => translation.Id);
            entity.Property(translation => translation.Culture).HasMaxLength(16).IsRequired();
            entity.Property(translation => translation.Name).HasMaxLength(GeoDivision.MaxNameLength).IsRequired();
            // 一行一语言：同一行政区同一 Culture 仅一条。
            entity.HasIndex(translation => new { translation.GeoDivisionId, translation.Culture }).IsUnique();
            entity.HasOne<GeoDivision>()
                .WithMany()
                .HasForeignKey(translation => translation.GeoDivisionId)
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
