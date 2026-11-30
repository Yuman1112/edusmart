using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestSupportEnquiries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestEnquiries",
                columns: table => new
                {
                    GuestEnquiryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubmissionKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProtectedContact = table.Column<string>(type: "nvarchar(max)", maxLength: 24000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestEnquiries", x => x.GuestEnquiryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestEnquiries_IsResolved_CreatedAtUtc",
                table: "GuestEnquiries",
                columns: new[] { "IsResolved", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestEnquiries_SubmissionKey",
                table: "GuestEnquiries",
                column: "SubmissionKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestEnquiries");
        }
    }
}
