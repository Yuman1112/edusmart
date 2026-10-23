using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorTeachingSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AreaOfExpertise",
                table: "TutorProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateTable(
                name: "TutorTeachingSubjects",
                columns: table => new
                {
                    TutorTeachingSubjectId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorProfileId = table.Column<int>(type: "int", nullable: false),
                    EducationStage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AcademicLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubjectCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorTeachingSubjects", x => x.TutorTeachingSubjectId);
                    table.ForeignKey(
                        name: "FK_TutorTeachingSubjects_TutorProfiles_TutorProfileId",
                        column: x => x.TutorProfileId,
                        principalTable: "TutorProfiles",
                        principalColumn: "TutorProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorTeachingSubjects_TutorProfileId_EducationStage_AcademicLevel_SubjectCode",
                table: "TutorTeachingSubjects",
                columns: new[] { "TutorProfileId", "EducationStage", "AcademicLevel", "SubjectCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorTeachingSubjects");

            migrationBuilder.AlterColumn<string>(
                name: "AreaOfExpertise",
                table: "TutorProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
