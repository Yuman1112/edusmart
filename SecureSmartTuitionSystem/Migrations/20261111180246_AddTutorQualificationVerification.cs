using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorQualificationVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FieldOfStudy",
                table: "TutorProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QualificationVerifiedAtUtc",
                table: "TutorProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualificationVerifiedByUserId",
                table: "TutorProfiles",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TutorTeachingChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorProfileId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AcademicLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubjectCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorTeachingChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorTeachingChangeRequests_TutorProfiles_TutorProfileId",
                        column: x => x.TutorProfileId,
                        principalTable: "TutorProfiles",
                        principalColumn: "TutorProfileId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TutorQualificationDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorProfileId = table.Column<int>(type: "int", nullable: false),
                    TeachingChangeRequestId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorQualificationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorQualificationDocuments_TutorProfiles_TutorProfileId",
                        column: x => x.TutorProfileId,
                        principalTable: "TutorProfiles",
                        principalColumn: "TutorProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorQualificationDocuments_TutorTeachingChangeRequests_TeachingChangeRequestId",
                        column: x => x.TeachingChangeRequestId,
                        principalTable: "TutorTeachingChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorQualificationDocuments_TeachingChangeRequestId",
                table: "TutorQualificationDocuments",
                column: "TeachingChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorQualificationDocuments_TutorProfileId_Kind",
                table: "TutorQualificationDocuments",
                columns: new[] { "TutorProfileId", "Kind" },
                unique: true,
                filter: "[TeachingChangeRequestId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TutorTeachingChangeRequests_TutorProfileId_AcademicLevel_SubjectCode",
                table: "TutorTeachingChangeRequests",
                columns: new[] { "TutorProfileId", "AcademicLevel", "SubjectCode" },
                unique: true,
                filter: "[Status] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorQualificationDocuments");

            migrationBuilder.DropTable(
                name: "TutorTeachingChangeRequests");

            migrationBuilder.DropColumn(
                name: "FieldOfStudy",
                table: "TutorProfiles");

            migrationBuilder.DropColumn(
                name: "QualificationVerifiedAtUtc",
                table: "TutorProfiles");

            migrationBuilder.DropColumn(
                name: "QualificationVerifiedByUserId",
                table: "TutorProfiles");
        }
    }
}
