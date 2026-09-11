using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddBillPaymentAccessToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentAccessToken",
                table: "Bills",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bills_PaymentAccessToken",
                table: "Bills",
                column: "PaymentAccessToken",
                unique: true,
                filter: "[PaymentAccessToken] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bills_PaymentAccessToken",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "PaymentAccessToken",
                table: "Bills");
        }
    }
}
