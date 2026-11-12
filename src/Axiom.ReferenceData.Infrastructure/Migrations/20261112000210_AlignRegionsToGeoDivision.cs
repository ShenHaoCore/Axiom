using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.ReferenceData.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignRegionsToGeoDivision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) 解除旧主键/索引/外键
            migrationBuilder.DropForeignKey(
                name: "FK_region_translations_regions_RegionId",
                table: "region_translations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_regions",
                table: "regions");

            migrationBuilder.DropIndex(
                name: "IX_regions_CountryCode_Level_Code",
                table: "regions");

            migrationBuilder.DropIndex(
                name: "IX_regions_CountryCode_Level",
                table: "regions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_region_translations",
                table: "region_translations");

            migrationBuilder.DropIndex(
                name: "IX_region_translations_RegionId_Culture_Field",
                table: "region_translations");

            // 2) Level → Depth 重命名（geo_division 口径：国家=0，省=1，地级=2，县级=3）
            migrationBuilder.RenameColumn(
                name: "Level",
                table: "regions",
                newName: "Depth");

            migrationBuilder.RenameIndex(
                name: "IX_regions_CountryCode_Level",
                table: "regions",
                newName: "IX_regions_CountryCode_Depth");

            // 3) 新列
            migrationBuilder.AddColumn<string>(
                name: "Abbrev",
                table: "regions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CodeIso",
                table: "regions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentCode",
                table: "regions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SortKey",
                table: "regions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "regions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "active");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "regions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RegionCountryCode",
                table: "region_translations",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RegionCode",
                table: "region_translations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // 4) 数据保留：在删除旧列之前回填
            //    4.1 深度平移：旧 Level（1 国家/2 省/3 市）→ Depth（0/1/2）
            migrationBuilder.Sql("UPDATE \"regions\" SET \"Depth\" = \"Depth\" - 1;");

            //    4.2 父级代码：由代理外键 ParentId 解析为父行业务代码
            migrationBuilder.Sql(
                "UPDATE \"regions\" SET \"ParentCode\" = p.\"Code\" " +
                "FROM \"regions\" p WHERE \"regions\".\"ParentId\" = p.\"Id\";");

            //    4.3 类型：按深度推导默认口径（国家=country，其余=province）
            migrationBuilder.Sql(
                "UPDATE \"regions\" SET \"Type\" = CASE WHEN \"Depth\" = 0 THEN 'country' ELSE 'province' END;");

            //    4.4 状态兜底
            migrationBuilder.Sql(
                "UPDATE \"regions\" SET \"Status\" = 'active' WHERE \"Status\" NOT IN ('active', 'deprecated');");

            //    4.5 翻译子表：由 RegionId 解析出复合键
            migrationBuilder.Sql(
                "UPDATE region_translations rt " +
                "SET \"RegionCountryCode\" = r.\"CountryCode\", \"RegionCode\" = r.\"Code\" " +
                "FROM \"regions\" r WHERE rt.\"RegionId\" = r.\"Id\";");

            // 5) 删除旧列
            migrationBuilder.DropColumn(
                name: "Id",
                table: "regions");

            migrationBuilder.DropColumn(
                name: "Admin1Code",
                table: "regions");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "regions");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "region_translations");

            migrationBuilder.DropColumn(
                name: "RegionId",
                table: "region_translations");

            // 6) 新主键
            migrationBuilder.AddPrimaryKey(
                name: "PK_regions",
                table: "regions",
                columns: new[] { "CountryCode", "Code" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_region_translations",
                table: "region_translations",
                columns: new[] { "RegionCountryCode", "RegionCode", "Culture", "Field" });

            // 7) 新索引
            migrationBuilder.CreateIndex(
                name: "IX_regions_CodeIso",
                table: "regions",
                column: "CodeIso",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regions_CountryCode_ParentCode",
                table: "regions",
                columns: new[] { "CountryCode", "ParentCode" });

            migrationBuilder.CreateIndex(
                name: "IX_regions_CountryCode_Depth",
                table: "regions",
                columns: new[] { "CountryCode", "Depth" });

            // 8) CHECK 约束（数据回填完成后生效）
            migrationBuilder.AddCheckConstraint(
                name: "CK_regions_depth",
                table: "regions",
                sql: "\"Depth\" >= 0 AND \"Depth\" <= 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_regions_parent_root",
                table: "regions",
                sql: "(\"ParentCode\" IS NULL) = (\"Depth\" = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_regions_status",
                table: "regions",
                sql: "\"Status\" IN ('active', 'deprecated')");

            // 9) 复合外键（自连 + 翻译子表；父码变更级联更新）
            migrationBuilder.AddForeignKey(
                name: "FK_region_translations_regions_RegionCountryCode_RegionCode",
                table: "region_translations",
                columns: new[] { "RegionCountryCode", "RegionCode" },
                principalTable: "regions",
                principalColumns: new[] { "CountryCode", "Code" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_regions_regions_CountryCode_ParentCode",
                table: "regions",
                columns: new[] { "CountryCode", "ParentCode" },
                principalTable: "regions",
                principalColumns: new[] { "CountryCode", "Code" },
                onDelete: ReferentialAction.Cascade,
                onUpdate: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 反向迁移仅恢复结构，不还原已删除的代理键数据（Down 场景为开发回滚）。
            migrationBuilder.DropForeignKey(
                name: "FK_region_translations_regions_RegionCountryCode_RegionCode",
                table: "region_translations");

            migrationBuilder.DropForeignKey(
                name: "FK_regions_regions_CountryCode_ParentCode",
                table: "regions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_regions",
                table: "regions");

            migrationBuilder.DropIndex(
                name: "IX_regions_CodeIso",
                table: "regions");

            migrationBuilder.DropIndex(
                name: "IX_regions_CountryCode_ParentCode",
                table: "regions");

            migrationBuilder.DropIndex(
                name: "IX_regions_CountryCode_Depth",
                table: "regions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_regions_depth",
                table: "regions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_regions_parent_root",
                table: "regions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_regions_status",
                table: "regions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_region_translations",
                table: "region_translations");

            migrationBuilder.DropColumn(
                name: "Abbrev",
                table: "regions");

            migrationBuilder.DropColumn(
                name: "CodeIso",
                table: "regions");

            migrationBuilder.DropColumn(
                name: "ParentCode",
                table: "regions");

            migrationBuilder.DropColumn(
                name: "SortKey",
                table: "regions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "regions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "regions");

            migrationBuilder.DropColumn(
                name: "RegionCountryCode",
                table: "region_translations");

            migrationBuilder.DropColumn(
                name: "RegionCode",
                table: "region_translations");

            migrationBuilder.RenameColumn(
                name: "Depth",
                table: "regions",
                newName: "Level");

            migrationBuilder.RenameIndex(
                name: "IX_regions_CountryCode_Depth",
                table: "regions",
                newName: "IX_regions_CountryCode_Level");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "regions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Admin1Code",
                table: "regions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "regions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "region_translations",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "RegionId",
                table: "region_translations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE \"regions\" SET \"Level\" = \"Level\" + 1;");

            migrationBuilder.AddPrimaryKey(
                name: "PK_regions",
                table: "regions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_region_translations",
                table: "region_translations",
                columns: new[] { "Id", "Culture", "Field" });

            migrationBuilder.CreateIndex(
                name: "IX_regions_CountryCode_Level_Code",
                table: "regions",
                columns: new[] { "CountryCode", "Level", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regions_CountryCode_Level",
                table: "regions",
                columns: new[] { "CountryCode", "Level" });

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
