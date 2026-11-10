IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FullName] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(128) NOT NULL,
        [ProviderKey] nvarchar(128) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(128) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907073639_InitialIdentityDatabase'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260907073639_InitialIdentityDatabase', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081520_AddParentAndStudentProfiles'
)
BEGIN
    CREATE TABLE [ParentProfiles] (
        [ParentProfileId] int NOT NULL IDENTITY,
        [ApplicationUserId] nvarchar(450) NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [IdentificationLast4] nvarchar(4) NULL,
        [Address] nvarchar(250) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ParentProfiles] PRIMARY KEY ([ParentProfileId]),
        CONSTRAINT [FK_ParentProfiles_AspNetUsers_ApplicationUserId] FOREIGN KEY ([ApplicationUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081520_AddParentAndStudentProfiles'
)
BEGIN
    CREATE TABLE [StudentProfiles] (
        [StudentProfileId] int NOT NULL IDENTITY,
        [StudentNumber] nvarchar(20) NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [DateOfBirth] datetime2 NOT NULL,
        [AcademicLevel] nvarchar(30) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ParentProfileId] int NOT NULL,
        [ApplicationUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_StudentProfiles] PRIMARY KEY ([StudentProfileId]),
        CONSTRAINT [FK_StudentProfiles_AspNetUsers_ApplicationUserId] FOREIGN KEY ([ApplicationUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StudentProfiles_ParentProfiles_ParentProfileId] FOREIGN KEY ([ParentProfileId]) REFERENCES [ParentProfiles] ([ParentProfileId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081520_AddParentAndStudentProfiles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ParentProfiles_ApplicationUserId] ON [ParentProfiles] ([ApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081520_AddParentAndStudentProfiles'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StudentProfiles_ApplicationUserId] ON [StudentProfiles] ([ApplicationUserId]) WHERE [ApplicationUserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081520_AddParentAndStudentProfiles'
)
BEGIN
    CREATE INDEX [IX_StudentProfiles_ParentProfileId] ON [StudentProfiles] ([ParentProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081520_AddParentAndStudentProfiles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StudentProfiles_StudentNumber] ON [StudentProfiles] ([StudentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081520_AddParentAndStudentProfiles'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260907081520_AddParentAndStudentProfiles', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081904_AddSubjectAndClassSession'
)
BEGIN
    CREATE TABLE [Subjects] (
        [SubjectId] int NOT NULL IDENTITY,
        [SubjectName] nvarchar(100) NOT NULL,
        [AcademicLevel] nvarchar(30) NOT NULL,
        [MonthlyFee] decimal(10,2) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Subjects] PRIMARY KEY ([SubjectId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081904_AddSubjectAndClassSession'
)
BEGIN
    CREATE TABLE [ClassSessions] (
        [ClassSessionId] int NOT NULL IDENTITY,
        [ClassCode] nvarchar(30) NOT NULL,
        [SubjectId] int NOT NULL,
        [TutorUserId] nvarchar(450) NOT NULL,
        [DayOfWeek] int NOT NULL,
        [StartTime] time NOT NULL,
        [EndTime] time NOT NULL,
        [Venue] nvarchar(100) NOT NULL,
        [Capacity] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_ClassSessions] PRIMARY KEY ([ClassSessionId]),
        CONSTRAINT [FK_ClassSessions_AspNetUsers_TutorUserId] FOREIGN KEY ([TutorUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClassSessions_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([SubjectId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081904_AddSubjectAndClassSession'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClassSessions_ClassCode] ON [ClassSessions] ([ClassCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081904_AddSubjectAndClassSession'
)
BEGIN
    CREATE INDEX [IX_ClassSessions_SubjectId] ON [ClassSessions] ([SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081904_AddSubjectAndClassSession'
)
BEGIN
    CREATE INDEX [IX_ClassSessions_TutorUserId] ON [ClassSessions] ([TutorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081904_AddSubjectAndClassSession'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Subjects_SubjectName_AcademicLevel] ON [Subjects] ([SubjectName], [AcademicLevel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907081904_AddSubjectAndClassSession'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260907081904_AddSubjectAndClassSession', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082356_AddEnrollmentApplications'
)
BEGIN
    CREATE TABLE [EnrollmentApplications] (
        [EnrollmentApplicationId] int NOT NULL IDENTITY,
        [ApplicationNumber] nvarchar(30) NOT NULL,
        [SubmittedByUserId] nvarchar(450) NULL,
        [ExistingParentProfileId] int NULL,
        [ExistingStudentProfileId] int NULL,
        [GuardianName] nvarchar(100) NOT NULL,
        [GuardianEmail] nvarchar(256) NOT NULL,
        [GuardianPhoneNumber] nvarchar(30) NOT NULL,
        [GuardianIdentificationLast4] nvarchar(4) NULL,
        [StudentName] nvarchar(100) NOT NULL,
        [StudentDateOfBirth] datetime2 NOT NULL,
        [AcademicLevel] nvarchar(30) NOT NULL,
        [RequestedStartDate] datetime2 NOT NULL,
        [HardcopyMaterialsRequested] bit NOT NULL,
        [FinalTwoWeeksConfirmed] bit NOT NULL,
        [PotentialDuplicateStudent] bit NOT NULL,
        [PotentialDuplicateGuardian] bit NOT NULL,
        [Status] int NOT NULL,
        [ApplicantNotes] nvarchar(500) NULL,
        [RejectionReason] nvarchar(500) NULL,
        [SubmittedAt] datetime2 NOT NULL,
        [ReviewedAt] datetime2 NULL,
        [ReviewedByUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_EnrollmentApplications] PRIMARY KEY ([EnrollmentApplicationId]),
        CONSTRAINT [FK_EnrollmentApplications_AspNetUsers_ReviewedByUserId] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EnrollmentApplications_AspNetUsers_SubmittedByUserId] FOREIGN KEY ([SubmittedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EnrollmentApplications_ParentProfiles_ExistingParentProfileId] FOREIGN KEY ([ExistingParentProfileId]) REFERENCES [ParentProfiles] ([ParentProfileId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EnrollmentApplications_StudentProfiles_ExistingStudentProfileId] FOREIGN KEY ([ExistingStudentProfileId]) REFERENCES [StudentProfiles] ([StudentProfileId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082356_AddEnrollmentApplications'
)
BEGIN
    CREATE TABLE [EnrollmentApplicationSubjects] (
        [EnrollmentApplicationSubjectId] int NOT NULL IDENTITY,
        [EnrollmentApplicationId] int NOT NULL,
        [ClassSessionId] int NOT NULL,
        [MonthlyFeeAtApplication] decimal(10,2) NOT NULL,
        CONSTRAINT [PK_EnrollmentApplicationSubjects] PRIMARY KEY ([EnrollmentApplicationSubjectId]),
        CONSTRAINT [FK_EnrollmentApplicationSubjects_ClassSessions_ClassSessionId] FOREIGN KEY ([ClassSessionId]) REFERENCES [ClassSessions] ([ClassSessionId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EnrollmentApplicationSubjects_EnrollmentApplications_EnrollmentApplicationId] FOREIGN KEY ([EnrollmentApplicationId]) REFERENCES [EnrollmentApplications] ([EnrollmentApplicationId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082356_AddEnrollmentApplications'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EnrollmentApplications_ApplicationNumber] ON [EnrollmentApplications] ([ApplicationNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082356_AddEnrollmentApplications'
)
BEGIN
    CREATE INDEX [IX_EnrollmentApplications_ExistingParentProfileId] ON [EnrollmentApplications] ([ExistingParentProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082356_AddEnrollmentApplications'
)
BEGIN
    CREATE INDEX [IX_EnrollmentApplications_ExistingStudentProfileId] ON [EnrollmentApplications] ([ExistingStudentProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082356_AddEnrollmentApplications'
)
BEGIN
    CREATE INDEX [IX_EnrollmentApplications_ReviewedByUserId] ON [EnrollmentApplications] ([ReviewedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082356_AddEnrollmentApplications'
)
BEGIN
    CREATE INDEX [IX_EnrollmentApplications_SubmittedByUserId] ON [EnrollmentApplications] ([SubmittedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082356_AddEnrollmentApplications'
)
BEGIN
    CREATE INDEX [IX_EnrollmentApplicationSubjects_ClassSessionId] ON [EnrollmentApplicationSubjects] ([ClassSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082356_AddEnrollmentApplications'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EnrollmentApplicationSubjects_EnrollmentApplicationId_ClassSessionId] ON [EnrollmentApplicationSubjects] ([EnrollmentApplicationId], [ClassSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082356_AddEnrollmentApplications'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260907082356_AddEnrollmentApplications', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082802_AddStudentEnrollmentAndHistory'
)
BEGIN
    CREATE TABLE [EnrollmentStatusHistories] (
        [EnrollmentStatusHistoryId] int NOT NULL IDENTITY,
        [EnrollmentApplicationId] int NOT NULL,
        [PreviousStatus] int NULL,
        [NewStatus] int NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        [ChangedByUserId] nvarchar(450) NULL,
        [Remarks] nvarchar(500) NULL,
        CONSTRAINT [PK_EnrollmentStatusHistories] PRIMARY KEY ([EnrollmentStatusHistoryId]),
        CONSTRAINT [FK_EnrollmentStatusHistories_AspNetUsers_ChangedByUserId] FOREIGN KEY ([ChangedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EnrollmentStatusHistories_EnrollmentApplications_EnrollmentApplicationId] FOREIGN KEY ([EnrollmentApplicationId]) REFERENCES [EnrollmentApplications] ([EnrollmentApplicationId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082802_AddStudentEnrollmentAndHistory'
)
BEGIN
    CREATE TABLE [StudentEnrollments] (
        [StudentEnrollmentId] int NOT NULL IDENTITY,
        [StudentProfileId] int NOT NULL,
        [ClassSessionId] int NOT NULL,
        [EnrollmentApplicationId] int NULL,
        [StartDate] datetime2 NOT NULL,
        [EnrolledAt] datetime2 NOT NULL,
        [DropRequestedAt] datetime2 NULL,
        [DropEffectiveDate] datetime2 NULL,
        [EndedAt] datetime2 NULL,
        [HardcopyMaterialsRequested] bit NOT NULL,
        [MonthlyFeeAtEnrollment] decimal(10,2) NOT NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_StudentEnrollments] PRIMARY KEY ([StudentEnrollmentId]),
        CONSTRAINT [FK_StudentEnrollments_ClassSessions_ClassSessionId] FOREIGN KEY ([ClassSessionId]) REFERENCES [ClassSessions] ([ClassSessionId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StudentEnrollments_EnrollmentApplications_EnrollmentApplicationId] FOREIGN KEY ([EnrollmentApplicationId]) REFERENCES [EnrollmentApplications] ([EnrollmentApplicationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StudentEnrollments_StudentProfiles_StudentProfileId] FOREIGN KEY ([StudentProfileId]) REFERENCES [StudentProfiles] ([StudentProfileId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082802_AddStudentEnrollmentAndHistory'
)
BEGIN
    CREATE INDEX [IX_EnrollmentStatusHistories_ChangedByUserId] ON [EnrollmentStatusHistories] ([ChangedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082802_AddStudentEnrollmentAndHistory'
)
BEGIN
    CREATE INDEX [IX_EnrollmentStatusHistories_EnrollmentApplicationId_ChangedAt] ON [EnrollmentStatusHistories] ([EnrollmentApplicationId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082802_AddStudentEnrollmentAndHistory'
)
BEGIN
    CREATE INDEX [IX_StudentEnrollments_ClassSessionId] ON [StudentEnrollments] ([ClassSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082802_AddStudentEnrollmentAndHistory'
)
BEGIN
    CREATE INDEX [IX_StudentEnrollments_EnrollmentApplicationId] ON [StudentEnrollments] ([EnrollmentApplicationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082802_AddStudentEnrollmentAndHistory'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StudentEnrollments_StudentProfileId_ClassSessionId] ON [StudentEnrollments] ([StudentProfileId], [ClassSessionId]) WHERE [Status] IN (0, 1)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260907082802_AddStudentEnrollmentAndHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260907082802_AddStudentEnrollmentAndHistory', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908083228_AddMustChangePasswordToApplicationUser'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [MustChangePassword] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908083228_AddMustChangePasswordToApplicationUser'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260908083228_AddMustChangePasswordToApplicationUser', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909122041_UpdateStudentAndParentIdentification'
)
BEGIN
    ALTER TABLE [StudentProfiles] ADD [Gender] nvarchar(10) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909122041_UpdateStudentAndParentIdentification'
)
BEGIN
    ALTER TABLE [StudentProfiles] ADD [IdentificationNumber] nvarchar(30) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909122041_UpdateStudentAndParentIdentification'
)
BEGIN
    ALTER TABLE [StudentProfiles] ADD [IdentificationType] nvarchar(30) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909122041_UpdateStudentAndParentIdentification'
)
BEGIN
    ALTER TABLE [StudentProfiles] ADD [School] nvarchar(150) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909122041_UpdateStudentAndParentIdentification'
)
BEGIN
    ALTER TABLE [ParentProfiles] ADD [IdentificationNumber] nvarchar(14) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909122041_UpdateStudentAndParentIdentification'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260909122041_UpdateStudentAndParentIdentification', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909142949_AddEducationStageToSubject'
)
BEGIN
    ALTER TABLE [Subjects] ADD [EducationStage] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909142949_AddEducationStageToSubject'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260909142949_AddEducationStageToSubject', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909150337_AddEducationStageAndClassName'
)
BEGIN
    ALTER TABLE [ClassSessions] ADD [ClassName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909150337_AddEducationStageAndClassName'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260909150337_AddEducationStageAndClassName', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910031225_CompleteClassSessionManagement'
)
BEGIN
    DROP INDEX [IX_Subjects_SubjectName_AcademicLevel] ON [Subjects];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910031225_CompleteClassSessionManagement'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ClassSessions]') AND [c].[name] = N'Venue');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [ClassSessions] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [ClassSessions] ALTER COLUMN [Venue] nvarchar(250) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910031225_CompleteClassSessionManagement'
)
BEGIN
    ALTER TABLE [ClassSessions] ADD [Remark] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910031225_CompleteClassSessionManagement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Subjects_EducationStage_SubjectName_AcademicLevel] ON [Subjects] ([EducationStage], [SubjectName], [AcademicLevel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910031225_CompleteClassSessionManagement'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ClassSessions_ClassName] ON [ClassSessions] ([ClassName]) WHERE [ClassName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910031225_CompleteClassSessionManagement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260910031225_CompleteClassSessionManagement', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910092056_AddEnrollmentApplicantSnapshotFields'
)
BEGIN
    DROP INDEX [IX_EnrollmentApplications_ApplicationNumber] ON [EnrollmentApplications];
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EnrollmentApplications]') AND [c].[name] = N'ApplicationNumber');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [EnrollmentApplications] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [EnrollmentApplications] ALTER COLUMN [ApplicationNumber] nvarchar(7) NOT NULL;
    CREATE UNIQUE INDEX [IX_EnrollmentApplications_ApplicationNumber] ON [EnrollmentApplications] ([ApplicationNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910092056_AddEnrollmentApplicantSnapshotFields'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [GuardianIdentificationNumber] nvarchar(14) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910092056_AddEnrollmentApplicantSnapshotFields'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [StudentGender] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910092056_AddEnrollmentApplicantSnapshotFields'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [StudentIdentificationNumber] nvarchar(30) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910092056_AddEnrollmentApplicantSnapshotFields'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [StudentIdentificationType] nvarchar(30) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910092056_AddEnrollmentApplicantSnapshotFields'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [StudentSchool] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910092056_AddEnrollmentApplicantSnapshotFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260910092056_AddEnrollmentApplicantSnapshotFields', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910095549_AddAdminAndTutorProfiles'
)
BEGIN
    CREATE TABLE [AdminProfiles] (
        [AdminProfileId] int NOT NULL IDENTITY,
        [ApplicationUserId] nvarchar(450) NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [Position] nvarchar(100) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AdminProfiles] PRIMARY KEY ([AdminProfileId]),
        CONSTRAINT [FK_AdminProfiles_AspNetUsers_ApplicationUserId] FOREIGN KEY ([ApplicationUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910095549_AddAdminAndTutorProfiles'
)
BEGIN
    CREATE TABLE [TutorProfiles] (
        [TutorProfileId] int NOT NULL IDENTITY,
        [ApplicationUserId] nvarchar(450) NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [Qualification] nvarchar(200) NOT NULL,
        [TeachingExperience] nvarchar(500) NULL,
        [AreaOfExpertise] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TutorProfiles] PRIMARY KEY ([TutorProfileId]),
        CONSTRAINT [FK_TutorProfiles_AspNetUsers_ApplicationUserId] FOREIGN KEY ([ApplicationUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910095549_AddAdminAndTutorProfiles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AdminProfiles_ApplicationUserId] ON [AdminProfiles] ([ApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910095549_AddAdminAndTutorProfiles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TutorProfiles_ApplicationUserId] ON [TutorProfiles] ([ApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910095549_AddAdminAndTutorProfiles'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260910095549_AddAdminAndTutorProfiles', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EnrollmentApplications]') AND [c].[name] = N'StudentIdentificationNumber');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [EnrollmentApplications] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [EnrollmentApplications] ALTER COLUMN [StudentIdentificationNumber] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EnrollmentApplications]') AND [c].[name] = N'StudentGender');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [EnrollmentApplications] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [EnrollmentApplications] ALTER COLUMN [StudentGender] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EnrollmentApplications]') AND [c].[name] = N'GuardianIdentificationNumber');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [EnrollmentApplications] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [EnrollmentApplications] ALTER COLUMN [GuardianIdentificationNumber] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [DeliveryAddressLine1] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [DeliveryAddressLine2] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [DeliveryCity] nvarchar(80) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [DeliveryPostcode] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [DeliveryRecipientName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [DeliveryRegion] nvarchar(30) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [DeliveryState] nvarchar(80) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [DiscountAmountAtApplication] decimal(10,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [DiscountRateAtApplication] decimal(5,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [DiscountedTuitionAtApplication] decimal(10,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [EnrollmentDurationMonths] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [EstimatedTotalAtApplication] decimal(10,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [HardcopyFeeAtApplication] decimal(10,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [HardcopySubjectCountAtApplication] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [MonthlyTuitionFeeAtApplication] decimal(10,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [ShippingFeeAtApplication] decimal(10,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [TuitionSubtotalAtApplication] decimal(10,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260910104612_AddEnrollmentPlanDeliveryAndFeeSnapshot', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910132839_AddPaymentAndBilling'
)
BEGIN
    CREATE TABLE [Bills] (
        [BillId] int NOT NULL IDENTITY,
        [BillNumber] nvarchar(7) NOT NULL,
        [EnrollmentApplicationId] int NOT NULL,
        [TuitionSubtotal] decimal(10,2) NOT NULL,
        [TuitionDiscountRate] decimal(5,2) NOT NULL,
        [TuitionDiscountAmount] decimal(10,2) NOT NULL,
        [DiscountedTuitionAmount] decimal(10,2) NOT NULL,
        [HardcopyFee] decimal(10,2) NOT NULL,
        [DeliveryFee] decimal(10,2) NOT NULL,
        [TotalAmount] decimal(10,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Status] int NOT NULL,
        [IssuedAt] datetime2 NOT NULL,
        [DueAt] datetime2 NOT NULL,
        [PaidAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        CONSTRAINT [PK_Bills] PRIMARY KEY ([BillId]),
        CONSTRAINT [FK_Bills_EnrollmentApplications_EnrollmentApplicationId] FOREIGN KEY ([EnrollmentApplicationId]) REFERENCES [EnrollmentApplications] ([EnrollmentApplicationId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910132839_AddPaymentAndBilling'
)
BEGIN
    CREATE TABLE [BillItems] (
        [BillItemId] int NOT NULL IDENTITY,
        [BillId] int NOT NULL,
        [ItemType] int NOT NULL,
        [Description] nvarchar(250) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitAmount] decimal(10,2) NOT NULL,
        [LineTotal] decimal(10,2) NOT NULL,
        CONSTRAINT [PK_BillItems] PRIMARY KEY ([BillItemId]),
        CONSTRAINT [FK_BillItems_Bills_BillId] FOREIGN KEY ([BillId]) REFERENCES [Bills] ([BillId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910132839_AddPaymentAndBilling'
)
BEGIN
    CREATE TABLE [Payments] (
        [PaymentId] int NOT NULL IDENTITY,
        [PaymentNumber] nvarchar(7) NOT NULL,
        [BillId] int NOT NULL,
        [Amount] decimal(10,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [PaymentProvider] nvarchar(50) NOT NULL,
        [ProviderCheckoutSessionId] nvarchar(255) NULL,
        [ProviderPaymentIntentId] nvarchar(255) NULL,
        [ProviderEventId] nvarchar(255) NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [FailedAt] datetime2 NULL,
        [FailureReason] nvarchar(500) NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentId]),
        CONSTRAINT [FK_Payments_Bills_BillId] FOREIGN KEY ([BillId]) REFERENCES [Bills] ([BillId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910132839_AddPaymentAndBilling'
)
BEGIN
    CREATE INDEX [IX_BillItems_BillId] ON [BillItems] ([BillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910132839_AddPaymentAndBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Bills_BillNumber] ON [Bills] ([BillNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910132839_AddPaymentAndBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Bills_EnrollmentApplicationId] ON [Bills] ([EnrollmentApplicationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910132839_AddPaymentAndBilling'
)
BEGIN
    CREATE INDEX [IX_Payments_BillId] ON [Payments] ([BillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910132839_AddPaymentAndBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_PaymentNumber] ON [Payments] ([PaymentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910132839_AddPaymentAndBilling'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_ProviderCheckoutSessionId] ON [Payments] ([ProviderCheckoutSessionId]) WHERE [ProviderCheckoutSessionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910132839_AddPaymentAndBilling'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_ProviderEventId] ON [Payments] ([ProviderEventId]) WHERE [ProviderEventId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910132839_AddPaymentAndBilling'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_ProviderPaymentIntentId] ON [Payments] ([ProviderPaymentIntentId]) WHERE [ProviderPaymentIntentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910132839_AddPaymentAndBilling'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260910132839_AddPaymentAndBilling', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910152859_AddBillPaymentAccessToken'
)
BEGIN
    ALTER TABLE [Bills] ADD [PaymentAccessToken] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910152859_AddBillPaymentAccessToken'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Bills_PaymentAccessToken] ON [Bills] ([PaymentAccessToken]) WHERE [PaymentAccessToken] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910152859_AddBillPaymentAccessToken'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260910152859_AddBillPaymentAccessToken', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911185422_AddUniqueStudentIdentification'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StudentProfiles_IdentificationType_IdentificationNumber] ON [StudentProfiles] ([IdentificationType], [IdentificationNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911185422_AddUniqueStudentIdentification'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260911185422_AddUniqueStudentIdentification', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911200636_AddParentStudentRelationship'
)
BEGIN
    CREATE TABLE [ParentStudents] (
        [ParentStudentId] int NOT NULL IDENTITY,
        [ParentProfileId] int NOT NULL,
        [StudentProfileId] int NOT NULL,
        [Relationship] nvarchar(50) NULL,
        [IsPrimaryGuardian] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [LinkedAt] datetime2 NOT NULL,
        [EndedAt] datetime2 NULL,
        CONSTRAINT [PK_ParentStudents] PRIMARY KEY ([ParentStudentId]),
        CONSTRAINT [FK_ParentStudents_ParentProfiles_ParentProfileId] FOREIGN KEY ([ParentProfileId]) REFERENCES [ParentProfiles] ([ParentProfileId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ParentStudents_StudentProfiles_StudentProfileId] FOREIGN KEY ([StudentProfileId]) REFERENCES [StudentProfiles] ([StudentProfileId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911200636_AddParentStudentRelationship'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ParentStudents_ParentProfileId_StudentProfileId] ON [ParentStudents] ([ParentProfileId], [StudentProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911200636_AddParentStudentRelationship'
)
BEGIN
    CREATE INDEX [IX_ParentStudents_StudentProfileId] ON [ParentStudents] ([StudentProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911200636_AddParentStudentRelationship'
)
BEGIN
    INSERT INTO ParentStudents
    (
        ParentProfileId,
        StudentProfileId,
        Relationship,
        IsPrimaryGuardian,
        IsActive,
        LinkedAt,
        EndedAt
    )
    SELECT
        ParentProfileId,
        StudentProfileId,
        NULL,
        1,
        1,
        GETUTCDATE(),
        NULL
    FROM StudentProfiles;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911200636_AddParentStudentRelationship'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260911200636_AddParentStudentRelationship', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913050000_RemoveDuplicateReview'
)
BEGIN
    UPDATE EnrollmentApplications SET Status = 0 WHERE Status = 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913050000_RemoveDuplicateReview'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EnrollmentApplications]') AND [c].[name] = N'PotentialDuplicateGuardian');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [EnrollmentApplications] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [EnrollmentApplications] DROP COLUMN [PotentialDuplicateGuardian];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913050000_RemoveDuplicateReview'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260913050000_RemoveDuplicateReview', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913153749_AddStudentEmail'
)
BEGIN
    ALTER TABLE [StudentProfiles] ADD [Email] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913153749_AddStudentEmail'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260913153749_AddStudentEmail', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913174530_RemoveStudentProfileEmail'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StudentProfiles]') AND [c].[name] = N'Email');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [StudentProfiles] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [StudentProfiles] DROP COLUMN [Email];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913174530_RemoveStudentProfileEmail'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260913174530_RemoveStudentProfileEmail', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913175053_AddStudentEmailToEnrollmentApplication'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [StudentEmail] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913175053_AddStudentEmailToEnrollmentApplication'
)
BEGIN
    ALTER TABLE [EnrollmentApplications] ADD [StudentHasNoEmail] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913175053_AddStudentEmailToEnrollmentApplication'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260913175053_AddStudentEmailToEnrollmentApplication', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913211317_RemoveLegacyParentStudentRelationship'
)
BEGIN
    ALTER TABLE [StudentProfiles] DROP CONSTRAINT [FK_StudentProfiles_ParentProfiles_ParentProfileId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913211317_RemoveLegacyParentStudentRelationship'
)
BEGIN
    DROP INDEX [IX_StudentProfiles_ParentProfileId] ON [StudentProfiles];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913211317_RemoveLegacyParentStudentRelationship'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StudentProfiles]') AND [c].[name] = N'ParentProfileId');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [StudentProfiles] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [StudentProfiles] DROP COLUMN [ParentProfileId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913211317_RemoveLegacyParentStudentRelationship'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260913211317_RemoveLegacyParentStudentRelationship', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929153000_HardenPaymentAccessTokens'
)
BEGIN
    DROP INDEX [IX_Bills_PaymentAccessToken] ON [Bills];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929153000_HardenPaymentAccessTokens'
)
BEGIN
    EXEC sp_rename N'[Bills].[PaymentAccessToken]', N'PaymentAccessTokenHash', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929153000_HardenPaymentAccessTokens'
)
BEGIN
    ALTER TABLE [Bills] ADD [PaymentAccessTokenExpiresAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929153000_HardenPaymentAccessTokens'
)
BEGIN
    UPDATE [Bills] SET [PaymentAccessTokenHash] = NULL, [PaymentAccessTokenExpiresAt] = NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929153000_HardenPaymentAccessTokens'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Bills_PaymentAccessTokenHash] ON [Bills] ([PaymentAccessTokenHash]) WHERE [PaymentAccessTokenHash] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929153000_HardenPaymentAccessTokens'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929153000_HardenPaymentAccessTokens', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929155859_RemoveLegacyDuplicateReviewColumns'
)
BEGIN
    IF COL_LENGTH(
        'EnrollmentApplications',
        'PotentialDuplicateStudent'
    ) IS NOT NULL
    BEGIN
        ALTER TABLE [EnrollmentApplications]
        DROP COLUMN [PotentialDuplicateStudent];
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929155859_RemoveLegacyDuplicateReviewColumns'
)
BEGIN
    IF COL_LENGTH(
        'EnrollmentApplications',
        'PotentialDuplicateGuardian'
    ) IS NOT NULL
    BEGIN
        ALTER TABLE [EnrollmentApplications]
        DROP COLUMN [PotentialDuplicateGuardian];
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929155859_RemoveLegacyDuplicateReviewColumns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929155859_RemoveLegacyDuplicateReviewColumns', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930055037_AddCommunicationAnnouncements'
)
BEGIN
    CREATE TABLE [Announcements] (
        [AnnouncementId] int NOT NULL IDENTITY,
        [Title] nvarchar(150) NOT NULL,
        [Content] nvarchar(4000) NOT NULL,
        [TargetAudience] int NOT NULL,
        [PublishAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NULL,
        [IsArchived] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedByUserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_Announcements] PRIMARY KEY ([AnnouncementId]),
        CONSTRAINT [FK_Announcements_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930055037_AddCommunicationAnnouncements'
)
BEGIN
    CREATE INDEX [IX_Announcements_CreatedByUserId] ON [Announcements] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930055037_AddCommunicationAnnouncements'
)
BEGIN
    CREATE INDEX [IX_Announcements_PublishAt] ON [Announcements] ([PublishAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930055037_AddCommunicationAnnouncements'
)
BEGIN
    CREATE INDEX [IX_Announcements_TargetAudience_IsArchived] ON [Announcements] ([TargetAudience], [IsArchived]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930055037_AddCommunicationAnnouncements'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930055037_AddCommunicationAnnouncements', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930063235_AddAnnouncementReadTracking'
)
BEGIN
    CREATE TABLE [AnnouncementReads] (
        [AnnouncementReadId] int NOT NULL IDENTITY,
        [AnnouncementId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [ReadAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AnnouncementReads] PRIMARY KEY ([AnnouncementReadId]),
        CONSTRAINT [FK_AnnouncementReads_Announcements_AnnouncementId] FOREIGN KEY ([AnnouncementId]) REFERENCES [Announcements] ([AnnouncementId]) ON DELETE CASCADE,
        CONSTRAINT [FK_AnnouncementReads_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930063235_AddAnnouncementReadTracking'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AnnouncementReads_AnnouncementId_UserId] ON [AnnouncementReads] ([AnnouncementId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930063235_AddAnnouncementReadTracking'
)
BEGIN
    CREATE INDEX [IX_AnnouncementReads_UserId_ReadAt] ON [AnnouncementReads] ([UserId], [ReadAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930063235_AddAnnouncementReadTracking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930063235_AddAnnouncementReadTracking', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930070000_AddSecureMessaging'
)
BEGIN
    CREATE TABLE [Conversations] (
        [ConversationId] int NOT NULL IDENTITY,
        [ParentUserId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastMessageAt] datetime2 NULL,
        [IsClosed] bit NOT NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([ConversationId]),
        CONSTRAINT [FK_Conversations_AspNetUsers_ParentUserId] FOREIGN KEY ([ParentUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930070000_AddSecureMessaging'
)
BEGIN
    CREATE TABLE [ChatMessages] (
        [ChatMessageId] int NOT NULL IDENTITY,
        [ConversationId] int NOT NULL,
        [SenderUserId] nvarchar(450) NOT NULL,
        [ProtectedContent] nvarchar(max) NULL,
        [SentAt] datetime2 NOT NULL,
        [ReadAt] datetime2 NULL,
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([ChatMessageId]),
        CONSTRAINT [FK_ChatMessages_AspNetUsers_SenderUserId] FOREIGN KEY ([SenderUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChatMessages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([ConversationId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930070000_AddSecureMessaging'
)
BEGIN
    CREATE TABLE [MessageAttachments] (
        [MessageAttachmentId] int NOT NULL IDENTITY,
        [ChatMessageId] int NOT NULL,
        [OriginalFileName] nvarchar(255) NOT NULL,
        [StoredFileName] nvarchar(80) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [FileSize] bigint NOT NULL,
        [Sha256Hash] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_MessageAttachments] PRIMARY KEY ([MessageAttachmentId]),
        CONSTRAINT [FK_MessageAttachments_ChatMessages_ChatMessageId] FOREIGN KEY ([ChatMessageId]) REFERENCES [ChatMessages] ([ChatMessageId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930070000_AddSecureMessaging'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Conversations_ParentUserId] ON [Conversations] ([ParentUserId]) WHERE [ParentUserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930070000_AddSecureMessaging'
)
BEGIN
    CREATE INDEX [IX_Conversations_LastMessageAt] ON [Conversations] ([LastMessageAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930070000_AddSecureMessaging'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_SenderUserId] ON [ChatMessages] ([SenderUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930070000_AddSecureMessaging'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_ConversationId_SentAt] ON [ChatMessages] ([ConversationId], [SentAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930070000_AddSecureMessaging'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_ConversationId_ReadAt] ON [ChatMessages] ([ConversationId], [ReadAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930070000_AddSecureMessaging'
)
BEGIN
    CREATE INDEX [IX_MessageAttachments_ChatMessageId] ON [MessageAttachments] ([ChatMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930070000_AddSecureMessaging'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930070000_AddSecureMessaging', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930084000_AddMessageEditingAndDeletion'
)
BEGIN
    ALTER TABLE [ChatMessages] ADD [DeletedByUserId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930084000_AddMessageEditingAndDeletion'
)
BEGIN
    ALTER TABLE [ChatMessages] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930084000_AddMessageEditingAndDeletion'
)
BEGIN
    ALTER TABLE [ChatMessages] ADD [EditedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930084000_AddMessageEditingAndDeletion'
)
BEGIN
    ALTER TABLE [ChatMessages] ADD [IsDeletedForEveryone] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930084000_AddMessageEditingAndDeletion'
)
BEGIN
    CREATE TABLE [MessageUserDeletions] (
        [MessageUserDeletionId] int NOT NULL IDENTITY,
        [ChatMessageId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [DeletedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MessageUserDeletions] PRIMARY KEY ([MessageUserDeletionId]),
        CONSTRAINT [FK_MessageUserDeletions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MessageUserDeletions_ChatMessages_ChatMessageId] FOREIGN KEY ([ChatMessageId]) REFERENCES [ChatMessages] ([ChatMessageId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930084000_AddMessageEditingAndDeletion'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_DeletedAt] ON [ChatMessages] ([DeletedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930084000_AddMessageEditingAndDeletion'
)
BEGIN
    CREATE INDEX [IX_MessageUserDeletions_UserId] ON [MessageUserDeletions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930084000_AddMessageEditingAndDeletion'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MessageUserDeletions_ChatMessageId_UserId] ON [MessageUserDeletions] ([ChatMessageId], [UserId]) WHERE [ChatMessageId] IS NOT NULL AND [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930084000_AddMessageEditingAndDeletion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930084000_AddMessageEditingAndDeletion', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930140000_AddSecurityAuditAndFinancialNotes'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payments]') AND [c].[name] = N'FailureReason');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Payments] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [Payments] ALTER COLUMN [FailureReason] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930140000_AddSecurityAuditAndFinancialNotes'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Bills]') AND [c].[name] = N'CancellationReason');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Bills] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [Bills] ALTER COLUMN [CancellationReason] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930140000_AddSecurityAuditAndFinancialNotes'
)
BEGIN
    CREATE TABLE [AuditLogEntries] (
        [AuditLogEntryId] bigint NOT NULL IDENTITY,
        [OccurredAtUtc] datetime2 NOT NULL,
        [ActorUserId] nvarchar(450) NULL,
        [ActorName] nvarchar(256) NOT NULL,
        [EventName] nvarchar(100) NOT NULL,
        [Severity] int NOT NULL,
        [EntityName] nvarchar(120) NOT NULL,
        [EntityKey] nvarchar(256) NOT NULL,
        [RemoteIp] nvarchar(64) NOT NULL,
        [TraceId] nvarchar(120) NOT NULL,
        [Summary] nvarchar(1000) NOT NULL,
        [IntegrityProof] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_AuditLogEntries] PRIMARY KEY ([AuditLogEntryId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930140000_AddSecurityAuditAndFinancialNotes'
)
BEGIN
    CREATE INDEX [IX_AuditLogEntries_OccurredAtUtc] ON [AuditLogEntries] ([OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930140000_AddSecurityAuditAndFinancialNotes'
)
BEGIN
    CREATE INDEX [IX_AuditLogEntries_Severity_OccurredAtUtc] ON [AuditLogEntries] ([Severity], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930140000_AddSecurityAuditAndFinancialNotes'
)
BEGIN
    CREATE INDEX [IX_AuditLogEntries_ActorUserId_OccurredAtUtc] ON [AuditLogEntries] ([ActorUserId], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930140000_AddSecurityAuditAndFinancialNotes'
)
BEGIN
    CREATE INDEX [IX_AuditLogEntries_EventName_RemoteIp_OccurredAtUtc] ON [AuditLogEntries] ([EventName], [RemoteIp], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930140000_AddSecurityAuditAndFinancialNotes'
)
BEGIN
    CREATE TABLE [SecurityEmailDeliveries] (
        [AuditLogEntryId] bigint NOT NULL,
        [SentAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SecurityEmailDeliveries] PRIMARY KEY ([AuditLogEntryId]),
        CONSTRAINT [FK_SecurityEmailDeliveries_AuditLogEntries_AuditLogEntryId] FOREIGN KEY ([AuditLogEntryId]) REFERENCES [AuditLogEntries] ([AuditLogEntryId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930140000_AddSecurityAuditAndFinancialNotes'
)
BEGIN
    CREATE OR ALTER TRIGGER [dbo].[TR_AuditLogEntries_AppendOnly]
    ON [dbo].[AuditLogEntries] AFTER UPDATE, DELETE AS
    BEGIN
        SET NOCOUNT ON;
        THROW 51001, 'Audit log entries are append-only.', 1;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930140000_AddSecurityAuditAndFinancialNotes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930140000_AddSecurityAuditAndFinancialNotes', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003032214_AddClassCalendarAndCommunicationUpgrade'
)
BEGIN
    ALTER TABLE [StudentEnrollments] ADD [WithdrawalReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003032214_AddClassCalendarAndCommunicationUpgrade'
)
BEGIN
    ALTER TABLE [ClassSessions] ADD [EndDate] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003032214_AddClassCalendarAndCommunicationUpgrade'
)
BEGIN
    ALTER TABLE [ClassSessions] ADD [MinimumCapacity] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003032214_AddClassCalendarAndCommunicationUpgrade'
)
BEGIN
    ALTER TABLE [ClassSessions] ADD [StartDate] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003032214_AddClassCalendarAndCommunicationUpgrade'
)
BEGIN
    CREATE TABLE [ClassLessonChanges] (
        [ClassLessonChangeId] int NOT NULL IDENTITY,
        [ClassSessionId] int NOT NULL,
        [OriginalDate] date NOT NULL,
        [OriginalStartTime] time NOT NULL,
        [OriginalEndTime] time NOT NULL,
        [CancellationReason] nvarchar(500) NOT NULL,
        [CancelledAtUtc] datetime2 NOT NULL,
        [ChangedByUserId] nvarchar(450) NOT NULL,
        [ReplacementDate] date NULL,
        [ReplacementStartTime] time NULL,
        [ReplacementEndTime] time NULL,
        [ReplacementVenue] nvarchar(250) NULL,
        [StudentIdsJson] nvarchar(max) NOT NULL,
        [RecipientIdsJson] nvarchar(max) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ClassLessonChanges] PRIMARY KEY ([ClassLessonChangeId]),
        CONSTRAINT [FK_ClassLessonChanges_ClassSessions_ClassSessionId] FOREIGN KEY ([ClassSessionId]) REFERENCES [ClassSessions] ([ClassSessionId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003032214_AddClassCalendarAndCommunicationUpgrade'
)
BEGIN
    CREATE TABLE [UserNotifications] (
        [UserNotificationId] bigint NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Text] nvarchar(1000) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [AvailableFromDate] date NULL,
        [ReadAtUtc] datetime2 NULL,
        CONSTRAINT [PK_UserNotifications] PRIMARY KEY ([UserNotificationId]),
        CONSTRAINT [FK_UserNotifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003032214_AddClassCalendarAndCommunicationUpgrade'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClassLessonChanges_ClassSessionId_OriginalDate] ON [ClassLessonChanges] ([ClassSessionId], [OriginalDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003032214_AddClassCalendarAndCommunicationUpgrade'
)
BEGIN
    CREATE INDEX [IX_UserNotifications_UserId_CreatedAtUtc] ON [UserNotifications] ([UserId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003032214_AddClassCalendarAndCommunicationUpgrade'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003032214_AddClassCalendarAndCommunicationUpgrade', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003050551_SyncClassCalendarRefundModel'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ClassSessions]') AND [c].[name] = N'MinimumCapacity');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [ClassSessions] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [ClassSessions] DROP COLUMN [MinimumCapacity];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003050551_SyncClassCalendarRefundModel'
)
BEGIN
    CREATE TABLE [EnrollmentExitRequests] (
        [EnrollmentExitRequestId] int NOT NULL IDENTITY,
        [StudentEnrollmentId] int NOT NULL,
        [PaymentId] int NULL,
        [RequestedByUserId] nvarchar(450) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [RequestedAtUtc] datetime2 NOT NULL,
        [EffectiveDate] date NOT NULL,
        [EstimatedRefund] decimal(10,2) NOT NULL,
        [ApprovedRefund] decimal(10,2) NOT NULL,
        [RefundStatus] int NOT NULL,
        [ReviewedByUserId] nvarchar(450) NULL,
        [ReviewNote] nvarchar(max) NULL,
        [RefundStartedAtUtc] datetime2 NULL,
        [RefundCompletedAtUtc] datetime2 NULL,
        [ProviderRefundId] nvarchar(255) NULL,
        [LastError] nvarchar(500) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EnrollmentExitRequests] PRIMARY KEY ([EnrollmentExitRequestId]),
        CONSTRAINT [FK_EnrollmentExitRequests_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([PaymentId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EnrollmentExitRequests_StudentEnrollments_StudentEnrollmentId] FOREIGN KEY ([StudentEnrollmentId]) REFERENCES [StudentEnrollments] ([StudentEnrollmentId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003050551_SyncClassCalendarRefundModel'
)
BEGIN
    CREATE INDEX [IX_EnrollmentExitRequests_PaymentId] ON [EnrollmentExitRequests] ([PaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003050551_SyncClassCalendarRefundModel'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EnrollmentExitRequests_ProviderRefundId] ON [EnrollmentExitRequests] ([ProviderRefundId]) WHERE [ProviderRefundId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003050551_SyncClassCalendarRefundModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EnrollmentExitRequests_StudentEnrollmentId] ON [EnrollmentExitRequests] ([StudentEnrollmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003050551_SyncClassCalendarRefundModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003050551_SyncClassCalendarRefundModel', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003093536_AddConversationUserPreferences'
)
BEGIN
    CREATE TABLE [ConversationUserPreferences] (
        [ConversationUserPreferenceId] bigint NOT NULL IDENTITY,
        [ConversationId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [IsPinned] bit NOT NULL,
        [IsHidden] bit NOT NULL,
        [HiddenAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ConversationUserPreferences] PRIMARY KEY ([ConversationUserPreferenceId]),
        CONSTRAINT [FK_ConversationUserPreferences_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ConversationUserPreferences_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([ConversationId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003093536_AddConversationUserPreferences'
)
BEGIN
    CREATE INDEX [IX_ConversationUserPreferences_ConversationId] ON [ConversationUserPreferences] ([ConversationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003093536_AddConversationUserPreferences'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ConversationUserPreferences_UserId_ConversationId] ON [ConversationUserPreferences] ([UserId], [ConversationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003093536_AddConversationUserPreferences'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003093536_AddConversationUserPreferences', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018200329_AddTutorTeachingSubjects'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TutorProfiles]') AND [c].[name] = N'AreaOfExpertise');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [TutorProfiles] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [TutorProfiles] ALTER COLUMN [AreaOfExpertise] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018200329_AddTutorTeachingSubjects'
)
BEGIN
    CREATE TABLE [TutorTeachingSubjects] (
        [TutorTeachingSubjectId] int NOT NULL IDENTITY,
        [TutorProfileId] int NOT NULL,
        [EducationStage] nvarchar(20) NOT NULL,
        [AcademicLevel] nvarchar(20) NOT NULL,
        [SubjectCode] nvarchar(10) NOT NULL,
        CONSTRAINT [PK_TutorTeachingSubjects] PRIMARY KEY ([TutorTeachingSubjectId]),
        CONSTRAINT [FK_TutorTeachingSubjects_TutorProfiles_TutorProfileId] FOREIGN KEY ([TutorProfileId]) REFERENCES [TutorProfiles] ([TutorProfileId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018200329_AddTutorTeachingSubjects'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TutorTeachingSubjects_TutorProfileId_EducationStage_AcademicLevel_SubjectCode] ON [TutorTeachingSubjects] ([TutorProfileId], [EducationStage], [AcademicLevel], [SubjectCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018200329_AddTutorTeachingSubjects'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261018200329_AddTutorTeachingSubjects', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019193351_AddTutorPendingEmailChange'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [PendingTutorEmail] nvarchar(254) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019193351_AddTutorPendingEmailChange'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [TutorEmailChangeExpiresAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019193351_AddTutorPendingEmailChange'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [TutorEmailChangeRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019193351_AddTutorPendingEmailChange'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [TutorEmailChangeRequestedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019193351_AddTutorPendingEmailChange'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [TutorEmailChangeRequestedByUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019193351_AddTutorPendingEmailChange'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019193351_AddTutorPendingEmailChange', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192713_AddTutorAvailabilitySlots'
)
BEGIN
    CREATE TABLE [TutorAvailabilitySlots] (
        [TutorAvailabilitySlotId] int NOT NULL IDENTITY,
        [ApplicationUserId] nvarchar(450) NOT NULL,
        [Day] int NOT NULL,
        [Slot] int NOT NULL,
        CONSTRAINT [PK_TutorAvailabilitySlots] PRIMARY KEY ([TutorAvailabilitySlotId]),
        CONSTRAINT [FK_TutorAvailabilitySlots_AspNetUsers_ApplicationUserId] FOREIGN KEY ([ApplicationUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192713_AddTutorAvailabilitySlots'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TutorAvailabilitySlots_ApplicationUserId_Day_Slot] ON [TutorAvailabilitySlots] ([ApplicationUserId], [Day], [Slot]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192713_AddTutorAvailabilitySlots'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020192713_AddTutorAvailabilitySlots', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024121700_AddAttendanceModule'
)
BEGIN
    CREATE TABLE [AttendanceLessons] (
        [AttendanceLessonId] int NOT NULL IDENTITY,
        [ClassSessionId] int NOT NULL,
        [OriginalDate] date NOT NULL,
        [IsReplacement] bit NOT NULL,
        [ClassLessonChangeId] int NULL,
        [LessonDate] date NOT NULL,
        [StartTime] time NOT NULL,
        [EndTime] time NOT NULL,
        [TutorUserId] nvarchar(450) NOT NULL,
        [ClassCode] nvarchar(30) NOT NULL,
        [ClassName] nvarchar(200) NOT NULL,
        [TutorName] nvarchar(100) NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [UpdatedByUserId] nvarchar(450) NOT NULL,
        [SubmittedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AttendanceLessons] PRIMARY KEY ([AttendanceLessonId]),
        CONSTRAINT [FK_AttendanceLessons_AspNetUsers_TutorUserId] FOREIGN KEY ([TutorUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AttendanceLessons_ClassLessonChanges_ClassLessonChangeId] FOREIGN KEY ([ClassLessonChangeId]) REFERENCES [ClassLessonChanges] ([ClassLessonChangeId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AttendanceLessons_ClassSessions_ClassSessionId] FOREIGN KEY ([ClassSessionId]) REFERENCES [ClassSessions] ([ClassSessionId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024121700_AddAttendanceModule'
)
BEGIN
    CREATE TABLE [AttendanceRecords] (
        [AttendanceRecordId] int NOT NULL IDENTITY,
        [AttendanceLessonId] int NOT NULL,
        [StudentProfileId] int NOT NULL,
        [StudentNumber] nvarchar(20) NOT NULL,
        [StudentName] nvarchar(100) NOT NULL,
        [Status] int NULL,
        [Remark] nvarchar(300) NULL,
        CONSTRAINT [PK_AttendanceRecords] PRIMARY KEY ([AttendanceRecordId]),
        CONSTRAINT [CK_AttendanceRecord_Status] CHECK ([Status] IS NULL OR [Status] IN (1,2,3)),
        CONSTRAINT [FK_AttendanceRecords_AttendanceLessons_AttendanceLessonId] FOREIGN KEY ([AttendanceLessonId]) REFERENCES [AttendanceLessons] ([AttendanceLessonId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AttendanceRecords_StudentProfiles_StudentProfileId] FOREIGN KEY ([StudentProfileId]) REFERENCES [StudentProfiles] ([StudentProfileId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024121700_AddAttendanceModule'
)
BEGIN
    CREATE TABLE [AttendanceCorrectionRequests] (
        [AttendanceCorrectionRequestId] int NOT NULL IDENTITY,
        [AttendanceRecordId] int NOT NULL,
        [RequestedByUserId] nvarchar(450) NOT NULL,
        [RequestedByName] nvarchar(256) NOT NULL,
        [RequestedAtUtc] datetime2 NOT NULL,
        [PreviousStatus] int NULL,
        [RequestedStatus] int NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Decision] int NOT NULL,
        [ReviewedByUserId] nvarchar(450) NULL,
        [ReviewedByName] nvarchar(256) NULL,
        [ReviewedAtUtc] datetime2 NULL,
        [ReviewRemark] nvarchar(500) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AttendanceCorrectionRequests] PRIMARY KEY ([AttendanceCorrectionRequestId]),
        CONSTRAINT [CK_AttendanceCorrection_Decision] CHECK ([Decision] IN (0,1,2)),
        CONSTRAINT [CK_AttendanceCorrection_Status] CHECK ([RequestedStatus] IN (1,2,3)),
        CONSTRAINT [FK_AttendanceCorrectionRequests_AttendanceRecords_AttendanceRecordId] FOREIGN KEY ([AttendanceRecordId]) REFERENCES [AttendanceRecords] ([AttendanceRecordId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024121700_AddAttendanceModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AttendanceCorrectionRequests_AttendanceRecordId] ON [AttendanceCorrectionRequests] ([AttendanceRecordId]) WHERE [Decision] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024121700_AddAttendanceModule'
)
BEGIN
    CREATE INDEX [IX_AttendanceCorrectionRequests_RequestedByUserId_RequestedAtUtc] ON [AttendanceCorrectionRequests] ([RequestedByUserId], [RequestedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024121700_AddAttendanceModule'
)
BEGIN
    CREATE INDEX [IX_AttendanceLessons_ClassLessonChangeId] ON [AttendanceLessons] ([ClassLessonChangeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024121700_AddAttendanceModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AttendanceLessons_ClassSessionId_OriginalDate_IsReplacement] ON [AttendanceLessons] ([ClassSessionId], [OriginalDate], [IsReplacement]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024121700_AddAttendanceModule'
)
BEGIN
    CREATE INDEX [IX_AttendanceLessons_TutorUserId_LessonDate] ON [AttendanceLessons] ([TutorUserId], [LessonDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024121700_AddAttendanceModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AttendanceRecords_AttendanceLessonId_StudentProfileId] ON [AttendanceRecords] ([AttendanceLessonId], [StudentProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024121700_AddAttendanceModule'
)
BEGIN
    CREATE INDEX [IX_AttendanceRecords_StudentProfileId] ON [AttendanceRecords] ([StudentProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024121700_AddAttendanceModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261024121700_AddAttendanceModule', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028053739_AddMonthlyTuitionBilling'
)
BEGIN
    CREATE TABLE [MonthlyInvoices] (
        [MonthlyInvoiceId] int NOT NULL IDENTITY,
        [StudentProfileId] int NOT NULL,
        [Month] date NOT NULL,
        [IssuedAtUtc] datetime2 NOT NULL,
        [DueAtUtc] datetime2 NOT NULL,
        [PaidAtUtc] datetime2 NULL,
        [Total] decimal(10,2) NOT NULL,
        [Status] int NOT NULL,
        [CheckoutKey] uniqueidentifier NOT NULL,
        [CheckoutSessionId] nvarchar(255) NULL,
        [PaymentIntentId] nvarchar(255) NULL,
        CONSTRAINT [PK_MonthlyInvoices] PRIMARY KEY ([MonthlyInvoiceId]),
        CONSTRAINT [FK_MonthlyInvoices_StudentProfiles_StudentProfileId] FOREIGN KEY ([StudentProfileId]) REFERENCES [StudentProfiles] ([StudentProfileId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028053739_AddMonthlyTuitionBilling'
)
BEGIN
    CREATE TABLE [BillingReminders] (
        [BillingReminderId] bigint NOT NULL IDENTITY,
        [MonthlyInvoiceId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Kind] nvarchar(30) NOT NULL,
        [Day] date NOT NULL,
        [EmailAttemptedAtUtc] datetime2 NULL,
        [EmailSentAtUtc] datetime2 NULL,
        [EmailError] nvarchar(300) NULL,
        CONSTRAINT [PK_BillingReminders] PRIMARY KEY ([BillingReminderId]),
        CONSTRAINT [FK_BillingReminders_MonthlyInvoices_MonthlyInvoiceId] FOREIGN KEY ([MonthlyInvoiceId]) REFERENCES [MonthlyInvoices] ([MonthlyInvoiceId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028053739_AddMonthlyTuitionBilling'
)
BEGIN
    CREATE TABLE [MonthlyInvoiceLines] (
        [MonthlyInvoiceLineId] int NOT NULL IDENTITY,
        [MonthlyInvoiceId] int NOT NULL,
        [StudentEnrollmentId] int NOT NULL,
        [Description] nvarchar(250) NOT NULL,
        [Amount] decimal(10,2) NOT NULL,
        CONSTRAINT [PK_MonthlyInvoiceLines] PRIMARY KEY ([MonthlyInvoiceLineId]),
        CONSTRAINT [FK_MonthlyInvoiceLines_MonthlyInvoices_MonthlyInvoiceId] FOREIGN KEY ([MonthlyInvoiceId]) REFERENCES [MonthlyInvoices] ([MonthlyInvoiceId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028053739_AddMonthlyTuitionBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BillingReminders_MonthlyInvoiceId_UserId_Kind_Day] ON [BillingReminders] ([MonthlyInvoiceId], [UserId], [Kind], [Day]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028053739_AddMonthlyTuitionBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MonthlyInvoiceLines_MonthlyInvoiceId_StudentEnrollmentId] ON [MonthlyInvoiceLines] ([MonthlyInvoiceId], [StudentEnrollmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028053739_AddMonthlyTuitionBilling'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MonthlyInvoices_CheckoutSessionId] ON [MonthlyInvoices] ([CheckoutSessionId]) WHERE [CheckoutSessionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028053739_AddMonthlyTuitionBilling'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MonthlyInvoices_PaymentIntentId] ON [MonthlyInvoices] ([PaymentIntentId]) WHERE [PaymentIntentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028053739_AddMonthlyTuitionBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MonthlyInvoices_StudentProfileId_Month] ON [MonthlyInvoices] ([StudentProfileId], [Month]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028053739_AddMonthlyTuitionBilling'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028053739_AddMonthlyTuitionBilling', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028201323_AddTutorEmploymentType'
)
BEGIN
    ALTER TABLE [TutorProfiles] ADD [EmploymentType] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028201323_AddTutorEmploymentType'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028201323_AddTutorEmploymentType', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031152846_AddLearningMaterials'
)
BEGIN
    CREATE TABLE [LearningMaterials] (
        [LearningMaterialId] int NOT NULL IDENTITY,
        [ClassSessionId] int NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [OriginalFileName] nvarchar(180) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [FileSize] int NOT NULL,
        [FileContent] varbinary(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [UploadedByUserId] nvarchar(450) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_LearningMaterials] PRIMARY KEY ([LearningMaterialId]),
        CONSTRAINT [CK_LearningMaterials_FileSize] CHECK ([FileSize] > 0 AND [FileSize] <= 10485760 AND DATALENGTH([FileContent]) = [FileSize]),
        CONSTRAINT [FK_LearningMaterials_ClassSessions_ClassSessionId] FOREIGN KEY ([ClassSessionId]) REFERENCES [ClassSessions] ([ClassSessionId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031152846_AddLearningMaterials'
)
BEGIN
    CREATE INDEX [IX_LearningMaterials_ClassSessionId_IsActive_CreatedAtUtc] ON [LearningMaterials] ([ClassSessionId], [IsActive], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031152846_AddLearningMaterials'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031152846_AddLearningMaterials', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101064047_AddLearningMaterialAttachments'
)
BEGIN
    CREATE TABLE [LearningMaterialAttachments] (
        [LearningMaterialAttachmentId] int NOT NULL IDENTITY,
        [LearningMaterialId] int NOT NULL,
        [OriginalFileName] nvarchar(180) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [FileSize] int NOT NULL,
        [FileContent] varbinary(max) NOT NULL,
        CONSTRAINT [PK_LearningMaterialAttachments] PRIMARY KEY ([LearningMaterialAttachmentId]),
        CONSTRAINT [CK_LearningMaterialAttachments_FileSize] CHECK ([FileSize] > 0 AND [FileSize] <= 10485760 AND DATALENGTH([FileContent]) = [FileSize]),
        CONSTRAINT [FK_LearningMaterialAttachments_LearningMaterials_LearningMaterialId] FOREIGN KEY ([LearningMaterialId]) REFERENCES [LearningMaterials] ([LearningMaterialId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101064047_AddLearningMaterialAttachments'
)
BEGIN
    CREATE INDEX [IX_LearningMaterialAttachments_LearningMaterialId] ON [LearningMaterialAttachments] ([LearningMaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101064047_AddLearningMaterialAttachments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101064047_AddLearningMaterialAttachments', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103202919_AddAssessmentsModule'
)
BEGIN
    CREATE TABLE [Assessments] (
        [AssessmentId] int NOT NULL IDENTITY,
        [ClassSessionId] int NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Kind] int NOT NULL,
        [Instructions] nvarchar(4000) NULL,
        [MaxMarks] decimal(7,2) NOT NULL,
        [OpensAtMyt] datetime2 NOT NULL,
        [DueAtMyt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [PublishedAtUtc] datetime2 NULL,
        [ResultsPublishedAtUtc] datetime2 NULL,
        [CreatedByUserId] nvarchar(450) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Assessments] PRIMARY KEY ([AssessmentId]),
        CONSTRAINT [CK_Assessment_Dates] CHECK ([DueAtMyt] > [OpensAtMyt]),
        CONSTRAINT [CK_Assessment_Kind] CHECK ([Kind] IN (1,2,3)),
        CONSTRAINT [CK_Assessment_Marks] CHECK ([MaxMarks] > 0 AND [MaxMarks] <= 10000),
        CONSTRAINT [FK_Assessments_ClassSessions_ClassSessionId] FOREIGN KEY ([ClassSessionId]) REFERENCES [ClassSessions] ([ClassSessionId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103202919_AddAssessmentsModule'
)
BEGIN
    CREATE TABLE [AssessmentParticipants] (
        [AssessmentParticipantId] int NOT NULL IDENTITY,
        [AssessmentId] int NOT NULL,
        [StudentProfileId] int NOT NULL,
        [StudentNumber] nvarchar(20) NOT NULL,
        [StudentName] nvarchar(100) NOT NULL,
        [SubmittedAtUtc] datetime2 NULL,
        [SubmissionNote] nvarchar(2000) NULL,
        [Marks] decimal(7,2) NULL,
        [Feedback] nvarchar(2000) NULL,
        [GradedByUserId] nvarchar(450) NULL,
        [GradedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AssessmentParticipants] PRIMARY KEY ([AssessmentParticipantId]),
        CONSTRAINT [CK_AssessmentParticipant_Grade] CHECK (([Marks] IS NULL AND [GradedAtUtc] IS NULL) OR ([SubmittedAtUtc] IS NOT NULL AND [Marks] IS NOT NULL AND [GradedAtUtc] IS NOT NULL)),
        CONSTRAINT [CK_AssessmentParticipant_Marks] CHECK ([Marks] IS NULL OR ([Marks] >= 0 AND [Marks] <= 10000)),
        CONSTRAINT [FK_AssessmentParticipants_Assessments_AssessmentId] FOREIGN KEY ([AssessmentId]) REFERENCES [Assessments] ([AssessmentId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AssessmentParticipants_StudentProfiles_StudentProfileId] FOREIGN KEY ([StudentProfileId]) REFERENCES [StudentProfiles] ([StudentProfileId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103202919_AddAssessmentsModule'
)
BEGIN
    CREATE TABLE [AssessmentFiles] (
        [AssessmentFileId] int NOT NULL IDENTITY,
        [AssessmentId] int NOT NULL,
        [AssessmentParticipantId] int NULL,
        [Name] nvarchar(180) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [FileSize] int NOT NULL,
        [Content] varbinary(max) NOT NULL,
        CONSTRAINT [PK_AssessmentFiles] PRIMARY KEY ([AssessmentFileId]),
        CONSTRAINT [CK_AssessmentFile_Size] CHECK ([FileSize] > 0 AND [FileSize] <= 10485760 AND DATALENGTH([Content]) = [FileSize]),
        CONSTRAINT [FK_AssessmentFiles_AssessmentParticipants_AssessmentParticipantId] FOREIGN KEY ([AssessmentParticipantId]) REFERENCES [AssessmentParticipants] ([AssessmentParticipantId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AssessmentFiles_Assessments_AssessmentId] FOREIGN KEY ([AssessmentId]) REFERENCES [Assessments] ([AssessmentId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103202919_AddAssessmentsModule'
)
BEGIN
    CREATE INDEX [IX_AssessmentFiles_AssessmentId_AssessmentParticipantId] ON [AssessmentFiles] ([AssessmentId], [AssessmentParticipantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103202919_AddAssessmentsModule'
)
BEGIN
    CREATE INDEX [IX_AssessmentFiles_AssessmentParticipantId] ON [AssessmentFiles] ([AssessmentParticipantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103202919_AddAssessmentsModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AssessmentParticipants_AssessmentId_StudentProfileId] ON [AssessmentParticipants] ([AssessmentId], [StudentProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103202919_AddAssessmentsModule'
)
BEGIN
    CREATE INDEX [IX_AssessmentParticipants_StudentProfileId] ON [AssessmentParticipants] ([StudentProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103202919_AddAssessmentsModule'
)
BEGIN
    CREATE INDEX [IX_Assessments_ClassSessionId_IsActive_DueAtMyt] ON [Assessments] ([ClassSessionId], [IsActive], [DueAtMyt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103202919_AddAssessmentsModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103202919_AddAssessmentsModule', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107084603_AddGuestSupportEnquiries'
)
BEGIN
    CREATE TABLE [GuestEnquiries] (
        [GuestEnquiryId] int NOT NULL IDENTITY,
        [SubmissionKey] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [IsResolved] bit NOT NULL,
        [ResolvedAtUtc] datetime2 NULL,
        [ProtectedContact] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_GuestEnquiries] PRIMARY KEY ([GuestEnquiryId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107084603_AddGuestSupportEnquiries'
)
BEGIN
    CREATE INDEX [IX_GuestEnquiries_IsResolved_CreatedAtUtc] ON [GuestEnquiries] ([IsResolved], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107084603_AddGuestSupportEnquiries'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GuestEnquiries_SubmissionKey] ON [GuestEnquiries] ([SubmissionKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107084603_AddGuestSupportEnquiries'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107084603_AddGuestSupportEnquiries', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108092750_AddGuestEnquiryReplies'
)
BEGIN
    ALTER TABLE [GuestEnquiries] ADD [ProtectedReply] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108092750_AddGuestEnquiryReplies'
)
BEGIN
    ALTER TABLE [GuestEnquiries] ADD [ReadAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108092750_AddGuestEnquiryReplies'
)
BEGIN
    ALTER TABLE [GuestEnquiries] ADD [ReplyAttemptId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108092750_AddGuestEnquiryReplies'
)
BEGIN
    ALTER TABLE [GuestEnquiries] ADD [ReplySentAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108092750_AddGuestEnquiryReplies'
)
BEGIN
    ALTER TABLE [GuestEnquiries] ADD [ReplyState] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108092750_AddGuestEnquiryReplies'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108092750_AddGuestEnquiryReplies', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108102636_AddGuestReplyRecovery'
)
BEGIN
    ALTER TABLE [GuestEnquiries] ADD [ProtectedRecoveryHistory] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108102636_AddGuestReplyRecovery'
)
BEGIN
    ALTER TABLE [GuestEnquiries] ADD [ReplyClaimedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108102636_AddGuestReplyRecovery'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108102636_AddGuestReplyRecovery', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108150202_AllowSequentialReenrollment'
)
BEGIN
    DROP INDEX [IX_StudentEnrollments_StudentProfileId_ClassSessionId] ON [StudentEnrollments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108150202_AllowSequentialReenrollment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StudentEnrollments_StudentProfileId_ClassSessionId_StartDate] ON [StudentEnrollments] ([StudentProfileId], [ClassSessionId], [StartDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108150202_AllowSequentialReenrollment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108150202_AllowSequentialReenrollment', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108161803_AddGuestFollowUpConversation'
)
BEGIN
    ALTER TABLE [GuestEnquiries] ADD [ProtectedConversation] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108161803_AddGuestFollowUpConversation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108161803_AddGuestFollowUpConversation', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108182348_AddVideoPlaybackVault'
)
BEGIN
    CREATE TABLE [RecordedLessons] (
        [RecordedLessonId] int NOT NULL IDENTITY,
        [ClassSessionId] int NOT NULL,
        [UploadedByUserId] nvarchar(450) NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [RecordingDate] date NOT NULL,
        [StorageKey] nvarchar(40) NOT NULL,
        [OriginalFileName] nvarchar(180) NOT NULL,
        [FileSize] bigint NOT NULL,
        [DurationSeconds] float NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_RecordedLessons] PRIMARY KEY ([RecordedLessonId]),
        CONSTRAINT [CK_RecordedLesson_Duration] CHECK ([DurationSeconds] > 0 AND [DurationSeconds] <= 28800),
        CONSTRAINT [CK_RecordedLesson_Size] CHECK ([FileSize] > 0 AND [FileSize] <= 209715200),
        CONSTRAINT [FK_RecordedLessons_AspNetUsers_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RecordedLessons_ClassSessions_ClassSessionId] FOREIGN KEY ([ClassSessionId]) REFERENCES [ClassSessions] ([ClassSessionId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108182348_AddVideoPlaybackVault'
)
BEGIN
    CREATE TABLE [VideoWatchProgress] (
        [VideoWatchProgressId] int NOT NULL IDENTITY,
        [RecordedLessonId] int NOT NULL,
        [StudentUserId] nvarchar(450) NOT NULL,
        [PositionSeconds] float NOT NULL,
        [WatchedSeconds] float NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VideoWatchProgress] PRIMARY KEY ([VideoWatchProgressId]),
        CONSTRAINT [CK_VideoWatch_Seconds] CHECK ([PositionSeconds] >= 0 AND [WatchedSeconds] >= 0),
        CONSTRAINT [FK_VideoWatchProgress_AspNetUsers_StudentUserId] FOREIGN KEY ([StudentUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VideoWatchProgress_RecordedLessons_RecordedLessonId] FOREIGN KEY ([RecordedLessonId]) REFERENCES [RecordedLessons] ([RecordedLessonId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108182348_AddVideoPlaybackVault'
)
BEGIN
    CREATE INDEX [IX_RecordedLessons_ClassSessionId_IsActive_RecordingDate] ON [RecordedLessons] ([ClassSessionId], [IsActive], [RecordingDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108182348_AddVideoPlaybackVault'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RecordedLessons_StorageKey] ON [RecordedLessons] ([StorageKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108182348_AddVideoPlaybackVault'
)
BEGIN
    CREATE INDEX [IX_RecordedLessons_UploadedByUserId] ON [RecordedLessons] ([UploadedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108182348_AddVideoPlaybackVault'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VideoWatchProgress_RecordedLessonId_StudentUserId] ON [VideoWatchProgress] ([RecordedLessonId], [StudentUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108182348_AddVideoPlaybackVault'
)
BEGIN
    CREATE INDEX [IX_VideoWatchProgress_StudentUserId] ON [VideoWatchProgress] ([StudentUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108182348_AddVideoPlaybackVault'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108182348_AddVideoPlaybackVault', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109130047_AddVideoAdminRestore'
)
BEGIN
    ALTER TABLE [RecordedLessons] ADD [InactivatedByAdmin] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109130047_AddVideoAdminRestore'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109130047_AddVideoAdminRestore', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109150019_AddClassClosure'
)
BEGIN
    ALTER TABLE [ClassSessions] ADD [ClosedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109150019_AddClassClosure'
)
BEGIN
    ALTER TABLE [ClassSessions] ADD [ClosedByUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109150019_AddClassClosure'
)
BEGIN
    ALTER TABLE [ClassSessions] ADD [ClosureReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109150019_AddClassClosure'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109150019_AddClassClosure', N'9.0.0');
END;

COMMIT;
GO

