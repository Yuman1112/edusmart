using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddClassCalendarAndCommunicationUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WithdrawalReason",
                table: "StudentEnrollments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "ClassSessions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumCapacity",
                table: "ClassSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "ClassSessions",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClassLessonChanges",
                columns: table => new
                {
                    ClassLessonChangeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassSessionId = table.Column<int>(type: "int", nullable: false),
                    OriginalDate = table.Column<DateTime>(type: "date", nullable: false),
                    OriginalStartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    OriginalEndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReplacementDate = table.Column<DateTime>(type: "date", nullable: true),
                    ReplacementStartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    ReplacementEndTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    ReplacementVenue = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    StudentIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecipientIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassLessonChanges", x => x.ClassLessonChangeId);
                    table.ForeignKey(
                        name: "FK_ClassLessonChanges_ClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "ClassSessions",
                        principalColumn: "ClassSessionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    UserNotificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AvailableFromDate = table.Column<DateTime>(type: "date", nullable: true),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.UserNotificationId);
                    table.ForeignKey(
                        name: "FK_UserNotifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassLessonChanges_ClassSessionId_OriginalDate",
                table: "ClassLessonChanges",
                columns: new[] { "ClassSessionId", "OriginalDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_CreatedAtUtc",
                table: "UserNotifications",
                columns: new[] { "UserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassLessonChanges");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "WithdrawalReason",
                table: "StudentEnrollments");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "MinimumCapacity",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "ClassSessions");
        }
    }
}
