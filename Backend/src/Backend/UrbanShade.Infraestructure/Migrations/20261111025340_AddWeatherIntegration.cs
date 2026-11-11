using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanShade.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeatherIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WeatherRetryAfter",
                table: "GeoRegions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WeatherRequestedAt",
                table: "GeoRegions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WeatherForecasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeoRegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoundaryVersion = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Temperature = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Humidity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ApparentTemperature = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PrecipitationProbability = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    WindSpeed = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CloudCover = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    UvIndex = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    SolarRadiation = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ForecastAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherForecasts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeatherForecasts_GeoRegions_GeoRegionId",
                        column: x => x.GeoRegionId,
                        principalTable: "GeoRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeatherSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeoRegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoundaryVersion = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Temperature = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Humidity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ApparentTemperature = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    WindSpeed = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Precipitation = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CloudCover = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    WeatherCode = table.Column<int>(type: "integer", nullable: true),
                    ObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeatherSnapshots_GeoRegions_GeoRegionId",
                        column: x => x.GeoRegionId,
                        principalTable: "GeoRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherForecasts_GeoRegionId_BoundaryVersion_ForecastAt",
                table: "WeatherForecasts",
                columns: new[] { "GeoRegionId", "BoundaryVersion", "ForecastAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherForecasts_GeoRegionId_Source_ForecastAt",
                table: "WeatherForecasts",
                columns: new[] { "GeoRegionId", "Source", "ForecastAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeatherSnapshots_GeoRegionId_BoundaryVersion_FetchedAt",
                table: "WeatherSnapshots",
                columns: new[] { "GeoRegionId", "BoundaryVersion", "FetchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherSnapshots_GeoRegionId_ObservedAt",
                table: "WeatherSnapshots",
                columns: new[] { "GeoRegionId", "ObservedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeatherRetryAfter",
                table: "GeoRegions");

            migrationBuilder.DropTable(
                name: "WeatherForecasts");

            migrationBuilder.DropTable(
                name: "WeatherSnapshots");

            migrationBuilder.DropColumn(
                name: "WeatherRequestedAt",
                table: "GeoRegions");
        }
    }
}
