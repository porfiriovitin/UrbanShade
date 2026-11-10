using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanShade.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AuthenticationTokensAndTextRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Users" ALTER COLUMN "Role" TYPE text
                USING CASE "Role" WHEN 0 THEN 'PlatformAdministrator' WHEN 1 THEN 'Citizen' WHEN 2 THEN 'Manager' END;
                DELETE FROM "RefreshTokens";
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId",
                table: "RefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_FamilyId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "RefreshTokens");

            migrationBuilder.Sql("""
                ALTER TABLE "Users" ALTER COLUMN "Role" TYPE integer
                USING CASE "Role" WHEN 'PlatformAdministrator' THEN 0 WHEN 'Citizen' THEN 1 WHEN 'Manager' THEN 2 END;
                """);
        }
    }
}
