using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class EnableBatchTeachingSubjectRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TutorQualificationDocuments_TeachingChangeRequestId",
                table: "TutorQualificationDocuments");

            migrationBuilder.AddColumn<int>(
                name: "EvidenceRequestId",
                table: "TutorTeachingChangeRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorTeachingChangeRequests_EvidenceRequestId",
                table: "TutorTeachingChangeRequests",
                column: "EvidenceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorQualificationDocuments_TeachingChangeRequestId",
                table: "TutorQualificationDocuments",
                column: "TeachingChangeRequestId",
                filter: "[TeachingChangeRequestId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_TutorTeachingChangeRequests_TutorTeachingChangeRequests_EvidenceRequestId",
                table: "TutorTeachingChangeRequests",
                column: "EvidenceRequestId",
                principalTable: "TutorTeachingChangeRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [TutorTeachingChangeRequests] WHERE [EvidenceRequestId] IS NOT NULL)
                    THROW 50001, 'Shared subject submissions exist. Restore a backup to revert this migration without losing their evidence links.', 1;
                IF EXISTS (
                    SELECT [TeachingChangeRequestId] FROM [TutorQualificationDocuments]
                    WHERE [TeachingChangeRequestId] IS NOT NULL
                    GROUP BY [TeachingChangeRequestId] HAVING COUNT(*) > 1)
                    THROW 50002, 'Multiple evidence files exist. Restore a backup to revert this migration without losing evidence.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_TutorTeachingChangeRequests_TutorTeachingChangeRequests_EvidenceRequestId",
                table: "TutorTeachingChangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_TutorTeachingChangeRequests_EvidenceRequestId",
                table: "TutorTeachingChangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_TutorQualificationDocuments_TeachingChangeRequestId",
                table: "TutorQualificationDocuments");

            migrationBuilder.DropColumn(
                name: "EvidenceRequestId",
                table: "TutorTeachingChangeRequests");

            migrationBuilder.CreateIndex(
                name: "IX_TutorQualificationDocuments_TeachingChangeRequestId",
                table: "TutorQualificationDocuments",
                column: "TeachingChangeRequestId",
                unique: true,
                filter: "[TeachingChangeRequestId] IS NOT NULL");
        }
    }
}
