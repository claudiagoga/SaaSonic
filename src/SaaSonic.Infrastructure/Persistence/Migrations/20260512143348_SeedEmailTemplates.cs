using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSonic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedEmailTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var now = new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero);

            var emailVerificationBody =
                "<p>Hi {Name},</p>" +
                "<p>Thanks for signing up! Please verify your email address by clicking the link below:</p>" +
                "<p><a href=\"{CallbackUrl}\">Verify my email</a></p>" +
                "<p>This link expires in 24 hours. If you did not create an account, you can safely ignore this email.</p>";

            var passwordResetBody =
                "<p>Hi {Name},</p>" +
                "<p>We received a request to reset your password. Click the link below to choose a new one:</p>" +
                "<p><a href=\"{CallbackUrl}\">Reset my password</a></p>" +
                "<p>This link expires in 1 hour. If you did not request a password reset, you can safely ignore this email.</p>";

            var welcomeBody =
                "<p>Hi {Name},</p>" +
                "<p>Your email has been verified and your account is ready to go.</p>" +
                "<p>Head back to the app to get started.</p>";

            var workspaceInvitationBody =
                "<p>Hi there,</p>" +
                "<p><strong>{InviterName}</strong> has invited you to join <strong>{WorkspaceName}</strong>.</p>" +
                "<p>Use the token below to accept the invitation (it expires in 7 days):</p>" +
                "<p><code>{Token}</code></p>" +
                "<p>If you were not expecting this invitation, you can safely ignore this email.</p>";

            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Slug", "Subject", "Body", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { "email-verification", "Verify your email address",          emailVerificationBody,    now, now },
                    { "password-reset",     "Reset your password",                passwordResetBody,        now, now },
                    { "welcome",            "Welcome to the platform!",           welcomeBody,              now, now },
                    { "workspace-invitation", "You've been invited to join {WorkspaceName}", workspaceInvitationBody, now, now },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Slug",
                keyValues: new object[]
                {
                    "email-verification",
                    "password-reset",
                    "welcome",
                    "workspace-invitation",
                });
        }
    }
}
