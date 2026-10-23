BEGIN TRANSACTION;
DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TutorProfiles]') AND [c].[name] = N'AreaOfExpertise');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TutorProfiles] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [TutorProfiles] ALTER COLUMN [AreaOfExpertise] nvarchar(200) NULL;

CREATE TABLE [TutorTeachingSubjects] (
    [TutorTeachingSubjectId] int NOT NULL IDENTITY,
    [TutorProfileId] int NOT NULL,
    [EducationStage] nvarchar(20) NOT NULL,
    [AcademicLevel] nvarchar(20) NOT NULL,
    [SubjectCode] nvarchar(10) NOT NULL,
    CONSTRAINT [PK_TutorTeachingSubjects] PRIMARY KEY ([TutorTeachingSubjectId]),
    CONSTRAINT [FK_TutorTeachingSubjects_TutorProfiles_TutorProfileId] FOREIGN KEY ([TutorProfileId]) REFERENCES [TutorProfiles] ([TutorProfileId]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_TutorTeachingSubjects_TutorProfileId_EducationStage_AcademicLevel_SubjectCode] ON [TutorTeachingSubjects] ([TutorProfileId], [EducationStage], [AcademicLevel], [SubjectCode]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261018200329_AddTutorTeachingSubjects', N'9.0.0');

COMMIT;
GO

