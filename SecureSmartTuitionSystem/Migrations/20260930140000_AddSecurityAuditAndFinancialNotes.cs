using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace SecureSmartTuitionSystem.Migrations;
public partial class AddSecurityAuditAndFinancialNotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(name: "FailureReason", table: "Payments",
            type: "nvarchar(max)", nullable: true, oldClrType: typeof(string), oldType: "nvarchar(500)", oldMaxLength: 500, oldNullable: true);
        migrationBuilder.AlterColumn<string>(name: "CancellationReason", table: "Bills",
            type: "nvarchar(max)", nullable: true, oldClrType: typeof(string), oldType: "nvarchar(500)", oldMaxLength: 500, oldNullable: true);
        migrationBuilder.CreateTable(name: "AuditLogEntries", columns: table => new {
            AuditLogEntryId = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
            ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
            ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
            EventName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
            Severity = table.Column<int>(type: "int", nullable: false),
            EntityName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
            EntityKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
            RemoteIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
            TraceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
            Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
            IntegrityProof = table.Column<string>(type: "nvarchar(max)", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_AuditLogEntries", x => x.AuditLogEntryId));
        migrationBuilder.CreateIndex("IX_AuditLogEntries_OccurredAtUtc", "AuditLogEntries", "OccurredAtUtc");
        migrationBuilder.CreateIndex("IX_AuditLogEntries_Severity_OccurredAtUtc", "AuditLogEntries", new[] { "Severity", "OccurredAtUtc" });
        migrationBuilder.CreateIndex("IX_AuditLogEntries_ActorUserId_OccurredAtUtc", "AuditLogEntries", new[] { "ActorUserId", "OccurredAtUtc" });
        migrationBuilder.CreateIndex("IX_AuditLogEntries_EventName_RemoteIp_OccurredAtUtc", "AuditLogEntries", new[] { "EventName", "RemoteIp", "OccurredAtUtc" });
        migrationBuilder.CreateTable(name: "SecurityEmailDeliveries", columns: table => new {
            AuditLogEntryId = table.Column<long>(type: "bigint", nullable: false),
            SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
        }, constraints: table => {
            table.PrimaryKey("PK_SecurityEmailDeliveries", x => x.AuditLogEntryId);
            table.ForeignKey("FK_SecurityEmailDeliveries_AuditLogEntries_AuditLogEntryId", x => x.AuditLogEntryId,
                "AuditLogEntries", "AuditLogEntryId", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_AuditLogEntries_AppendOnly]
            ON [dbo].[AuditLogEntries] AFTER UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51001, 'Audit log entries are append-only.', 1;
            END
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [Payments] WHERE [FailureReason] LIKE 'edusmart:v1:%')
                OR EXISTS (SELECT 1 FROM [Bills] WHERE [CancellationReason] LIKE 'edusmart:v1:%')
                THROW 51002, 'Decrypt or restore encrypted financial notes before rolling back this migration.', 1;
            """);
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_AuditLogEntries_AppendOnly];");
        migrationBuilder.DropTable("SecurityEmailDeliveries");
        migrationBuilder.DropTable("AuditLogEntries");
        migrationBuilder.AlterColumn<string>("FailureReason", "Payments", "nvarchar(500)", maxLength: 500, nullable: true,
            oldClrType: typeof(string), oldType: "nvarchar(max)", oldNullable: true);
        migrationBuilder.AlterColumn<string>("CancellationReason", "Bills", "nvarchar(500)", maxLength: 500, nullable: true,
            oldClrType: typeof(string), oldType: "nvarchar(max)", oldNullable: true);
    }
}
