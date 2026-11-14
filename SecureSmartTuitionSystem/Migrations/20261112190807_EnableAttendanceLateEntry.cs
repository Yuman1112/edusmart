using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class EnableAttendanceLateEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LateSubmissionReason",
                table: "AttendanceLessons",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SubmittedLate",
                table: "AttendanceLessons",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AttendanceReopenRequests",
                columns: table => new
                {
                    AttendanceReopenRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassSessionId = table.Column<int>(type: "int", nullable: false),
                    OriginalDate = table.Column<DateTime>(type: "date", nullable: false),
                    LessonDate = table.Column<DateTime>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsReplacement = table.Column<bool>(type: "bit", nullable: false),
                    ChangeId = table.Column<int>(type: "int", nullable: true),
                    TutorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ClassName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TutorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpenUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceReopenRequests", x => x.AttendanceReopenRequestId);
                    table.ForeignKey(
                        name: "FK_AttendanceReopenRequests_ClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "ClassSessions",
                        principalColumn: "ClassSessionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceReopenRequests_ClassSessionId_OriginalDate_IsReplacement",
                table: "AttendanceReopenRequests",
                columns: new[] { "ClassSessionId", "OriginalDate", "IsReplacement" },
                unique: true,
                filter: "[Decision] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceReopenRequests");

            migrationBuilder.DropColumn(
                name: "LateSubmissionReason",
                table: "AttendanceLessons");

            migrationBuilder.DropColumn(
                name: "SubmittedLate",
                table: "AttendanceLessons");
        }
    }
}
