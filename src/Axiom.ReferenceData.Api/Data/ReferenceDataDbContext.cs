// 配置基础数据服务独立数据库中的地点档案模型。
using Axiom.ReferenceData.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Axiom.ReferenceData.Api.Data;

public sealed class ReferenceDataDbContext(DbContextOptions<ReferenceDataDbContext> options) : DbContext(options)
{
    public DbSet<ReferenceLocation> ReferenceLocations => Set<ReferenceLocation>();
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

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.EventType).HasMaxLength(150).IsRequired();
            entity.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(message => new { message.PublishedAtUtc, message.OccurredAtUtc });
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages");
            entity.HasKey(message => new { message.EventId, message.Consumer });
            entity.Property(message => message.Consumer).HasMaxLength(100).IsRequired();
        });
    }
}
