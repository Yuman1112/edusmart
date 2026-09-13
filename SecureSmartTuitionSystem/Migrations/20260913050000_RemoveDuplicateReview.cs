using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SecureSmartTuitionSystem.Data;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260913050000_RemoveDuplicateReview")]
    public partial class RemoveDuplicateReview : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE EnrollmentApplications SET Status = 0 WHERE Status = 1;");

            migrationBuilder.DropColumn(
                name: "PotentialDuplicateGuardian",
                table: "EnrollmentApplications");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PotentialDuplicateGuardian",
                table: "EnrollmentApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
