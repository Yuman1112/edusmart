using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorQualificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Compatibility migration from the other merged branch. The earlier
            // AddTutorQualificationVerification already creates this schema.
            // Do not recreate tables, restore the obsolete unique index, or drop data.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorProfiles]') AND name = N'FieldOfStudy' AND system_type_id = TYPE_ID(N'nvarchar') AND max_length = 400 AND is_nullable = 1)
                    THROW 51000, 'Incompatible existing TutorProfiles.FieldOfStudy; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorProfiles]') AND name = N'QualificationVerifiedAtUtc' AND system_type_id = TYPE_ID(N'datetime2') AND max_length = 8 AND is_nullable = 1 AND scale = 7)
                    THROW 51000, 'Incompatible existing TutorProfiles.QualificationVerifiedAtUtc; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorProfiles]') AND name = N'QualificationVerifiedByUserId' AND system_type_id = TYPE_ID(N'nvarchar') AND max_length = 900 AND is_nullable = 1)
                    THROW 51000, 'Incompatible existing TutorProfiles.QualificationVerifiedByUserId; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND name = N'Id' AND system_type_id = TYPE_ID(N'int') AND max_length = 4 AND is_nullable = 0 AND is_identity = 1)
                    THROW 51000, 'Incompatible existing TutorTeachingChangeRequests.Id; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND name = N'TutorProfileId' AND system_type_id = TYPE_ID(N'int') AND max_length = 4 AND is_nullable = 0)
                    THROW 51000, 'Incompatible existing TutorTeachingChangeRequests.TutorProfileId; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND name = N'Type' AND system_type_id = TYPE_ID(N'int') AND max_length = 4 AND is_nullable = 0)
                    THROW 51000, 'Incompatible existing TutorTeachingChangeRequests.Type; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND name = N'Status' AND system_type_id = TYPE_ID(N'int') AND max_length = 4 AND is_nullable = 0)
                    THROW 51000, 'Incompatible existing TutorTeachingChangeRequests.Status; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND name = N'AcademicLevel' AND system_type_id = TYPE_ID(N'nvarchar') AND max_length = 40 AND is_nullable = 0)
                    THROW 51000, 'Incompatible existing TutorTeachingChangeRequests.AcademicLevel; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND name = N'SubjectCode' AND system_type_id = TYPE_ID(N'nvarchar') AND max_length = 20 AND is_nullable = 0)
                    THROW 51000, 'Incompatible existing TutorTeachingChangeRequests.SubjectCode; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND name = N'Reason' AND system_type_id = TYPE_ID(N'nvarchar') AND max_length = 2000 AND is_nullable = 0)
                    THROW 51000, 'Incompatible existing TutorTeachingChangeRequests.Reason; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND name = N'SubmittedAtUtc' AND system_type_id = TYPE_ID(N'datetime2') AND max_length = 8 AND is_nullable = 0)
                    THROW 51000, 'Incompatible existing TutorTeachingChangeRequests.SubmittedAtUtc; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND name = N'ReviewNotes' AND system_type_id = TYPE_ID(N'nvarchar') AND max_length = 2000 AND is_nullable = 1)
                    THROW 51000, 'Incompatible existing TutorTeachingChangeRequests.ReviewNotes; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND name = N'ReviewedByUserId' AND system_type_id = TYPE_ID(N'nvarchar') AND max_length = 900 AND is_nullable = 1)
                    THROW 51000, 'Incompatible existing TutorTeachingChangeRequests.ReviewedByUserId; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND name = N'ReviewedAtUtc' AND system_type_id = TYPE_ID(N'datetime2') AND max_length = 8 AND is_nullable = 1)
                    THROW 51000, 'Incompatible existing TutorTeachingChangeRequests.ReviewedAtUtc; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND i.is_primary_key=1 AND ic.key_ordinal=1 AND c.name=N'Id' AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal>0)=1)
                    THROW 51000, 'Incompatible existing TutorTeachingChangeRequests primary key; inspect the schema.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorQualificationDocuments]') AND name = N'Id' AND system_type_id = TYPE_ID(N'int') AND max_length = 4 AND is_nullable = 0 AND is_identity = 1)
                    THROW 51000, 'Incompatible existing TutorQualificationDocuments.Id; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorQualificationDocuments]') AND name = N'TutorProfileId' AND system_type_id = TYPE_ID(N'int') AND max_length = 4 AND is_nullable = 0)
                    THROW 51000, 'Incompatible existing TutorQualificationDocuments.TutorProfileId; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorQualificationDocuments]') AND name = N'TeachingChangeRequestId' AND system_type_id = TYPE_ID(N'int') AND max_length = 4 AND is_nullable = 1)
                    THROW 51000, 'Incompatible existing TutorQualificationDocuments.TeachingChangeRequestId; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorQualificationDocuments]') AND name = N'Kind' AND system_type_id = TYPE_ID(N'int') AND max_length = 4 AND is_nullable = 0)
                    THROW 51000, 'Incompatible existing TutorQualificationDocuments.Kind; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorQualificationDocuments]') AND name = N'FileName' AND system_type_id = TYPE_ID(N'nvarchar') AND max_length = 360 AND is_nullable = 0)
                    THROW 51000, 'Incompatible existing TutorQualificationDocuments.FileName; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TutorQualificationDocuments]') AND name = N'Content' AND system_type_id = TYPE_ID(N'varbinary') AND max_length = -1 AND is_nullable = 0)
                    THROW 51000, 'Incompatible existing TutorQualificationDocuments.Content; inspect the schema before updating.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'[dbo].[TutorQualificationDocuments]') AND i.is_primary_key=1 AND ic.key_ordinal=1 AND c.name=N'Id' AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal>0)=1)
                    THROW 51000, 'Incompatible existing TutorQualificationDocuments primary key; inspect the schema.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f JOIN sys.foreign_key_columns k ON k.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND f.referenced_object_id=OBJECT_ID(N'[dbo].[TutorProfiles]') AND COL_NAME(k.parent_object_id,k.parent_column_id)=N'TutorProfileId' AND COL_NAME(k.referenced_object_id,k.referenced_column_id)=N'TutorProfileId' AND f.delete_referential_action=0 AND f.is_disabled=0 AND f.is_not_trusted=0)
                    THROW 51000, 'Incompatible existing TutorTeachingChangeRequests.TutorProfileId foreign key; inspect the schema.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f JOIN sys.foreign_key_columns k ON k.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID(N'[dbo].[TutorQualificationDocuments]') AND f.referenced_object_id=OBJECT_ID(N'[dbo].[TutorProfiles]') AND COL_NAME(k.parent_object_id,k.parent_column_id)=N'TutorProfileId' AND COL_NAME(k.referenced_object_id,k.referenced_column_id)=N'TutorProfileId' AND f.delete_referential_action=0 AND f.is_disabled=0 AND f.is_not_trusted=0)
                    THROW 51000, 'Incompatible existing TutorQualificationDocuments.TutorProfileId foreign key; inspect the schema.', 1;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f JOIN sys.foreign_key_columns k ON k.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID(N'[dbo].[TutorQualificationDocuments]') AND f.referenced_object_id=OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND COL_NAME(k.parent_object_id,k.parent_column_id)=N'TeachingChangeRequestId' AND COL_NAME(k.referenced_object_id,k.referenced_column_id)=N'Id' AND f.delete_referential_action=0 AND f.is_disabled=0 AND f.is_not_trusted=0)
                    THROW 51000, 'Incompatible existing TutorQualificationDocuments.TeachingChangeRequestId foreign key; inspect the schema.', 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This compatibility migration creates no objects; rolling it back
            // must not drop objects owned by earlier migrations or existing data.
        }
    }
}
