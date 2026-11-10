using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddWithdrawalCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EnrollmentExitRequests_StudentEnrollmentId",
                table: "EnrollmentExitRequests");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "EnrollmentExitRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByUserId",
                table: "EnrollmentExitRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentExitRequests_StudentEnrollmentId",
                table: "EnrollmentExitRequests",
                column: "StudentEnrollmentId",
                unique: true,
                filter: "[CancelledAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EnrollmentExitRequests_StudentEnrollmentId",
                table: "EnrollmentExitRequests");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "EnrollmentExitRequests");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "EnrollmentExitRequests");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentExitRequests_StudentEnrollmentId",
                table: "EnrollmentExitRequests",
                column: "StudentEnrollmentId",
                unique: true);
        }
    }
}
