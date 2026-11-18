using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminPermissionLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PermissionLevel",
                table: "AdminProfiles",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "Standard");

            // Existing deployments keep their roles, credentials and account status.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.AspNetUserRoles ur
                    JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId WHERE r.Name = N'Admin')
                    AND NOT EXISTS (SELECT 1 FROM dbo.AspNetUsers u
                        JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
                        JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId
                        WHERE u.UserName = N'AD00001' AND r.Name = N'Admin'
                            AND u.IsActive = 1 AND u.MustChangePassword = 0)
                    THROW 51010, 'AD00001 must be an active, setup-complete Admin before applying this migration. Review the selected database.', 1;

                INSERT INTO dbo.AdminProfiles (ApplicationUserId, FullName, Position, CreatedAt, PermissionLevel)
                SELECT u.Id, LEFT(COALESCE(u.FullName, N'EduSmart Administrator'), 100),
                    N'Administrator', SYSUTCDATETIME(), N'High'
                FROM dbo.AspNetUsers u
                JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
                JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId
                WHERE u.UserName = N'AD00001' AND r.Name = N'Admin'
                    AND NOT EXISTS (SELECT 1 FROM dbo.AdminProfiles p WHERE p.ApplicationUserId = u.Id);

                UPDATE p SET PermissionLevel = N'High'
                FROM dbo.AdminProfiles p
                JOIN dbo.AspNetUsers u ON u.Id = p.ApplicationUserId
                JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
                JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId
                WHERE u.UserName = N'AD00001' AND r.Name = N'Admin';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AdminProfiles_PermissionLevel",
                table: "AdminProfiles",
                column: "PermissionLevel",
                unique: true,
                filter: "[PermissionLevel] = N'High'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AdminProfiles_PermissionLevel",
                table: "AdminProfiles",
                sql: "[PermissionLevel] IN (N'Standard', N'High')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AdminProfiles_PermissionLevel",
                table: "AdminProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AdminProfiles_PermissionLevel",
                table: "AdminProfiles");

            migrationBuilder.DropColumn(
                name: "PermissionLevel",
                table: "AdminProfiles");
        }
    }
}
