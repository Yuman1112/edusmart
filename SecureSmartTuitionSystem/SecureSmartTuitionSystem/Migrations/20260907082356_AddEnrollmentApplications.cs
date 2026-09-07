using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnrollmentApplications",
                columns: table => new
                {
                    EnrollmentApplicationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ExistingParentProfileId = table.Column<int>(type: "int", nullable: true),
                    ExistingStudentProfileId = table.Column<int>(type: "int", nullable: true),
                    GuardianName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GuardianEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GuardianPhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    GuardianIdentificationLast4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    StudentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StudentDateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcademicLevel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestedStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HardcopyMaterialsRequested = table.Column<bool>(type: "bit", nullable: false),
                    FinalTwoWeeksConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PotentialDuplicateStudent = table.Column<bool>(type: "bit", nullable: false),
                    PotentialDuplicateGuardian = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApplicantNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentApplications", x => x.EnrollmentApplicationId);
                    table.ForeignKey(
                        name: "FK_EnrollmentApplications_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentApplications_AspNetUsers_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentApplications_ParentProfiles_ExistingParentProfileId",
                        column: x => x.ExistingParentProfileId,
                        principalTable: "ParentProfiles",
                        principalColumn: "ParentProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentApplications_StudentProfiles_ExistingStudentProfileId",
                        column: x => x.ExistingStudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "StudentProfileId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EnrollmentApplicationSubjects",
                columns: table => new
                {
                    EnrollmentApplicationSubjectId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrollmentApplicationId = table.Column<int>(type: "int", nullable: false),
                    ClassSessionId = table.Column<int>(type: "int", nullable: false),
                    MonthlyFeeAtApplication = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentApplicationSubjects", x => x.EnrollmentApplicationSubjectId);
                    table.ForeignKey(
                        name: "FK_EnrollmentApplicationSubjects_ClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "ClassSessions",
                        principalColumn: "ClassSessionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentApplicationSubjects_EnrollmentApplications_EnrollmentApplicationId",
                        column: x => x.EnrollmentApplicationId,
                        principalTable: "EnrollmentApplications",
                        principalColumn: "EnrollmentApplicationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentApplications_ApplicationNumber",
                table: "EnrollmentApplications",
                column: "ApplicationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentApplications_ExistingParentProfileId",
                table: "EnrollmentApplications",
                column: "ExistingParentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentApplications_ExistingStudentProfileId",
                table: "EnrollmentApplications",
                column: "ExistingStudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentApplications_ReviewedByUserId",
                table: "EnrollmentApplications",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentApplications_SubmittedByUserId",
                table: "EnrollmentApplications",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentApplicationSubjects_ClassSessionId",
                table: "EnrollmentApplicationSubjects",
                column: "ClassSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentApplicationSubjects_EnrollmentApplicationId_ClassSessionId",
                table: "EnrollmentApplicationSubjects",
                columns: new[] { "EnrollmentApplicationId", "ClassSessionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnrollmentApplicationSubjects");

            migrationBuilder.DropTable(
                name: "EnrollmentApplications");
        }
    }
}
