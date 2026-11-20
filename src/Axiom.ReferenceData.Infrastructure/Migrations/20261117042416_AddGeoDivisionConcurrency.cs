using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.ReferenceData.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeoDivisionConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "geo_divisions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "geo_divisions");
        }
    }
}
