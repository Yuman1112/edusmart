using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureSmartTuitionSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorQualificationVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Two merged migration histories introduced the same qualification schema.
            // Adopt compatible existing objects without deleting data or editing history.
            // Incompatible existing definitions fail explicitly rather than being hidden.
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[dbo].[TutorProfiles]', N'FieldOfStudy') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[TutorProfiles] ADD [FieldOfStudy] nvarchar(200) NULL;
                END;
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[dbo].[TutorProfiles]', N'QualificationVerifiedAtUtc') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[TutorProfiles] ADD [QualificationVerifiedAtUtc] datetime2 NULL;
                END;
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[dbo].[TutorProfiles]', N'QualificationVerifiedByUserId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[TutorProfiles] ADD [QualificationVerifiedByUserId] nvarchar(450) NULL;
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]', N'U') IS NULL
                BEGIN
                CREATE TABLE [dbo].[TutorTeachingChangeRequests] (
                    [Id] int NOT NULL IDENTITY,
                    [TutorProfileId] int NOT NULL,
                    [Type] int NOT NULL,
                    [Status] int NOT NULL,
                    [AcademicLevel] nvarchar(20) NOT NULL,
                    [SubjectCode] nvarchar(10) NOT NULL,
                    [Reason] nvarchar(1000) NOT NULL,
                    [SubmittedAtUtc] datetime2 NOT NULL,
                    [ReviewNotes] nvarchar(1000) NULL,
                    [ReviewedByUserId] nvarchar(450) NULL,
                    [ReviewedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_TutorTeachingChangeRequests] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_TutorTeachingChangeRequests_TutorProfiles_TutorProfileId] FOREIGN KEY ([TutorProfileId]) REFERENCES [TutorProfiles] ([TutorProfileId]) ON DELETE NO ACTION
                );
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[TutorQualificationDocuments]', N'U') IS NULL
                BEGIN
                CREATE TABLE [dbo].[TutorQualificationDocuments] (
                    [Id] int NOT NULL IDENTITY,
                    [TutorProfileId] int NOT NULL,
                    [TeachingChangeRequestId] int NULL,
                    [Kind] int NOT NULL,
                    [FileName] nvarchar(180) NOT NULL,
                    [Content] varbinary(max) NOT NULL,
                    CONSTRAINT [PK_TutorQualificationDocuments] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_TutorQualificationDocuments_TutorProfiles_TutorProfileId] FOREIGN KEY ([TutorProfileId]) REFERENCES [TutorProfiles] ([TutorProfileId]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_TutorQualificationDocuments_TutorTeachingChangeRequests_TeachingChangeRequestId] FOREIGN KEY ([TeachingChangeRequestId]) REFERENCES [TutorTeachingChangeRequests] ([Id]) ON DELETE NO ACTION
                );
                END;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TutorQualificationDocuments]') AND name = N'IX_TutorQualificationDocuments_TeachingChangeRequestId')
                BEGIN
                    CREATE INDEX [IX_TutorQualificationDocuments_TeachingChangeRequestId] ON [dbo].[TutorQualificationDocuments] ([TeachingChangeRequestId]);
                END;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TutorQualificationDocuments]') AND name = N'IX_TutorQualificationDocuments_TutorProfileId_Kind')
                BEGIN
                    CREATE UNIQUE INDEX [IX_TutorQualificationDocuments_TutorProfileId_Kind] ON [dbo].[TutorQualificationDocuments] ([TutorProfileId], [Kind]) WHERE [TeachingChangeRequestId] IS NULL;
                END;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[TutorTeachingChangeRequests]') AND name = N'IX_TutorTeachingChangeRequests_TutorProfileId_AcademicLevel_SubjectCode')
                BEGIN
                    CREATE UNIQUE INDEX [IX_TutorTeachingChangeRequests_TutorProfileId_AcademicLevel_SubjectCode] ON [dbo].[TutorTeachingChangeRequests] ([TutorProfileId], [AcademicLevel], [SubjectCode]) WHERE [Status] = 0;
                END;
                """);

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
            migrationBuilder.Sql("""
                THROW 51000, 'This reconciliation may adopt pre-existing qualification data. Automatic rollback is blocked; use a verified backup and reviewed rollback plan.', 1;
                """);
        }
    }
}
