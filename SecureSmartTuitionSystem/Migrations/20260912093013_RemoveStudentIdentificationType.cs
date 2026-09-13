using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStudentIdentificationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_IdentificationType_IdentificationNumber",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "IdentificationType",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "PotentialDuplicateStudent",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "StudentIdentificationType",
                table: "EnrollmentApplications");

            migrationBuilder.AlterColumn<string>(
                name: "StudentIdentificationNumber",
                table: "EnrollmentApplications",
                type: "nvarchar(14)",
                maxLength: 14,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_IdentificationNumber",
                table: "StudentProfiles",
                column: "IdentificationNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_IdentificationNumber",
                table: "StudentProfiles");

            migrationBuilder.AddColumn<string>(
                name: "IdentificationType",
                table: "StudentProfiles",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "StudentIdentificationNumber",
                table: "EnrollmentApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(14)",
                oldMaxLength: 14);

            migrationBuilder.AddColumn<bool>(
                name: "PotentialDuplicateStudent",
                table: "EnrollmentApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StudentIdentificationType",
                table: "EnrollmentApplications",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_IdentificationType_IdentificationNumber",
                table: "StudentProfiles",
                columns: new[] { "IdentificationType", "IdentificationNumber" },
                unique: true);
        }
    }
}
