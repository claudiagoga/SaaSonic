using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSonic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvoicePlanDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "BillingInterval",
                table: "Invoices",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                table: "Invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PlanId",
                table: "Invoices",
                column: "PlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Plans_PlanId",
                table: "Invoices",
                column: "PlanId",
                principalTable: "Plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Plans_PlanId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PlanId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BillingInterval",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "Invoices");
        }
    }
}
