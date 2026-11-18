using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class UnifyTutorEmailRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "EmailChangeRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RequestedByUserId",
                table: "EmailChangeRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.Sql("""

-- Refuse ambiguous legacy state instead of silently discarding a pending request.
IF EXISTS (SELECT 1 FROM dbo.AspNetUsers WHERE
 (TutorEmailChangeRequestId IS NULL AND (PendingTutorEmail IS NOT NULL OR TutorEmailChangeRequestedAtUtc IS NOT NULL OR TutorEmailChangeExpiresAtUtc IS NOT NULL OR TutorEmailChangeRequestedByUserId IS NOT NULL)) OR
 (TutorEmailChangeRequestId IS NOT NULL AND (NULLIF(LTRIM(RTRIM(PendingTutorEmail)),N'') IS NULL OR TutorEmailChangeRequestedAtUtc IS NULL OR TutorEmailChangeExpiresAtUtc IS NULL OR TutorEmailChangeExpiresAtUtc <= TutorEmailChangeRequestedAtUtc)))
 THROW 51110, 'Incomplete legacy tutor email request. Review the pending request before migrating.', 1;
IF EXISTS (SELECT TutorEmailChangeRequestId FROM dbo.AspNetUsers WHERE TutorEmailChangeRequestId IS NOT NULL GROUP BY TutorEmailChangeRequestId HAVING COUNT(*) > 1)
 OR EXISTS (SELECT 1 FROM dbo.AspNetUsers u JOIN dbo.EmailChangeRequests r ON r.Id=u.TutorEmailChangeRequestId)
 OR EXISTS (SELECT 1 FROM dbo.AspNetUsers u JOIN dbo.EmailChangeRequests r ON r.UserId=u.Id AND r.Status IN (0,1) WHERE u.TutorEmailChangeRequestId IS NOT NULL)
 THROW 51111, 'Conflicting email requests found. Review before migrating.', 1;
IF EXISTS (SELECT 1 FROM dbo.AspNetUsers WHERE TutorEmailChangeRequestId IS NOT NULL AND COALESCE(SecurityStamp,N'') COLLATE Latin1_General_100_BIN2 LIKE N'%[^ -~]%')
 THROW 51112, 'A legacy security stamp needs manual review before migration.', 1;
UPDATE dbo.EmailChangeRequests SET RequestedByUserId=UserId WHERE Purpose=0 AND RequestedByUserId IS NULL;
INSERT dbo.EmailChangeRequests
 (Id,UserId,NewEmail,OriginalEmail,SecurityStampHash,RecipientHash,CodeHash,CodeNonce,LinkNonce,RequestedAtUtc,ExpiresAtUtc,CodeExpiresAtUtc,Attempts,Status,CompletedAtUtc,Purpose,RequestedByUserId)
SELECT TutorEmailChangeRequestId,Id,PendingTutorEmail,Email,
 CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varchar(max),COALESCE(SecurityStamp,N''))),2),
 N'',N'',NEWID(),NEWID(),TutorEmailChangeRequestedAtUtc,TutorEmailChangeExpiresAtUtc,TutorEmailChangeRequestedAtUtc,0,1,NULL,1,TutorEmailChangeRequestedByUserId
FROM dbo.AspNetUsers WHERE TutorEmailChangeRequestId IS NOT NULL;
""");

            migrationBuilder.DropColumn(
                name: "PendingTutorEmail",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TutorEmailChangeExpiresAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TutorEmailChangeRequestId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TutorEmailChangeRequestedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TutorEmailChangeRequestedByUserId",
                table: "AspNetUsers");


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not silently relabel managed requests as self-service on downgrade.
            migrationBuilder.Sql("THROW 51113, 'This data migration requires a coordinated backup restore to roll back. Do not downgrade the schema alone.', 1;");
        }
    }
}
