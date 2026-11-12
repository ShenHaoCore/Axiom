using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.ReferenceData.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "regions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "regions");
        }
    }
}
