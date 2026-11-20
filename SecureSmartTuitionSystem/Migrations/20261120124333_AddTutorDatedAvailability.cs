using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorDatedAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TutorAvailabilityAdjustments",
                columns: table => new
                {
                    ClassLessonChangeId = table.Column<int>(type: "int", nullable: false),
                    TutorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    BlocksOriginal = table.Column<bool>(type: "bit", nullable: false),
                    AddedDate = table.Column<DateTime>(type: "date", nullable: true),
                    AddedStart = table.Column<TimeOnly>(type: "time", nullable: true),
                    AddedEnd = table.Column<TimeOnly>(type: "time", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorAvailabilityAdjustments", x => x.ClassLessonChangeId);
                    table.ForeignKey(
                        name: "FK_TutorAvailabilityAdjustments_ClassLessonChanges_ClassLessonChangeId",
                        column: x => x.ClassLessonChangeId,
                        principalTable: "ClassLessonChanges",
                        principalColumn: "ClassLessonChangeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorAvailabilityAdjustments_TutorUserId_AddedDate",
                table: "TutorAvailabilityAdjustments",
                columns: new[] { "TutorUserId", "AddedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorAvailabilityAdjustments");
        }
    }
}
