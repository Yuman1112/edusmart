using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceLessons",
                columns: table => new
                {
                    AttendanceLessonId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassSessionId = table.Column<int>(type: "int", nullable: false),
                    OriginalDate = table.Column<DateTime>(type: "date", nullable: false),
                    IsReplacement = table.Column<bool>(type: "bit", nullable: false),
                    ClassLessonChangeId = table.Column<int>(type: "int", nullable: true),
                    LessonDate = table.Column<DateTime>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    TutorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ClassCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ClassName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TutorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceLessons", x => x.AttendanceLessonId);
                    table.ForeignKey(
                        name: "FK_AttendanceLessons_AspNetUsers_TutorUserId",
                        column: x => x.TutorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceLessons_ClassLessonChanges_ClassLessonChangeId",
                        column: x => x.ClassLessonChangeId,
                        principalTable: "ClassLessonChanges",
                        principalColumn: "ClassLessonChangeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceLessons_ClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "ClassSessions",
                        principalColumn: "ClassSessionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    AttendanceRecordId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceLessonId = table.Column<int>(type: "int", nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    StudentNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.AttendanceRecordId);
                    table.CheckConstraint("CK_AttendanceRecord_Status", "[Status] IS NULL OR [Status] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_AttendanceLessons_AttendanceLessonId",
                        column: x => x.AttendanceLessonId,
                        principalTable: "AttendanceLessons",
                        principalColumn: "AttendanceLessonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "StudentProfileId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceCorrectionRequests",
                columns: table => new
                {
                    AttendanceCorrectionRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceRecordId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    RequestedStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceCorrectionRequests", x => x.AttendanceCorrectionRequestId);
                    table.CheckConstraint("CK_AttendanceCorrection_Decision", "[Decision] IN (0,1,2)");
                    table.CheckConstraint("CK_AttendanceCorrection_Status", "[RequestedStatus] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_AttendanceCorrectionRequests_AttendanceRecords_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "AttendanceRecords",
                        principalColumn: "AttendanceRecordId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrectionRequests_AttendanceRecordId",
                table: "AttendanceCorrectionRequests",
                column: "AttendanceRecordId",
                unique: true,
                filter: "[Decision] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrectionRequests_RequestedByUserId_RequestedAtUtc",
                table: "AttendanceCorrectionRequests",
                columns: new[] { "RequestedByUserId", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLessons_ClassLessonChangeId",
                table: "AttendanceLessons",
                column: "ClassLessonChangeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLessons_ClassSessionId_OriginalDate_IsReplacement",
                table: "AttendanceLessons",
                columns: new[] { "ClassSessionId", "OriginalDate", "IsReplacement" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLessons_TutorUserId_LessonDate",
                table: "AttendanceLessons",
                columns: new[] { "TutorUserId", "LessonDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_AttendanceLessonId_StudentProfileId",
                table: "AttendanceRecords",
                columns: new[] { "AttendanceLessonId", "StudentProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_StudentProfileId",
                table: "AttendanceRecords",
                column: "StudentProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceCorrectionRequests");

            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "AttendanceLessons");
        }
    }
}
