using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace MyFrete.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class ProfessionalLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_professional_profile_immediate_availability",
                schema: "accounts",
                table: "professional_profile");

            migrationBuilder.AddColumn<Point>(
                name: "last_location",
                schema: "accounts",
                table: "professional_profile",
                type: "geography (point, 4326)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_location_at",
                schema: "accounts",
                table: "professional_profile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_professional_profile_immediate_availability",
                schema: "accounts",
                table: "professional_profile",
                column: "immediate_availability",
                filter: "immediate_availability");

            migrationBuilder.CreateIndex(
                name: "ix_professional_profile_last_location",
                schema: "accounts",
                table: "professional_profile",
                column: "last_location")
                .Annotation("Npgsql:IndexMethod", "gist");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_professional_profile_immediate_availability",
                schema: "accounts",
                table: "professional_profile");

            migrationBuilder.DropIndex(
                name: "ix_professional_profile_last_location",
                schema: "accounts",
                table: "professional_profile");

            migrationBuilder.DropColumn(
                name: "last_location",
                schema: "accounts",
                table: "professional_profile");

            migrationBuilder.DropColumn(
                name: "last_location_at",
                schema: "accounts",
                table: "professional_profile");

            migrationBuilder.CreateIndex(
                name: "ix_professional_profile_immediate_availability",
                schema: "accounts",
                table: "professional_profile",
                column: "immediate_availability");
        }
    }
}
