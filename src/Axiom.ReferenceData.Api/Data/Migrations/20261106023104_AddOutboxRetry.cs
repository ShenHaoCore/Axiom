using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.ReferenceData.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_PublishedAtUtc_OccurredAtUtc",
                table: "outbox_messages");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "outbox_messages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAtUtc",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_PublishedAtUtc_NextAttemptAtUtc",
                table: "outbox_messages",
                columns: new[] { "PublishedAtUtc", "NextAttemptAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_PublishedAtUtc_NextAttemptAtUtc",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_PublishedAtUtc_OccurredAtUtc",
                table: "outbox_messages",
                columns: new[] { "PublishedAtUtc", "OccurredAtUtc" });
        }
    }
}
