using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanShade.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCitiesAndSensorLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                table: "Sensors",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    IbgeCode = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sensors_CityId_OrganizationId",
                table: "Sensors",
                columns: new[] { "CityId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Cities_IbgeCode",
                table: "Cities",
                column: "IbgeCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sensors_Cities_CityId",
                table: "Sensors",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sensors_Cities_CityId",
                table: "Sensors");

            migrationBuilder.DropTable(
                name: "Cities");

            migrationBuilder.DropIndex(
                name: "IX_Sensors_CityId_OrganizationId",
                table: "Sensors");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "Sensors");
        }
    }
}
