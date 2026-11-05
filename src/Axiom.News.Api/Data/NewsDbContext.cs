// 配置新闻服务独立数据库中的文章模型。
using Axiom.News.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Axiom.News.Api.Data;

public sealed class NewsDbContext(DbContextOptions<NewsDbContext> options) : DbContext(options)
{
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NewsArticle>(entity =>
        {
            entity.ToTable("news_articles");
            entity.HasKey(article => article.Id);
            entity.Property(article => article.ConcurrencyToken).IsConcurrencyToken().IsRequired();
            entity.Property(article => article.Title).HasMaxLength(NewsArticleInputValidator.MAX_TITLE_LENGTH).IsRequired();
            entity.Property(article => article.Summary).HasMaxLength(NewsArticleInputValidator.MAX_SUMMARY_LENGTH);
            entity.Property(article => article.Content).HasMaxLength(NewsArticleInputValidator.MAX_CONTENT_LENGTH).IsRequired();
            entity.Property(article => article.Category).HasMaxLength(NewsArticleInputValidator.MAX_CATEGORY_LENGTH).IsRequired();
            entity.Property(article => article.Author).HasMaxLength(NewsArticleInputValidator.MAX_AUTHOR_LENGTH).IsRequired();
            entity.Property(article => article.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasIndex(article => article.Status);
            entity.HasIndex(article => article.Category);
            entity.HasIndex(article => new { article.Status, article.PublishedAt });
        });
    }
}
