using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSonic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TemplateSlug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Placeholders = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingEmails", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingEmails_Status_NextRetryAt",
                table: "PendingEmails",
                columns: new[] { "Status", "NextRetryAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingEmails");
        }
    }
}
