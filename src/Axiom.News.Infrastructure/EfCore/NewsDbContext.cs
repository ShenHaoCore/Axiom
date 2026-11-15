// Infrastructure 层 EF Core DbContext。
// 从 Axiom.News.Domain.Entities 引用 Entity，从 Axiom.News.Domain.Constants 引用字段长度。
using Axiom.News.Domain.Constants;
using Axiom.News.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Axiom.News.Infrastructure.EfCore;

public sealed class NewsDbContext(DbContextOptions<NewsDbContext> options) : DbContext(options)
{
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();

    public DbSet<NewsArticleTranslation> NewsArticleTranslations => Set<NewsArticleTranslation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NewsArticle>(entity =>
        {
            entity.ToTable("news_articles");
            entity.HasKey(article => article.Id);
            entity.Property(article => article.ConcurrencyToken).IsConcurrencyToken().IsRequired();
            entity.Property(article => article.Title).HasMaxLength(NewsArticleLengths.MaxTitleLength).IsRequired();
            entity.Property(article => article.Summary).HasMaxLength(NewsArticleLengths.MaxSummaryLength);
            entity.Property(article => article.Content).HasMaxLength(NewsArticleLengths.MaxContentLength).IsRequired();
            entity.Property(article => article.Category).HasMaxLength(NewsArticleLengths.MaxCategoryLength).IsRequired();
            entity.Property(article => article.Author).HasMaxLength(NewsArticleLengths.MaxAuthorLength).IsRequired();
            entity.Property(article => article.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasIndex(article => article.Status);
            entity.HasIndex(article => article.Category);
            entity.HasIndex(article => new { article.Status, article.PublishedAt });
        });

        modelBuilder.Entity<NewsArticleTranslation>(entity =>
        {
            entity.ToTable("news_article_translations");
            entity.HasKey(translation => translation.Id);
            entity.Property(translation => translation.Culture).HasMaxLength(16).IsRequired();
            entity.Property(translation => translation.Field).HasMaxLength(32).IsRequired();
            // 值列按最长字段（正文）建列，具体字段长度由应用层校验。
            entity.Property(translation => translation.Value)
                .HasMaxLength(NewsArticleLengths.MaxContentLength)
                .IsRequired();
            entity.HasIndex(translation => new { translation.NewsArticleId, translation.Culture, translation.Field })
                .IsUnique();
            entity.HasOne<NewsArticle>()
                .WithMany()
                .HasForeignKey(translation => translation.NewsArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
