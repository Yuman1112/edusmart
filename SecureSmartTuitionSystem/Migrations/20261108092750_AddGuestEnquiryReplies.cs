using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestEnquiryReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProtectedReply",
                table: "GuestEnquiries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAtUtc",
                table: "GuestEnquiries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplyAttemptId",
                table: "GuestEnquiries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplySentAtUtc",
                table: "GuestEnquiries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplyState",
                table: "GuestEnquiries",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProtectedReply",
                table: "GuestEnquiries");

            migrationBuilder.DropColumn(
                name: "ReadAtUtc",
                table: "GuestEnquiries");

            migrationBuilder.DropColumn(
                name: "ReplyAttemptId",
                table: "GuestEnquiries");

            migrationBuilder.DropColumn(
                name: "ReplySentAtUtc",
                table: "GuestEnquiries");

            migrationBuilder.DropColumn(
                name: "ReplyState",
                table: "GuestEnquiries");
        }
    }
}
