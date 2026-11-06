using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.ReferenceData.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Admin1Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AsciiName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LocalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Population = table.Column<long>(type: "bigint", nullable: true),
                    GeoNameId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_regions_CountryCode_Level",
                table: "regions",
                columns: new[] { "CountryCode", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_regions_CountryCode_Level_Code",
                table: "regions",
                columns: new[] { "CountryCode", "Level", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regions_GeoNameId",
                table: "regions",
                column: "GeoNameId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regions");
        }
    }
}
