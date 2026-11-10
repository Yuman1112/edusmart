BEGIN TRANSACTION;
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

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109155631_AddEnrollmentNotices'
)
BEGIN
    CREATE TABLE [EnrollmentNotices] (
        [EnrollmentNoticeId] bigint NOT NULL IDENTITY,
        [EnrollmentApplicationId] int NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [RecipientRole] nvarchar(20) NOT NULL,
        [ProtectedPayload] nvarchar(max) NOT NULL,
        [State] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [AttemptedAtUtc] datetime2 NULL,
        [SentAtUtc] datetime2 NULL,
        [AttemptId] uniqueidentifier NULL,
        [Note] nvarchar(200) NULL,
        CONSTRAINT [PK_EnrollmentNotices] PRIMARY KEY ([EnrollmentNoticeId]),
        CONSTRAINT [FK_EnrollmentNotices_EnrollmentApplications_EnrollmentApplicationId] FOREIGN KEY ([EnrollmentApplicationId]) REFERENCES [EnrollmentApplications] ([EnrollmentApplicationId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109155631_AddEnrollmentNotices'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EnrollmentNotices_EnrollmentApplicationId_Kind_RecipientRole] ON [EnrollmentNotices] ([EnrollmentApplicationId], [Kind], [RecipientRole]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109155631_AddEnrollmentNotices'
)
BEGIN
    CREATE INDEX [IX_EnrollmentNotices_State_CreatedAtUtc] ON [EnrollmentNotices] ([State], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109155631_AddEnrollmentNotices'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109155631_AddEnrollmentNotices', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109155656_UpdateApplicationModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109155656_UpdateApplicationModel', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110014007_AddWithdrawalCancellation'
)
BEGIN
    DROP INDEX [IX_EnrollmentExitRequests_StudentEnrollmentId] ON [EnrollmentExitRequests];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110014007_AddWithdrawalCancellation'
)
BEGIN
    ALTER TABLE [EnrollmentExitRequests] ADD [CancelledAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110014007_AddWithdrawalCancellation'
)
BEGIN
    ALTER TABLE [EnrollmentExitRequests] ADD [CancelledByUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110014007_AddWithdrawalCancellation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EnrollmentExitRequests_StudentEnrollmentId] ON [EnrollmentExitRequests] ([StudentEnrollmentId]) WHERE [CancelledAtUtc] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110014007_AddWithdrawalCancellation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110014007_AddWithdrawalCancellation', N'9.0.0');
END;

COMMIT;
GO

