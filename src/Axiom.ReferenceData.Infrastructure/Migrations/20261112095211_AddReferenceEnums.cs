using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.ReferenceData.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceEnums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reference_enums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reference_enums", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reference_enums_Category_Code",
                table: "reference_enums",
                columns: new[] { "Category", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reference_enums_Category_SortOrder",
                table: "reference_enums",
                columns: new[] { "Category", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reference_enums");
        }
    }
}
