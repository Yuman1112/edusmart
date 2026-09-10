using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentApplicantSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ApplicationNumber",
                table: "EnrollmentApplications",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "GuardianIdentificationNumber",
                table: "EnrollmentApplications",
                type: "nvarchar(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentGender",
                table: "EnrollmentApplications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentIdentificationNumber",
                table: "EnrollmentApplications",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentIdentificationType",
                table: "EnrollmentApplications",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentSchool",
                table: "EnrollmentApplications",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuardianIdentificationNumber",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "StudentGender",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "StudentIdentificationNumber",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "StudentIdentificationType",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "StudentSchool",
                table: "EnrollmentApplications");

            migrationBuilder.AlterColumn<string>(
                name: "ApplicationNumber",
                table: "EnrollmentApplications",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(7)",
                oldMaxLength: 7);
        }
    }
}
