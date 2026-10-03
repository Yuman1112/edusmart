using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class SyncClassCalendarRefundModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimumCapacity",
                table: "ClassSessions");

            migrationBuilder.CreateTable(
                name: "EnrollmentExitRequests",
                columns: table => new
                {
                    EnrollmentExitRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentEnrollmentId = table.Column<int>(type: "int", nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "date", nullable: false),
                    EstimatedRefund = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ApprovedRefund = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    RefundStatus = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefundStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefundCompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProviderRefundId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentExitRequests", x => x.EnrollmentExitRequestId);
                    table.ForeignKey(
                        name: "FK_EnrollmentExitRequests_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "PaymentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentExitRequests_StudentEnrollments_StudentEnrollmentId",
                        column: x => x.StudentEnrollmentId,
                        principalTable: "StudentEnrollments",
                        principalColumn: "StudentEnrollmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentExitRequests_PaymentId",
                table: "EnrollmentExitRequests",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentExitRequests_ProviderRefundId",
                table: "EnrollmentExitRequests",
                column: "ProviderRefundId",
                unique: true,
                filter: "[ProviderRefundId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentExitRequests_StudentEnrollmentId",
                table: "EnrollmentExitRequests",
                column: "StudentEnrollmentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnrollmentExitRequests");

            migrationBuilder.AddColumn<int>(
                name: "MinimumCapacity",
                table: "ClassSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
