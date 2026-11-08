using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoPlaybackVault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecordedLessons",
                columns: table => new
                {
                    RecordedLessonId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassSessionId = table.Column<int>(type: "int", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecordingDate = table.Column<DateTime>(type: "date", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    DurationSeconds = table.Column<double>(type: "float", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordedLessons", x => x.RecordedLessonId);
                    table.CheckConstraint("CK_RecordedLesson_Duration", "[DurationSeconds] > 0 AND [DurationSeconds] <= 28800");
                    table.CheckConstraint("CK_RecordedLesson_Size", "[FileSize] > 0 AND [FileSize] <= 209715200");
                    table.ForeignKey(
                        name: "FK_RecordedLessons_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecordedLessons_ClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "ClassSessions",
                        principalColumn: "ClassSessionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VideoWatchProgress",
                columns: table => new
                {
                    VideoWatchProgressId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordedLessonId = table.Column<int>(type: "int", nullable: false),
                    StudentUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PositionSeconds = table.Column<double>(type: "float", nullable: false),
                    WatchedSeconds = table.Column<double>(type: "float", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoWatchProgress", x => x.VideoWatchProgressId);
                    table.CheckConstraint("CK_VideoWatch_Seconds", "[PositionSeconds] >= 0 AND [WatchedSeconds] >= 0");
                    table.ForeignKey(
                        name: "FK_VideoWatchProgress_AspNetUsers_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoWatchProgress_RecordedLessons_RecordedLessonId",
                        column: x => x.RecordedLessonId,
                        principalTable: "RecordedLessons",
                        principalColumn: "RecordedLessonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecordedLessons_ClassSessionId_IsActive_RecordingDate",
                table: "RecordedLessons",
                columns: new[] { "ClassSessionId", "IsActive", "RecordingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordedLessons_StorageKey",
                table: "RecordedLessons",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecordedLessons_UploadedByUserId",
                table: "RecordedLessons",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoWatchProgress_RecordedLessonId_StudentUserId",
                table: "VideoWatchProgress",
                columns: new[] { "RecordedLessonId", "StudentUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoWatchProgress_StudentUserId",
                table: "VideoWatchProgress",
                column: "StudentUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoWatchProgress");

            migrationBuilder.DropTable(
                name: "RecordedLessons");
        }
    }
}
