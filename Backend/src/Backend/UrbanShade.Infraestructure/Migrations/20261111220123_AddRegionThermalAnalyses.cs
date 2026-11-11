using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanShade.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionThermalAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceGeoRegionId",
                table: "GeoRegions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RegionThermalAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeoRegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoundaryVersion = table.Column<int>(type: "integer", nullable: false),
                    ExposureIndex = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ExposureLevel = table.Column<string>(type: "text", nullable: true),
                    AverageHeatIndex = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    SurfaceTemperature = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    SurfaceTemperatureAnomaly = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    MeanNdvi = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    VegetationCoveragePercent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    SensorCount = table.Column<int>(type: "integer", nullable: false),
                    IsHeatIslandCandidate = table.Column<bool>(type: "boolean", nullable: true),
                    DataQuality = table.Column<string>(type: "text", nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "text", nullable: false),
                    DataOrigin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InputSignature = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceMetadata = table.Column<string>(type: "jsonb", nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegionThermalAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegionThermalAnalyses_GeoRegions_GeoRegionId",
                        column: x => x.GeoRegionId,
                        principalTable: "GeoRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeoRegions_ReferenceGeoRegionId",
                table: "GeoRegions",
                column: "ReferenceGeoRegionId");

            migrationBuilder.CreateIndex(
                name: "IX_RegionThermalAnalyses_GeoRegionId_BoundaryVersion_AnalyzedAt",
                table: "RegionThermalAnalyses",
                columns: new[] { "GeoRegionId", "BoundaryVersion", "AnalyzedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_GeoRegions_GeoRegions_ReferenceGeoRegionId",
                table: "GeoRegions",
                column: "ReferenceGeoRegionId",
                principalTable: "GeoRegions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GeoRegions_GeoRegions_ReferenceGeoRegionId",
                table: "GeoRegions");

            migrationBuilder.DropTable(
                name: "RegionThermalAnalyses");

            migrationBuilder.DropIndex(
                name: "IX_GeoRegions_ReferenceGeoRegionId",
                table: "GeoRegions");

            migrationBuilder.DropColumn(
                name: "ReferenceGeoRegionId",
                table: "GeoRegions");
        }
    }
}
