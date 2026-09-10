using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentPlanDeliveryAndFeeSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StudentIdentificationNumber",
                table: "EnrollmentApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StudentGender",
                table: "EnrollmentApplications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GuardianIdentificationNumber",
                table: "EnrollmentApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(14)",
                oldMaxLength: 14,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddressLine1",
                table: "EnrollmentApplications",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddressLine2",
                table: "EnrollmentApplications",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCity",
                table: "EnrollmentApplications",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPostcode",
                table: "EnrollmentApplications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryRecipientName",
                table: "EnrollmentApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryRegion",
                table: "EnrollmentApplications",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryState",
                table: "EnrollmentApplications",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmountAtApplication",
                table: "EnrollmentApplications",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountRateAtApplication",
                table: "EnrollmentApplications",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountedTuitionAtApplication",
                table: "EnrollmentApplications",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "EnrollmentDurationMonths",
                table: "EnrollmentApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedTotalAtApplication",
                table: "EnrollmentApplications",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HardcopyFeeAtApplication",
                table: "EnrollmentApplications",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "HardcopySubjectCountAtApplication",
                table: "EnrollmentApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyTuitionFeeAtApplication",
                table: "EnrollmentApplications",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFeeAtApplication",
                table: "EnrollmentApplications",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TuitionSubtotalAtApplication",
                table: "EnrollmentApplications",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryAddressLine1",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "DeliveryAddressLine2",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "DeliveryCity",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "DeliveryPostcode",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "DeliveryRecipientName",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "DeliveryRegion",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "DeliveryState",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "DiscountAmountAtApplication",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "DiscountRateAtApplication",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "DiscountedTuitionAtApplication",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "EnrollmentDurationMonths",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "EstimatedTotalAtApplication",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "HardcopyFeeAtApplication",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "HardcopySubjectCountAtApplication",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "MonthlyTuitionFeeAtApplication",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "ShippingFeeAtApplication",
                table: "EnrollmentApplications");

            migrationBuilder.DropColumn(
                name: "TuitionSubtotalAtApplication",
                table: "EnrollmentApplications");

            migrationBuilder.AlterColumn<string>(
                name: "StudentIdentificationNumber",
                table: "EnrollmentApplications",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StudentGender",
                table: "EnrollmentApplications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GuardianIdentificationNumber",
                table: "EnrollmentApplications",
                type: "nvarchar(14)",
                maxLength: 14,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
