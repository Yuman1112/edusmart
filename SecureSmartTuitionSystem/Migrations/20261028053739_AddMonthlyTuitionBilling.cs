using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyTuitionBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonthlyInvoices",
                columns: table => new
                {
                    MonthlyInvoiceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<DateTime>(type: "date", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Total = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CheckoutKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckoutSessionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PaymentIntentId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyInvoices", x => x.MonthlyInvoiceId);
                    table.ForeignKey(
                        name: "FK_MonthlyInvoices_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "StudentProfileId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillingReminders",
                columns: table => new
                {
                    BillingReminderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MonthlyInvoiceId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Day = table.Column<DateTime>(type: "date", nullable: false),
                    EmailAttemptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmailSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmailError = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingReminders", x => x.BillingReminderId);
                    table.ForeignKey(
                        name: "FK_BillingReminders_MonthlyInvoices_MonthlyInvoiceId",
                        column: x => x.MonthlyInvoiceId,
                        principalTable: "MonthlyInvoices",
                        principalColumn: "MonthlyInvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyInvoiceLines",
                columns: table => new
                {
                    MonthlyInvoiceLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MonthlyInvoiceId = table.Column<int>(type: "int", nullable: false),
                    StudentEnrollmentId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyInvoiceLines", x => x.MonthlyInvoiceLineId);
                    table.ForeignKey(
                        name: "FK_MonthlyInvoiceLines_MonthlyInvoices_MonthlyInvoiceId",
                        column: x => x.MonthlyInvoiceId,
                        principalTable: "MonthlyInvoices",
                        principalColumn: "MonthlyInvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingReminders_MonthlyInvoiceId_UserId_Kind_Day",
                table: "BillingReminders",
                columns: new[] { "MonthlyInvoiceId", "UserId", "Kind", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyInvoiceLines_MonthlyInvoiceId_StudentEnrollmentId",
                table: "MonthlyInvoiceLines",
                columns: new[] { "MonthlyInvoiceId", "StudentEnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyInvoices_CheckoutSessionId",
                table: "MonthlyInvoices",
                column: "CheckoutSessionId",
                unique: true,
                filter: "[CheckoutSessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyInvoices_PaymentIntentId",
                table: "MonthlyInvoices",
                column: "PaymentIntentId",
                unique: true,
                filter: "[PaymentIntentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyInvoices_StudentProfileId_Month",
                table: "MonthlyInvoices",
                columns: new[] { "StudentProfileId", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingReminders");

            migrationBuilder.DropTable(
                name: "MonthlyInvoiceLines");

            migrationBuilder.DropTable(
                name: "MonthlyInvoices");
        }
    }
}
