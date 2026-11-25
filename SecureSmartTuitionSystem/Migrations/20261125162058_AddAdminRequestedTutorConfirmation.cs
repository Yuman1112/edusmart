using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminRequestedTutorConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfirmationRequestedByName",
                table: "AttendanceCorrectionRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationRequestedByUserId",
                table: "AttendanceCorrectionRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationTutorUserId",
                table: "AttendanceCorrectionRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsStudentRequest",
                table: "AttendanceCorrectionRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TutorConfirmationRemark",
                table: "AttendanceCorrectionRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TutorConfirmationRequestedAtUtc",
                table: "AttendanceCorrectionRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TutorConfirmedAtUtc",
                table: "AttendanceCorrectionRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TutorConfirmedByName",
                table: "AttendanceCorrectionRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TutorConfirmedByUserId",
                table: "AttendanceCorrectionRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TutorSupportsCorrection",
                table: "AttendanceCorrectionRequests",
                type: "bit",
                nullable: true);

            // Classify existing requests only. No Tutor confirmation is requested automatically.
            migrationBuilder.Sql(@"
UPDATE r SET IsStudentRequest = 1
FROM AttendanceCorrectionRequests r
WHERE EXISTS (
 SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles role ON role.Id = ur.RoleId
 WHERE ur.UserId = r.RequestedByUserId AND role.NormalizedName = 'STUDENT')
AND NOT EXISTS (
 SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles role ON role.Id = ur.RoleId
 WHERE ur.UserId = r.RequestedByUserId AND role.NormalizedName IN ('ADMIN','TUTOR'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmationRequestedByName",
                table: "AttendanceCorrectionRequests");

            migrationBuilder.DropColumn(
                name: "ConfirmationRequestedByUserId",
                table: "AttendanceCorrectionRequests");

            migrationBuilder.DropColumn(
                name: "ConfirmationTutorUserId",
                table: "AttendanceCorrectionRequests");

            migrationBuilder.DropColumn(
                name: "IsStudentRequest",
                table: "AttendanceCorrectionRequests");

            migrationBuilder.DropColumn(
                name: "TutorConfirmationRemark",
                table: "AttendanceCorrectionRequests");

            migrationBuilder.DropColumn(
                name: "TutorConfirmationRequestedAtUtc",
                table: "AttendanceCorrectionRequests");

            migrationBuilder.DropColumn(
                name: "TutorConfirmedAtUtc",
                table: "AttendanceCorrectionRequests");

            migrationBuilder.DropColumn(
                name: "TutorConfirmedByName",
                table: "AttendanceCorrectionRequests");

            migrationBuilder.DropColumn(
                name: "TutorConfirmedByUserId",
                table: "AttendanceCorrectionRequests");

            migrationBuilder.DropColumn(
                name: "TutorSupportsCorrection",
                table: "AttendanceCorrectionRequests");
        }
    }
}
