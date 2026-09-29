using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SecureSmartTuitionSystem.Data;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260929153000_HardenPaymentAccessTokens")]
    public partial class HardenPaymentAccessTokens : Migration
    {
        protected override void Up(
            MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bills_PaymentAccessToken",
                table: "Bills");

            migrationBuilder.RenameColumn(
                name: "PaymentAccessToken",
                table: "Bills",
                newName: "PaymentAccessTokenHash");

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentAccessTokenExpiresAt",
                table: "Bills",
                type: "datetime2",
                nullable: true);

            // Previously stored values were raw bearer tokens and must
            // not be treated as hashes. New links are generated safely.
            migrationBuilder.Sql(
                "UPDATE [Bills] " +
                "SET [PaymentAccessTokenHash] = NULL, " +
                "[PaymentAccessTokenExpiresAt] = NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_PaymentAccessTokenHash",
                table: "Bills",
                column: "PaymentAccessTokenHash",
                unique: true,
                filter: "[PaymentAccessTokenHash] IS NOT NULL");
        }

        protected override void Down(
            MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bills_PaymentAccessTokenHash",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "PaymentAccessTokenExpiresAt",
                table: "Bills");

            migrationBuilder.RenameColumn(
                name: "PaymentAccessTokenHash",
                table: "Bills",
                newName: "PaymentAccessToken");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_PaymentAccessToken",
                table: "Bills",
                column: "PaymentAccessToken",
                unique: true,
                filter: "[PaymentAccessToken] IS NOT NULL");
        }
    }
}
