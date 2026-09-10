using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class CompleteClassSessionManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subjects_SubjectName_AcademicLevel",
                table: "Subjects");

            migrationBuilder.AlterColumn<string>(
                name: "Venue",
                table: "ClassSessions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                table: "ClassSessions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_EducationStage_SubjectName_AcademicLevel",
                table: "Subjects",
                columns: new[] { "EducationStage", "SubjectName", "AcademicLevel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_ClassName",
                table: "ClassSessions",
                column: "ClassName",
                unique: true,
                filter: "[ClassName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subjects_EducationStage_SubjectName_AcademicLevel",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_ClassSessions_ClassName",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "Remark",
                table: "ClassSessions");

            migrationBuilder.AlterColumn<string>(
                name: "Venue",
                table: "ClassSessions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_SubjectName_AcademicLevel",
                table: "Subjects",
                columns: new[] { "SubjectName", "AcademicLevel" },
                unique: true);
        }
    }
}
