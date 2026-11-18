using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorQualificationUpdateRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TutorQualificationUpdateRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorProfileId = table.Column<int>(type: "int", nullable: false),
                    Qualification = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FieldOfStudy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AreaOfExpertise = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OriginalQualification = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OriginalFieldOfStudy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OriginalAreaOfExpertise = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OriginalVerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CertificateName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    CertificateContent = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorQualificationUpdateRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorQualificationUpdateRequests_TutorProfiles_TutorProfileId",
                        column: x => x.TutorProfileId,
                        principalTable: "TutorProfiles",
                        principalColumn: "TutorProfileId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorQualificationUpdateRequests_TutorProfileId",
                table: "TutorQualificationUpdateRequests",
                column: "TutorProfileId",
                unique: true,
                filter: "[Status] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorQualificationUpdateRequests");
        }
    }
}
