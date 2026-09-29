using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    public partial class RemoveLegacyDuplicateReviewColumns
        : Migration
    {
        protected override void Up(
            MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'EnrollmentApplications',
                    'PotentialDuplicateStudent'
                ) IS NOT NULL
                BEGIN
                    ALTER TABLE [EnrollmentApplications]
                    DROP COLUMN [PotentialDuplicateStudent];
                END
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'EnrollmentApplications',
                    'PotentialDuplicateGuardian'
                ) IS NOT NULL
                BEGIN
                    ALTER TABLE [EnrollmentApplications]
                    DROP COLUMN [PotentialDuplicateGuardian];
                END
                """);
        }

        protected override void Down(
            MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'EnrollmentApplications',
                    'PotentialDuplicateStudent'
                ) IS NULL
                BEGIN
                    ALTER TABLE [EnrollmentApplications]
                    ADD [PotentialDuplicateStudent]
                        bit NOT NULL
                        CONSTRAINT [DF_EnrollmentApplications_PotentialDuplicateStudent]
                        DEFAULT CAST(0 AS bit);
                END
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'EnrollmentApplications',
                    'PotentialDuplicateGuardian'
                ) IS NULL
                BEGIN
                    ALTER TABLE [EnrollmentApplications]
                    ADD [PotentialDuplicateGuardian]
                        bit NOT NULL
                        CONSTRAINT [DF_EnrollmentApplications_PotentialDuplicateGuardian]
                        DEFAULT CAST(0 AS bit);
                END
                """);
        }
    }
}