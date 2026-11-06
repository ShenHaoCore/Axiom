using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.News.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "news_article_translations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NewsArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Culture = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Field = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_article_translations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_news_article_translations_news_articles_NewsArticleId",
                        column: x => x.NewsArticleId,
                        principalTable: "news_articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_news_article_translations_NewsArticleId_Culture_Field",
                table: "news_article_translations",
                columns: new[] { "NewsArticleId", "Culture", "Field" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "news_article_translations");
        }
    }
}
