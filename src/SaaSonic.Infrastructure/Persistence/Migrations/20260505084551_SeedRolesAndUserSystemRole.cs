using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SaaSonic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedRolesAndUserSystemRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SystemRoleId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name", "Scope" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "SystemAdmin", 0 },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "Owner", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "Admin", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000004"), "Editor", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000005"), "Viewer", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_SystemRoleId",
                table: "Users",
                column: "SystemRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_SystemRoleId",
                table: "Users",
                column: "SystemRoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_SystemRoleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_SystemRoleId",
                table: "Users");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"));

            migrationBuilder.DropColumn(
                name: "SystemRoleId",
                table: "Users");
        }
    }
}
