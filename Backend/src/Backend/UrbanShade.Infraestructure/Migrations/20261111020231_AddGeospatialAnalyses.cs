using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace UrbanShade.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeospatialAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "GeoRegions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Boundary = table.Column<Geometry>(type: "geometry(Geometry,4326)", nullable: false),
                    BoundaryVersion = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeoRegions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeoRegions_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GeoAnalysisJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeoRegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Boundary = table.Column<Geometry>(type: "geometry(Geometry,4326)", nullable: false),
                    BoundaryVersion = table.Column<int>(type: "integer", nullable: false),
                    ObservationStartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ObservationEndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Parameters = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResultSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeoAnalysisJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeoAnalysisJobs_GeoRegions_GeoRegionId",
                        column: x => x.GeoRegionId,
                        principalTable: "GeoRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SatelliteAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeoRegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeoAnalysisJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoundaryVersion = table.Column<int>(type: "integer", nullable: false),
                    DataSource = table.Column<string>(type: "text", nullable: false),
                    AnalysisType = table.Column<string>(type: "text", nullable: false),
                    MeanValue = table.Column<decimal>(type: "numeric", nullable: true),
                    MinValue = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxValue = table.Column<decimal>(type: "numeric", nullable: true),
                    VegetationCoveragePercent = table.Column<decimal>(type: "numeric", nullable: true),
                    VegetationAreaSquareMeters = table.Column<decimal>(type: "numeric", nullable: true),
                    ValidPixelPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    ObservationStartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ObservationEndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DatasetId = table.Column<string>(type: "text", nullable: false),
                    ImageId = table.Column<string>(type: "text", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatelliteAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SatelliteAnalyses_GeoAnalysisJobs_GeoAnalysisJobId",
                        column: x => x.GeoAnalysisJobId,
                        principalTable: "GeoAnalysisJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SatelliteAnalyses_GeoRegions_GeoRegionId",
                        column: x => x.GeoRegionId,
                        principalTable: "GeoRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GreenAreas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Geometry = table.Column<Geometry>(type: "geometry(Geometry,4326)", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    EstimatedAreaSquareMeters = table.Column<decimal>(type: "numeric", nullable: false),
                    ReferenceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GeoRegionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SatelliteAnalysisId = table.Column<Guid>(type: "uuid", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GreenAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GreenAreas_GeoRegions_GeoRegionId",
                        column: x => x.GeoRegionId,
                        principalTable: "GeoRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GreenAreas_SatelliteAnalyses_SatelliteAnalysisId",
                        column: x => x.SatelliteAnalysisId,
                        principalTable: "SatelliteAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeoAnalysisJobs_GeoRegionId_BoundaryVersion_ObservationStar~",
                table: "GeoAnalysisJobs",
                columns: new[] { "GeoRegionId", "BoundaryVersion", "ObservationStartAt", "ObservationEndAt", "AlgorithmVersion", "Parameters" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeoAnalysisJobs_Status_LeaseExpiresAt_CreatedAt",
                table: "GeoAnalysisJobs",
                columns: new[] { "Status", "LeaseExpiresAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GeoRegions_Boundary",
                table: "GeoRegions",
                column: "Boundary")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_GeoRegions_CityId",
                table: "GeoRegions",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_GreenAreas_Geometry",
                table: "GreenAreas",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_GreenAreas_GeoRegionId",
                table: "GreenAreas",
                column: "GeoRegionId");

            migrationBuilder.CreateIndex(
                name: "IX_GreenAreas_SatelliteAnalysisId",
                table: "GreenAreas",
                column: "SatelliteAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_SatelliteAnalyses_GeoAnalysisJobId_AnalysisType",
                table: "SatelliteAnalyses",
                columns: new[] { "GeoAnalysisJobId", "AnalysisType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SatelliteAnalyses_GeoRegionId_BoundaryVersion_AnalysisType_~",
                table: "SatelliteAnalyses",
                columns: new[] { "GeoRegionId", "BoundaryVersion", "AnalysisType", "ObservationEndAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GreenAreas");

            migrationBuilder.DropTable(
                name: "SatelliteAnalyses");

            migrationBuilder.DropTable(
                name: "GeoAnalysisJobs");

            migrationBuilder.DropTable(
                name: "GeoRegions");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
