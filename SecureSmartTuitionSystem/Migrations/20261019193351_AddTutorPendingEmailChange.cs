using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorPendingEmailChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingTutorEmail",
                table: "AspNetUsers",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TutorEmailChangeExpiresAtUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TutorEmailChangeRequestId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TutorEmailChangeRequestedAtUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TutorEmailChangeRequestedByUserId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingTutorEmail",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TutorEmailChangeExpiresAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TutorEmailChangeRequestId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TutorEmailChangeRequestedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TutorEmailChangeRequestedByUserId",
                table: "AspNetUsers");
        }
    }
}
