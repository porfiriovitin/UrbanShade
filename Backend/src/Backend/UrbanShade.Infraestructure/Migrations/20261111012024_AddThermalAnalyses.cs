using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanShade.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddThermalAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ThermalAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SensorReadingId = table.Column<Guid>(type: "uuid", nullable: false),
                    HeatIndex = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RiskLevel = table.Column<string>(type: "text", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThermalAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThermalAnalyses_SensorReadings_SensorReadingId",
                        column: x => x.SensorReadingId,
                        principalTable: "SensorReadings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThermalAnalyses_SensorReadingId_AlgorithmVersion",
                table: "ThermalAnalyses",
                columns: new[] { "SensorReadingId", "AlgorithmVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThermalAnalyses");
        }
    }
}
