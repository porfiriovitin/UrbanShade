using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanShade.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionalDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Organizations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "America/Sao_Paulo");

            migrationBuilder.CreateTable(
                name: "OrganizationRegions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeoRegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationRegions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationRegions_GeoRegions_GeoRegionId",
                        column: x => x.GeoRegionId,
                        principalTable: "GeoRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationRegions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRegions_GeoRegionId",
                table: "OrganizationRegions",
                column: "GeoRegionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRegions_OrganizationId_GeoRegionId",
                table: "OrganizationRegions",
                columns: new[] { "OrganizationId", "GeoRegionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationRegions");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Organizations");
        }
    }
}
