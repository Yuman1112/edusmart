using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assessments",
                columns: table => new
                {
                    AssessmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassSessionId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    MaxMarks = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    OpensAtMyt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueAtMyt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResultsPublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.AssessmentId);
                    table.CheckConstraint("CK_Assessment_Dates", "[DueAtMyt] > [OpensAtMyt]");
                    table.CheckConstraint("CK_Assessment_Kind", "[Kind] IN (1,2,3)");
                    table.CheckConstraint("CK_Assessment_Marks", "[MaxMarks] > 0 AND [MaxMarks] <= 10000");
                    table.ForeignKey(
                        name: "FK_Assessments_ClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "ClassSessions",
                        principalColumn: "ClassSessionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentParticipants",
                columns: table => new
                {
                    AssessmentParticipantId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssessmentId = table.Column<int>(type: "int", nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    StudentNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmissionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Marks = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    Feedback = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    GradedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    GradedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentParticipants", x => x.AssessmentParticipantId);
                    table.CheckConstraint("CK_AssessmentParticipant_Grade", "([Marks] IS NULL AND [GradedAtUtc] IS NULL) OR ([SubmittedAtUtc] IS NOT NULL AND [Marks] IS NOT NULL AND [GradedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_AssessmentParticipant_Marks", "[Marks] IS NULL OR ([Marks] >= 0 AND [Marks] <= 10000)");
                    table.ForeignKey(
                        name: "FK_AssessmentParticipants_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "AssessmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentParticipants_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "StudentProfileId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentFiles",
                columns: table => new
                {
                    AssessmentFileId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssessmentId = table.Column<int>(type: "int", nullable: false),
                    AssessmentParticipantId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentFiles", x => x.AssessmentFileId);
                    table.CheckConstraint("CK_AssessmentFile_Size", "[FileSize] > 0 AND [FileSize] <= 10485760 AND DATALENGTH([Content]) = [FileSize]");
                    table.ForeignKey(
                        name: "FK_AssessmentFiles_AssessmentParticipants_AssessmentParticipantId",
                        column: x => x.AssessmentParticipantId,
                        principalTable: "AssessmentParticipants",
                        principalColumn: "AssessmentParticipantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentFiles_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "AssessmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentFiles_AssessmentId_AssessmentParticipantId",
                table: "AssessmentFiles",
                columns: new[] { "AssessmentId", "AssessmentParticipantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentFiles_AssessmentParticipantId",
                table: "AssessmentFiles",
                column: "AssessmentParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentParticipants_AssessmentId_StudentProfileId",
                table: "AssessmentParticipants",
                columns: new[] { "AssessmentId", "StudentProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentParticipants_StudentProfileId",
                table: "AssessmentParticipants",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_ClassSessionId_IsActive_DueAtMyt",
                table: "Assessments",
                columns: new[] { "ClassSessionId", "IsActive", "DueAtMyt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentFiles");

            migrationBuilder.DropTable(
                name: "AssessmentParticipants");

            migrationBuilder.DropTable(
                name: "Assessments");
        }
    }
}
