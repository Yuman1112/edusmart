using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SecureSmartTuitionSystem.Data;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260930084000_AddMessageEditingAndDeletion")]
    public partial class AddMessageEditingAndDeletion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ChatMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "ChatMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeletedForEveryone",
                table: "ChatMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MessageUserDeletions",
                columns: table => new
                {
                    MessageUserDeletionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatMessageId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageUserDeletions", x => x.MessageUserDeletionId);
                    table.ForeignKey(
                        name: "FK_MessageUserDeletions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageUserDeletions_ChatMessages_ChatMessageId",
                        column: x => x.ChatMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "ChatMessageId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_DeletedAt",
                table: "ChatMessages",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MessageUserDeletions_UserId",
                table: "MessageUserDeletions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageUserDeletions_ChatMessageId_UserId",
                table: "MessageUserDeletions",
                columns: new[] { "ChatMessageId", "UserId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MessageUserDeletions");
            migrationBuilder.DropIndex(name: "IX_ChatMessages_DeletedAt", table: "ChatMessages");
            migrationBuilder.DropColumn(name: "DeletedByUserId", table: "ChatMessages");
            migrationBuilder.DropColumn(name: "DeletedAt", table: "ChatMessages");
            migrationBuilder.DropColumn(name: "EditedAt", table: "ChatMessages");
            migrationBuilder.DropColumn(name: "IsDeletedForEveryone", table: "ChatMessages");
        }
    }
}
