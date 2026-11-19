using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddQualificationRequestAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QualificationRequestAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    CurrentDocumentId = table.Column<int>(type: "int", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualificationRequestAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualificationRequestAttachments_TutorQualificationDocuments_CurrentDocumentId",
                        column: x => x.CurrentDocumentId,
                        principalTable: "TutorQualificationDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QualificationRequestAttachments_TutorQualificationUpdateRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "TutorQualificationUpdateRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QualificationRequestAttachments_CurrentDocumentId",
                table: "QualificationRequestAttachments",
                column: "CurrentDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_QualificationRequestAttachments_RequestId",
                table: "QualificationRequestAttachments",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QualificationRequestAttachments");
        }
    }
}
