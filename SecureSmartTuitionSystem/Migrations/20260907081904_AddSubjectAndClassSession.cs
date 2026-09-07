using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectAndClassSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    SubjectId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcademicLevel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MonthlyFee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.SubjectId);
                });

            migrationBuilder.CreateTable(
                name: "ClassSessions",
                columns: table => new
                {
                    ClassSessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    TutorUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Venue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSessions", x => x.ClassSessionId);
                    table.ForeignKey(
                        name: "FK_ClassSessions_AspNetUsers_TutorUserId",
                        column: x => x.TutorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSessions_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_ClassCode",
                table: "ClassSessions",
                column: "ClassCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_SubjectId",
                table: "ClassSessions",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_TutorUserId",
                table: "ClassSessions",
                column: "TutorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_SubjectName_AcademicLevel",
                table: "Subjects",
                columns: new[] { "SubjectName", "AcademicLevel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassSessions");

            migrationBuilder.DropTable(
                name: "Subjects");
        }
    }
}
