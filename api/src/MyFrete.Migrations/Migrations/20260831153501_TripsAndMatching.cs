using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFrete.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class TripsAndMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "matching");

            migrationBuilder.EnsureSchema(
                name: "trips");

            migrationBuilder.CreateTable(
                name: "matching_session",
                schema: "matching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_lat = table.Column<double>(type: "double precision", nullable: false),
                    origin_lng = table.Column<double>(type: "double precision", nullable: false),
                    weight_grams = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deadline_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    contacted_count = table.Column<int>(type: "integer", nullable: false),
                    contacted_ids = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    current_offer_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matching_session", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "offer",
                schema: "matching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    respond_by = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offer", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trip",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    agreed_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    client_response = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    client_responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_notified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    payment_settled_outside_app = table.Column<bool>(type: "boolean", nullable: false),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trip", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_matching_session_request_id",
                schema: "matching",
                table: "matching_session",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_matching_session_state",
                schema: "matching",
                table: "matching_session",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "ix_offer_professional_id_outcome",
                schema: "matching",
                table: "offer",
                columns: new[] { "professional_id", "outcome" });

            migrationBuilder.CreateIndex(
                name: "ix_offer_session_id",
                schema: "matching",
                table: "offer",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_trip_client_id_created_at",
                schema: "trips",
                table: "trip",
                columns: new[] { "client_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_trip_professional_id_status",
                schema: "trips",
                table: "trip",
                columns: new[] { "professional_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_trip_request_id",
                schema: "trips",
                table: "trip",
                column: "request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "matching_session",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "offer",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "trip",
                schema: "trips");
        }
    }
}
