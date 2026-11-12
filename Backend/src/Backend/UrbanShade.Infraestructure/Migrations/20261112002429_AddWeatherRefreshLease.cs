using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanShade.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeatherRefreshLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WeatherLeaseExpiresAt",
                table: "GeoRegions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WeatherLeaseId",
                table: "GeoRegions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeatherLeaseExpiresAt",
                table: "GeoRegions");

            migrationBuilder.DropColumn(
                name: "WeatherLeaseId",
                table: "GeoRegions");
        }
    }
}
