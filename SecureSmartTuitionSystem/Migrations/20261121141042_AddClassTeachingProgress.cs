using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddClassTeachingProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgressSyllabuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    AcademicYear = table.Column<int>(type: "int", nullable: false),
                    TrackingFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressSyllabuses", x => x.Id);
                    table.CheckConstraint("CK_ProgressSyllabus_Year", "[AcademicYear] BETWEEN 2000 AND 2100 AND YEAR([TrackingFrom]) = [AcademicYear]");
                    table.ForeignKey(
                        name: "FK_ProgressSyllabuses_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgressClassStates",
                columns: table => new
                {
                    ClassSessionId = table.Column<int>(type: "int", nullable: false),
                    SyllabusId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressClassStates", x => x.ClassSessionId);
                    table.ForeignKey(
                        name: "FK_ProgressClassStates_ClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "ClassSessions",
                        principalColumn: "ClassSessionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgressClassStates_ProgressSyllabuses_SyllabusId",
                        column: x => x.SyllabusId,
                        principalTable: "ProgressSyllabuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgressTopics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SyllabusId = table.Column<int>(type: "int", nullable: false),
                    Chapter = table.Column<int>(type: "int", nullable: false),
                    Subchapter = table.Column<int>(type: "int", nullable: false),
                    ChapterTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressTopics", x => x.Id);
                    table.CheckConstraint("CK_ProgressTopic_Numbers", "[Chapter] BETWEEN 1 AND 20 AND [Subchapter] BETWEEN 1 AND 20");
                    table.ForeignKey(
                        name: "FK_ProgressTopics_ProgressSyllabuses_SyllabusId",
                        column: x => x.SyllabusId,
                        principalTable: "ProgressSyllabuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgressLessonRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassSessionId = table.Column<int>(type: "int", nullable: false),
                    LessonDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangeKey = table.Column<int>(type: "int", nullable: false),
                    OriginalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Start = table.Column<TimeOnly>(type: "time", nullable: false),
                    End = table.Column<TimeOnly>(type: "time", nullable: false),
                    TutorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedLate = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LateReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressLessonRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgressLessonRecords_ProgressClassStates_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "ProgressClassStates",
                        principalColumn: "ClassSessionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgressTopicStates",
                columns: table => new
                {
                    ClassSessionId = table.Column<int>(type: "int", nullable: false),
                    TopicId = table.Column<int>(type: "int", nullable: false),
                    Percent = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressTopicStates", x => new { x.ClassSessionId, x.TopicId });
                    table.CheckConstraint("CK_ProgressTopicState_Percent", "[Percent] IN (0,25,50,75,100) AND (([Percent] = 100 AND [CompletedAtUtc] IS NOT NULL) OR ([Percent] < 100 AND [CompletedAtUtc] IS NULL))");
                    table.ForeignKey(
                        name: "FK_ProgressTopicStates_ProgressClassStates_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "ProgressClassStates",
                        principalColumn: "ClassSessionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgressTopicStates_ProgressTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "ProgressTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgressChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassSessionId = table.Column<int>(type: "int", nullable: false),
                    LessonRecordId = table.Column<int>(type: "int", nullable: true),
                    TopicId = table.Column<int>(type: "int", nullable: true),
                    Before = table.Column<int>(type: "int", nullable: true),
                    After = table.Column<int>(type: "int", nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LateReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgressChanges_ProgressClassStates_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "ProgressClassStates",
                        principalColumn: "ClassSessionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgressChanges_ProgressLessonRecords_LessonRecordId",
                        column: x => x.LessonRecordId,
                        principalTable: "ProgressLessonRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgressChanges_ProgressTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "ProgressTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgressChanges_ClassSessionId_RecordedAtUtc",
                table: "ProgressChanges",
                columns: new[] { "ClassSessionId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgressChanges_LessonRecordId",
                table: "ProgressChanges",
                column: "LessonRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressChanges_TopicId",
                table: "ProgressChanges",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressClassStates_SyllabusId",
                table: "ProgressClassStates",
                column: "SyllabusId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressLessonRecords_ClassSessionId_LessonDate_ChangeKey",
                table: "ProgressLessonRecords",
                columns: new[] { "ClassSessionId", "LessonDate", "ChangeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgressSyllabuses_SubjectId_AcademicYear",
                table: "ProgressSyllabuses",
                columns: new[] { "SubjectId", "AcademicYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgressTopics_SyllabusId_Chapter_Subchapter",
                table: "ProgressTopics",
                columns: new[] { "SyllabusId", "Chapter", "Subchapter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgressTopicStates_TopicId",
                table: "ProgressTopicStates",
                column: "TopicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [ProgressClassStates]) THROW 51000, 'Teaching progress exists. Restore a backup or review an explicit data-preserving rollback; automatic rollback is blocked.', 1;");
            migrationBuilder.DropTable(
                name: "ProgressChanges");

            migrationBuilder.DropTable(
                name: "ProgressTopicStates");

            migrationBuilder.DropTable(
                name: "ProgressLessonRecords");

            migrationBuilder.DropTable(
                name: "ProgressTopics");

            migrationBuilder.DropTable(
                name: "ProgressClassStates");

            migrationBuilder.DropTable(
                name: "ProgressSyllabuses");
        }
    }
}
