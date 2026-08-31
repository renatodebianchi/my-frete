using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFrete.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class Notifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "device_token",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    token = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_token", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_dispatch",
                schema: "notifications",
                columns: table => new
                {
                    dedupe_key = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_dispatch", x => x.dedupe_key);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_token_token",
                schema: "notifications",
                table: "device_token",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_token_user_id",
                schema: "notifications",
                table: "device_token",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_token",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_dispatch",
                schema: "notifications");
        }
    }
}
