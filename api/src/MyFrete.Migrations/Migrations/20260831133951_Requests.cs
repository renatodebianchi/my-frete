using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace MyFrete.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class Requests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "requests");

            migrationBuilder.CreateTable(
                name: "transport_request",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    items = table.Column<string>(type: "jsonb", nullable: false),
                    estimated_weight_grams = table.Column<int>(type: "integer", nullable: false),
                    origin_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    origin_point = table.Column<Point>(type: "geography (point, 4326)", nullable: false),
                    destination_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    destination_point = table.Column<Point>(type: "geography (point, 4326)", nullable: false),
                    distance_meters = table.Column<double>(type: "double precision", nullable: false),
                    distance_source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    estimated_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    pricing_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    assigned_professional_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transport_request", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transport_request_client_id_created_at",
                schema: "requests",
                table: "transport_request",
                columns: new[] { "client_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transport_request_origin_point",
                schema: "requests",
                table: "transport_request",
                column: "origin_point")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_transport_request_status",
                schema: "requests",
                table: "transport_request",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transport_request",
                schema: "requests");
        }
    }
}
