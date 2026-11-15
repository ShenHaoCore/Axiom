using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.ReferenceData.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RedesignGeoDivisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- region_translations：EAV → 宽表，再改名 ---
            migrationBuilder.Sql("""
                DELETE FROM region_translations
                WHERE "Field" <> 'Name';

                DELETE FROM region_translations AS older
                USING region_translations AS newer
                WHERE older."Field" = 'Name'
                  AND newer."Field" = 'Name'
                  AND older."RegionId" = newer."RegionId"
                  AND older."Culture" = newer."Culture"
                  AND older."Id" < newer."Id";
                """);

            migrationBuilder.DropIndex(
                name: "IX_region_translations_RegionId_Culture_Field",
                table: "region_translations");

            migrationBuilder.DropColumn(
                name: "Field",
                table: "region_translations");

            migrationBuilder.RenameColumn(
                name: "Value",
                table: "region_translations",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "RegionId",
                table: "region_translations",
                newName: "GeoDivisionId");

            migrationBuilder.RenameTable(
                name: "region_translations",
                newName: "geo_division_translations");

            migrationBuilder.CreateIndex(
                name: "IX_geo_division_translations_GeoDivisionId_Culture",
                table: "geo_division_translations",
                columns: new[] { "GeoDivisionId", "Culture" },
                unique: true);

            // --- regions → geo_divisions ---
            migrationBuilder.RenameTable(
                name: "regions",
                newName: "geo_divisions");

            migrationBuilder.RenameColumn(
                name: "Admin1Code",
                table: "geo_divisions",
                newName: "SubdivisionCode");

            migrationBuilder.AlterColumn<string>(
                name: "SubdivisionCode",
                table: "geo_divisions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "geo_divisions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "standard");

            migrationBuilder.RenameIndex(
                name: "IX_regions_CountryCode_Level",
                table: "geo_divisions",
                newName: "IX_geo_divisions_CountryCode_Level");

            migrationBuilder.RenameIndex(
                name: "IX_regions_CountryCode_Level_Code",
                table: "geo_divisions",
                newName: "IX_geo_divisions_CountryCode_Level_Code");

            migrationBuilder.RenameIndex(
                name: "IX_regions_GeoNameId",
                table: "geo_divisions",
                newName: "IX_geo_divisions_GeoNameId");

            migrationBuilder.CreateIndex(
                name: "IX_geo_divisions_ParentId",
                table: "geo_divisions",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_geo_divisions_geo_divisions_ParentId",
                table: "geo_divisions",
                column: "ParentId",
                principalTable: "geo_divisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 翻译表 FK 名随表重命名后可能仍挂旧名；重建以对齐快照。
            migrationBuilder.DropForeignKey(
                name: "FK_region_translations_regions_RegionId",
                table: "geo_division_translations");

            migrationBuilder.AddForeignKey(
                name: "FK_geo_division_translations_geo_divisions_GeoDivisionId",
                table: "geo_division_translations",
                column: "GeoDivisionId",
                principalTable: "geo_divisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_geo_division_translations_geo_divisions_GeoDivisionId",
                table: "geo_division_translations");

            migrationBuilder.DropForeignKey(
                name: "FK_geo_divisions_geo_divisions_ParentId",
                table: "geo_divisions");

            migrationBuilder.DropIndex(
                name: "IX_geo_divisions_ParentId",
                table: "geo_divisions");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "geo_divisions");

            migrationBuilder.AlterColumn<string>(
                name: "SubdivisionCode",
                table: "geo_divisions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "SubdivisionCode",
                table: "geo_divisions",
                newName: "Admin1Code");

            migrationBuilder.RenameIndex(
                name: "IX_geo_divisions_CountryCode_Level",
                table: "geo_divisions",
                newName: "IX_regions_CountryCode_Level");

            migrationBuilder.RenameIndex(
                name: "IX_geo_divisions_CountryCode_Level_Code",
                table: "geo_divisions",
                newName: "IX_regions_CountryCode_Level_Code");

            migrationBuilder.RenameIndex(
                name: "IX_geo_divisions_GeoNameId",
                table: "geo_divisions",
                newName: "IX_regions_GeoNameId");

            migrationBuilder.RenameTable(
                name: "geo_divisions",
                newName: "regions");

            migrationBuilder.DropIndex(
                name: "IX_geo_division_translations_GeoDivisionId_Culture",
                table: "geo_division_translations");

            migrationBuilder.RenameColumn(
                name: "GeoDivisionId",
                table: "geo_division_translations",
                newName: "RegionId");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "geo_division_translations",
                newName: "Value");

            migrationBuilder.AddColumn<string>(
                name: "Field",
                table: "geo_division_translations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Name");

            migrationBuilder.RenameTable(
                name: "geo_division_translations",
                newName: "region_translations");

            migrationBuilder.CreateIndex(
                name: "IX_region_translations_RegionId_Culture_Field",
                table: "region_translations",
                columns: new[] { "RegionId", "Culture", "Field" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_region_translations_regions_RegionId",
                table: "region_translations",
                column: "RegionId",
                principalTable: "regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
