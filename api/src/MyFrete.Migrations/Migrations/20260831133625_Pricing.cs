using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFrete.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class Pricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pricing");

            migrationBuilder.CreateTable(
                name: "pricing_rule",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_fare = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    per_km = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    per_kg = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    min_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pricing_rule", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pricing_rule_effective_from",
                schema: "pricing",
                table: "pricing_rule",
                column: "effective_from");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pricing_rule",
                schema: "pricing");
        }
    }
}
