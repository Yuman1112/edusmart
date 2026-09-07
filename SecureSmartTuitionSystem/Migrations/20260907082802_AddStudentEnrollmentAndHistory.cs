using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentEnrollmentAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnrollmentStatusHistories",
                columns: table => new
                {
                    EnrollmentStatusHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrollmentApplicationId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentStatusHistories", x => x.EnrollmentStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_EnrollmentStatusHistories_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentStatusHistories_EnrollmentApplications_EnrollmentApplicationId",
                        column: x => x.EnrollmentApplicationId,
                        principalTable: "EnrollmentApplications",
                        principalColumn: "EnrollmentApplicationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentEnrollments",
                columns: table => new
                {
                    StudentEnrollmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    ClassSessionId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentApplicationId = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DropRequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DropEffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HardcopyMaterialsRequested = table.Column<bool>(type: "bit", nullable: false),
                    MonthlyFeeAtEnrollment = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentEnrollments", x => x.StudentEnrollmentId);
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_ClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "ClassSessions",
                        principalColumn: "ClassSessionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_EnrollmentApplications_EnrollmentApplicationId",
                        column: x => x.EnrollmentApplicationId,
                        principalTable: "EnrollmentApplications",
                        principalColumn: "EnrollmentApplicationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "StudentProfileId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentStatusHistories_ChangedByUserId",
                table: "EnrollmentStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentStatusHistories_EnrollmentApplicationId_ChangedAt",
                table: "EnrollmentStatusHistories",
                columns: new[] { "EnrollmentApplicationId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_ClassSessionId",
                table: "StudentEnrollments",
                column: "ClassSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_EnrollmentApplicationId",
                table: "StudentEnrollments",
                column: "EnrollmentApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_StudentProfileId_ClassSessionId",
                table: "StudentEnrollments",
                columns: new[] { "StudentProfileId", "ClassSessionId" },
                unique: true,
                filter: "[Status] IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnrollmentStatusHistories");

            migrationBuilder.DropTable(
                name: "StudentEnrollments");
        }
    }
}
