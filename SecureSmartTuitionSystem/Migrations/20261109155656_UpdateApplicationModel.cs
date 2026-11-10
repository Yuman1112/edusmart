using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    public partial class UpdateApplicationModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Snapshot reconciliation only. AddVideoPlaybackVault already created
            // both video tables and AddVideoAdminRestore added InactivatedByAdmin.
            // Keep the generated target model and current model snapshot intact.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No schema changes were introduced by this migration.
            // Earlier migrations own the video tables and their rollback.
        }
    }
}
