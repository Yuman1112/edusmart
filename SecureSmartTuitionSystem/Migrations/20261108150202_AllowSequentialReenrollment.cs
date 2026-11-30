using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AllowSequentialReenrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentEnrollments_StudentProfileId_ClassSessionId",
                table: "StudentEnrollments");

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_StudentProfileId_ClassSessionId_StartDate",
                table: "StudentEnrollments",
                columns: new[] { "StudentProfileId", "ClassSessionId", "StartDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentEnrollments_StudentProfileId_ClassSessionId_StartDate",
                table: "StudentEnrollments");

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_StudentProfileId_ClassSessionId",
                table: "StudentEnrollments",
                columns: new[] { "StudentProfileId", "ClassSessionId" },
                unique: true,
                filter: "[Status] IN (0, 1)");
        }
    }
}
