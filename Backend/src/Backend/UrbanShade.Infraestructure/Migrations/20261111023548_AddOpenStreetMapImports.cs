using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanShade.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenStreetMapImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GeoAnalysisJobs_GeoRegionId_BoundaryVersion_ObservationStar~",
                table: "GeoAnalysisJobs");

            migrationBuilder.DropIndex(
                name: "IX_GeoAnalysisJobs_Status_LeaseExpiresAt_CreatedAt",
                table: "GeoAnalysisJobs");

            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedAt",
                table: "GreenAreas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OsmElementId",
                table: "GreenAreas",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OsmElementType",
                table: "GreenAreas",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceMetadata",
                table: "GreenAreas",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenStreetMapReferenceDate",
                table: "GeoRegions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenStreetMapRequestedAt",
                table: "GeoRegions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobType",
                table: "GeoAnalysisJobs",
                type: "text",
                nullable: false,
                defaultValue: "Satellite");

            migrationBuilder.CreateTable(
                name: "GreenAreaRegions",
                columns: table => new
                {
                    GreenAreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeoRegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoundaryVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GreenAreaRegions", x => new { x.GreenAreaId, x.GeoRegionId });
                    table.ForeignKey(
                        name: "FK_GreenAreaRegions_GeoRegions_GeoRegionId",
                        column: x => x.GeoRegionId,
                        principalTable: "GeoRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GreenAreaRegions_GreenAreas_GreenAreaId",
                        column: x => x.GreenAreaId,
                        principalTable: "GreenAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GreenAreas_OsmElementType_OsmElementId",
                table: "GreenAreas",
                columns: new[] { "OsmElementType", "OsmElementId" },
                unique: true,
                filter: "\"OsmElementType\" IS NOT NULL AND \"OsmElementId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GeoAnalysisJobs_GeoRegionId_BoundaryVersion_ObservationStar~",
                table: "GeoAnalysisJobs",
                columns: new[] { "GeoRegionId", "BoundaryVersion", "ObservationStartAt", "ObservationEndAt", "AlgorithmVersion", "Parameters", "JobType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeoAnalysisJobs_JobType_Status_LeaseExpiresAt_CreatedAt",
                table: "GeoAnalysisJobs",
                columns: new[] { "JobType", "Status", "LeaseExpiresAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GreenAreaRegions_GeoRegionId",
                table: "GreenAreaRegions",
                column: "GeoRegionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GreenAreaRegions");

            migrationBuilder.DropIndex(
                name: "IX_GreenAreas_OsmElementType_OsmElementId",
                table: "GreenAreas");

            migrationBuilder.DropIndex(
                name: "IX_GeoAnalysisJobs_GeoRegionId_BoundaryVersion_ObservationStar~",
                table: "GeoAnalysisJobs");

            migrationBuilder.DropIndex(
                name: "IX_GeoAnalysisJobs_JobType_Status_LeaseExpiresAt_CreatedAt",
                table: "GeoAnalysisJobs");

            migrationBuilder.DropColumn(
                name: "ImportedAt",
                table: "GreenAreas");

            migrationBuilder.DropColumn(
                name: "OsmElementId",
                table: "GreenAreas");

            migrationBuilder.DropColumn(
                name: "OsmElementType",
                table: "GreenAreas");

            migrationBuilder.DropColumn(
                name: "SourceMetadata",
                table: "GreenAreas");

            migrationBuilder.DropColumn(
                name: "OpenStreetMapReferenceDate",
                table: "GeoRegions");

            migrationBuilder.DropColumn(
                name: "OpenStreetMapRequestedAt",
                table: "GeoRegions");

            migrationBuilder.DropColumn(
                name: "JobType",
                table: "GeoAnalysisJobs");

            migrationBuilder.CreateIndex(
                name: "IX_GeoAnalysisJobs_GeoRegionId_BoundaryVersion_ObservationStar~",
                table: "GeoAnalysisJobs",
                columns: new[] { "GeoRegionId", "BoundaryVersion", "ObservationStartAt", "ObservationEndAt", "AlgorithmVersion", "Parameters" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeoAnalysisJobs_Status_LeaseExpiresAt_CreatedAt",
                table: "GeoAnalysisJobs",
                columns: new[] { "Status", "LeaseExpiresAt", "CreatedAt" });
        }
    }
}
