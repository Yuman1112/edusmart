using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddParentStudentRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParentStudents",
                columns: table => new
                {
                    ParentStudentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentProfileId = table.Column<int>(type: "int", nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsPrimaryGuardian = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentStudents", x => x.ParentStudentId);
                    table.ForeignKey(
                        name: "FK_ParentStudents_ParentProfiles_ParentProfileId",
                        column: x => x.ParentProfileId,
                        principalTable: "ParentProfiles",
                        principalColumn: "ParentProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentStudents_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "StudentProfileId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudents_ParentProfileId_StudentProfileId",
                table: "ParentStudents",
                columns: new[] { "ParentProfileId", "StudentProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudents_StudentProfileId",
                table: "ParentStudents",
                column: "StudentProfileId");

            migrationBuilder.Sql(
                """
                INSERT INTO ParentStudents
                (
                    ParentProfileId,
                    StudentProfileId,
                    Relationship,
                    IsPrimaryGuardian,
                    IsActive,
                    LinkedAt,
                    EndedAt
                )
                SELECT
                    ParentProfileId,
                    StudentProfileId,
                    NULL,
                    1,
                    1,
                    GETUTCDATE(),
                    NULL
                FROM StudentProfiles;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParentStudents");
        }
    }
}
