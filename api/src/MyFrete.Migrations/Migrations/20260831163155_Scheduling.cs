using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFrete.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class Scheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "scheduling");

            migrationBuilder.CreateTable(
                name: "professional_daily_load",
                schema: "scheduling",
                columns: table => new
                {
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    load_date = table.Column<DateOnly>(type: "date", nullable: false),
                    accepted_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professional_daily_load", x => new { x.professional_id, x.load_date });
                });

            migrationBuilder.CreateTable(
                name: "professional_schedule_availability",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    available_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professional_schedule_availability", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_offer",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    weight_grams = table.Column<int>(type: "integer", nullable: false),
                    estimated_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scheduled_offer", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_professional_schedule_availability_available_date",
                schema: "scheduling",
                table: "professional_schedule_availability",
                column: "available_date");

            migrationBuilder.CreateIndex(
                name: "ix_professional_schedule_availability_professional_id_availabl",
                schema: "scheduling",
                table: "professional_schedule_availability",
                columns: new[] { "professional_id", "available_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_offer_professional_id_outcome",
                schema: "scheduling",
                table: "scheduled_offer",
                columns: new[] { "professional_id", "outcome" });

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_offer_request_id_outcome",
                schema: "scheduling",
                table: "scheduled_offer",
                columns: new[] { "request_id", "outcome" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "professional_daily_load",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "professional_schedule_availability",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "scheduled_offer",
                schema: "scheduling");
        }
    }
}
