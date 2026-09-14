using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyParentStudentRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentProfiles_ParentProfiles_ParentProfileId",
                table: "StudentProfiles");

            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_ParentProfileId",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "ParentProfileId",
                table: "StudentProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentProfileId",
                table: "StudentProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_ParentProfileId",
                table: "StudentProfiles",
                column: "ParentProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentProfiles_ParentProfiles_ParentProfileId",
                table: "StudentProfiles",
                column: "ParentProfileId",
                principalTable: "ParentProfiles",
                principalColumn: "ParentProfileId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
