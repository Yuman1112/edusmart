/* ============================================================================
   Secure Smart Tuition System — SQL Server Database Schema
   Covers the 5 modules owned by this report:
     Class & Enrollment Management | Payment & Billing |
     Communication & Announcement | Reporting & Analytics | System Security
   ----------------------------------------------------------------------------
   Notes:
   - Tables marked [STUB - other module] belong to the teammate's modules
     (User Authentication & RBAC, Tutor Management, Learning & Assessment,
     Attendance Management, Video Playback Vault) and are included here only
     because the five modules in this report hold a foreign key into them.
     Only the minimum columns needed for those foreign keys/joins are defined.
   - This script matches the EF Core Code-First model in /Models exactly, so
     it can be used directly, OR you can delete this file and let
     `Add-Migration InitialCreate` / `dotnet ef database update` generate the
     same schema from the C# models instead.
   - AES-256 encrypted columns (Payment.CardTokenEncrypted, Payment.PayerNoteEncrypted,
     Invoice.RemarksEncrypted) are encrypted at the APPLICATION layer (see
     Services/AesEncryptionService.cs + AppDbContext value converters) before
     EF Core persists them, per Chapter 2.3 "Adopted Approach and
     Implementation" (Payment & Billing Module). They are therefore stored as
     NVARCHAR (base64 ciphertext), not SQL Server's native ENCRYPTBYKEY.
   ============================================================================ */

IF DB_ID('SecureSmartTuitionSystemDb') IS NULL
BEGIN
    PRINT 'Run this script against an existing SecureSmartTuitionSystemDb database, or create one first.';
END
GO

/* ============================================================================
   1. STUB TABLES (owned by teammate modules, referenced here as FKs only)
   ============================================================================ */

-- [STUB - User Authentication & RBAC Module]
CREATE TABLE [dbo].[AspNetUsers] (
    [Id]                NVARCHAR(450)   NOT NULL PRIMARY KEY,   -- ASP.NET Core Identity Guid
    [UserName]          NVARCHAR(256)   NOT NULL,
    [Email]             NVARCHAR(256)   NOT NULL,
    [Role]              NVARCHAR(30)    NOT NULL,               -- 'Admin' | 'Guardian' | 'Student' | 'Tutor'
    [IsActive]          BIT             NOT NULL DEFAULT (1),   -- flipped to 0 by overdue-suspension (Payment & Billing)
    [CreatedAt]         DATETIME2       NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO
CREATE UNIQUE INDEX [IX_AspNetUsers_Email] ON [dbo].[AspNetUsers]([Email]);
GO

-- [STUB - Tutor Management Module]
CREATE TABLE [dbo].[Tutors] (
    [TutorId]           INT             IDENTITY(1,1) PRIMARY KEY,
    [UserId]            NVARCHAR(450)   NOT NULL REFERENCES [dbo].[AspNetUsers]([Id]),
    [FullName]          NVARCHAR(150)   NOT NULL,
    [SubjectSpecialism]  NVARCHAR(100)  NULL
);
GO

-- [STUB - Learning & Assessment Module] — needed for deadline/grading notifications
-- and the Assignment Submission Rate Report.
CREATE TABLE [dbo].[Assignments] (
    [AssignmentId]      INT             IDENTITY(1,1) PRIMARY KEY,
    [ClassSessionId]    INT             NOT NULL,               -- FK added after ClassSessions is created
    [Title]              NVARCHAR(200)  NOT NULL,
    [DueDate]            DATETIME2      NOT NULL,
    [LateSubmissionAllowed] BIT         NOT NULL DEFAULT (0),
    [CreatedAt]          DATETIME2      NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE [dbo].[AssignmentSubmissions] (
    [SubmissionId]      INT             IDENTITY(1,1) PRIMARY KEY,
    [AssignmentId]      INT             NOT NULL REFERENCES [dbo].[Assignments]([AssignmentId]),
    [StudentId]         INT             NOT NULL,               -- FK added after Students is created
    [SubmittedAt]       DATETIME2       NULL,                   -- NULL = not yet submitted
    [IsLate]            BIT             NOT NULL DEFAULT (0),
    [Score]             DECIMAL(5,2)    NULL,
    [GradedAt]          DATETIME2       NULL
);
GO

/* ============================================================================
   2. CLASS & ENROLLMENT MANAGEMENT MODULE
   ============================================================================ */

CREATE TABLE [dbo].[Guardians] (
    [GuardianId]        INT             IDENTITY(1,1) PRIMARY KEY,
    [UserId]            NVARCHAR(450)   NULL REFERENCES [dbo].[AspNetUsers]([Id]),  -- NULL until enrollment fee paid (per report: account only activated after payment)
    [FullName]          NVARCHAR(150)   NOT NULL,
    [Email]             NVARCHAR(256)   NOT NULL,
    [PhoneNumber]       NVARCHAR(30)    NOT NULL,
    [IdentificationNo]  NVARCHAR(30)    NOT NULL,               -- IC / passport
    [CreatedAt]         DATETIME2       NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO
-- Exact-match duplicate check target: email (Chapter 2.2 Adopted Approach)
CREATE UNIQUE INDEX [IX_Guardians_Email] ON [dbo].[Guardians]([Email]);
GO

CREATE TABLE [dbo].[Students] (
    [StudentId]         INT             IDENTITY(1,1) PRIMARY KEY,
    [UserId]            NVARCHAR(450)   NULL REFERENCES [dbo].[AspNetUsers]([Id]),  -- NULL until enrollment fee paid
    [GuardianId]        INT             NOT NULL REFERENCES [dbo].[Guardians]([GuardianId]),
    [FullName]          NVARCHAR(150)   NOT NULL,
    [BirthDate]         DATE            NOT NULL,
    [FormYear]          NVARCHAR(20)    NOT NULL,               -- e.g. "Form 4", "Year 6"
    [CreatedAt]         DATETIME2       NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO
-- Blocking index for the Jaro-Winkler duplicate check: narrow candidates by BirthDate first
CREATE INDEX [IX_Students_BirthDate] ON [dbo].[Students]([BirthDate]);
GO

CREATE TABLE [dbo].[Subjects] (
    [SubjectId]         INT             IDENTITY(1,1) PRIMARY KEY,
    [SubjectName]       NVARCHAR(100)   NOT NULL,
    [FormYear]          NVARCHAR(20)    NOT NULL,
    [MonthlyFee]        DECIMAL(10,2)   NOT NULL,               -- fee-rate table used by FeeCalculationService
    [IsActive]          BIT             NOT NULL DEFAULT (1)
);
GO

CREATE TABLE [dbo].[ClassSessions] (
    [ClassSessionId]    INT             IDENTITY(1,1) PRIMARY KEY,
    [SubjectId]         INT             NOT NULL REFERENCES [dbo].[Subjects]([SubjectId]),
    [TutorId]           INT             NULL REFERENCES [dbo].[Tutors]([TutorId]),
    [DayOfWeek]         TINYINT         NOT NULL,               -- 0=Sunday .. 6=Saturday
    [StartTime]         TIME            NOT NULL,
    [EndTime]           TIME            NOT NULL,
    [Venue]             NVARCHAR(100)   NOT NULL,
    [MinCapacity]        INT            NOT NULL DEFAULT (1),
    [MaxCapacity]         INT           NOT NULL DEFAULT (30),
    [Status]            NVARCHAR(20)    NOT NULL DEFAULT ('Active'),  -- Active | Cancelled | Rescheduled
    [CancellationReason]  NVARCHAR(300) NULL,
    [CreatedAt]         DATETIME2       NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO
ALTER TABLE [dbo].[Assignments]
    ADD CONSTRAINT [FK_Assignments_ClassSessions]
    FOREIGN KEY ([ClassSessionId]) REFERENCES [dbo].[ClassSessions]([ClassSessionId]);
GO
ALTER TABLE [dbo].[AssignmentSubmissions]
    ADD CONSTRAINT [FK_AssignmentSubmissions_Students]
    FOREIGN KEY ([StudentId]) REFERENCES [dbo].[Students]([StudentId]);
GO

-- Enrollment application (one row per class a student applies for)
CREATE TABLE [dbo].[Enrollments] (
    [EnrollmentId]      INT             IDENTITY(1,1) PRIMARY KEY,
    [StudentId]         INT             NULL REFERENCES [dbo].[Students]([StudentId]),   -- may be NULL for a brand-new applicant not yet persisted as Student until de-dup review completes
    [GuardianId]        INT             NULL REFERENCES [dbo].[Guardians]([GuardianId]),
    [ClassSessionId]    INT             NOT NULL REFERENCES [dbo].[ClassSessions]([ClassSessionId]),
    [RequestedStartDate] DATE           NOT NULL,
    [HardcopyMaterialsOptIn] BIT        NOT NULL DEFAULT (0),
    [Status]            NVARCHAR(20)    NOT NULL DEFAULT ('Pending'),  -- Pending | FlaggedDuplicate | Approved | Rejected
    [DuplicateFlag]      BIT            NOT NULL DEFAULT (0),
    [DuplicateMatchStudentId] INT       NULL REFERENCES [dbo].[Students]([StudentId]),
    [DuplicateMatchScore] DECIMAL(5,4)  NULL,                   -- Jaro-Winkler score of the flagged match
    [RejectionReason]    NVARCHAR(300)  NULL,
    [SubmittedAt]        DATETIME2      NOT NULL DEFAULT (SYSUTCDATETIME()),
    [DecidedAt]          DATETIME2      NULL,
    [DecidedByUserId]    NVARCHAR(450)  NULL REFERENCES [dbo].[AspNetUsers]([Id])
);
GO
CREATE INDEX [IX_Enrollments_Status] ON [dbo].[Enrollments]([Status]);
GO

-- Applicant's raw submitted details, captured BEFORE a Student/Guardian record
-- is confirmed to exist (used purely for the de-dup comparison + review screen)
CREATE TABLE [dbo].[EnrollmentApplicants] (
    [EnrollmentId]       INT            NOT NULL PRIMARY KEY REFERENCES [dbo].[Enrollments]([EnrollmentId]),
    [StudentFullName]    NVARCHAR(150)  NOT NULL,
    [StudentBirthDate]   DATE           NOT NULL,
    [GuardianFullName]   NVARCHAR(150)  NOT NULL,
    [GuardianEmail]      NVARCHAR(256)  NOT NULL,
    -- Full number/IC captured so a real Guardian record can be created on
    -- approval; only the last 4 digits are ever used for the exact-match
    -- duplicate check (Ch.2.2) — see EnrollmentApplicant.GuardianPhoneOrIcLast4
    -- (a computed, non-persisted property) in the EF Core model.
    [GuardianPhoneNumber]      NVARCHAR(30) NOT NULL,
    [GuardianIdentificationNo] NVARCHAR(30) NOT NULL
);
GO

-- Multiple-subject selection per enrollment (checkbox multi-select)
CREATE TABLE [dbo].[EnrollmentSubjects] (
    [EnrollmentSubjectId] INT           IDENTITY(1,1) PRIMARY KEY,
    [EnrollmentId]        INT           NOT NULL REFERENCES [dbo].[Enrollments]([EnrollmentId]),
    [SubjectId]           INT           NOT NULL REFERENCES [dbo].[Subjects]([SubjectId])
);
GO
CREATE UNIQUE INDEX [IX_EnrollmentSubjects_Unique] ON [dbo].[EnrollmentSubjects]([EnrollmentId],[SubjectId]);
GO

-- Add/Drop subject requests made after initial enrollment
CREATE TABLE [dbo].[SubjectChangeRequests] (
    [SubjectChangeRequestId] INT        IDENTITY(1,1) PRIMARY KEY,
    [StudentId]           INT           NOT NULL REFERENCES [dbo].[Students]([StudentId]),
    [SubjectId]           INT           NOT NULL REFERENCES [dbo].[Subjects]([SubjectId]),
    [ChangeType]          NVARCHAR(10)  NOT NULL,               -- 'Add' | 'Drop'
    [EffectiveMonth]      DATE          NOT NULL,               -- first day of the billing month it applies to
    [RequestedByUserId]   NVARCHAR(450) NOT NULL REFERENCES [dbo].[AspNetUsers]([Id]),
    [Status]              NVARCHAR(20)  NOT NULL DEFAULT ('Pending'), -- Pending|Applied|Rejected
    [DoubleConfirmed]     BIT           NOT NULL DEFAULT (0),
    [CreatedAt]           DATETIME2     NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO

/* ============================================================================
   3. PAYMENT & BILLING MODULE
   ============================================================================ */

CREATE TABLE [dbo].[Invoices] (
    [InvoiceId]         INT             IDENTITY(1,1) PRIMARY KEY,
    [StudentId]         INT             NOT NULL REFERENCES [dbo].[Students]([StudentId]),
    [EnrollmentId]       INT            NULL REFERENCES [dbo].[Enrollments]([EnrollmentId]),  -- set for the very first invoice
    [BillingMonth]       DATE           NOT NULL,               -- first day of the billed month
    [InvoiceType]         NVARCHAR(20)  NOT NULL DEFAULT ('Monthly'), -- Monthly | AddDropAdjustment | Enrollment
    [SubTotal]           DECIMAL(10,2)  NOT NULL,
    [HardcopyFee]         DECIMAL(10,2) NOT NULL DEFAULT (0),
    [DiscountAmount]      DECIMAL(10,2) NOT NULL DEFAULT (0),
    [AdditionalCharge]    DECIMAL(10,2) NOT NULL DEFAULT (0),
    [TotalAmount]         DECIMAL(10,2) NOT NULL,
    [Status]              NVARCHAR(20)  NOT NULL DEFAULT ('PendingPayment'), -- PendingPayment|Paid|Overdue|Cancelled
    [DueDate]             DATE          NOT NULL,
    [RemarksEncrypted]    NVARCHAR(500) NULL,                   -- AES-256 encrypted free-text remarks
    [CreatedAt]           DATETIME2     NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO
CREATE INDEX [IX_Invoices_Student_Status] ON [dbo].[Invoices]([StudentId],[Status]);
GO

CREATE TABLE [dbo].[InvoiceLineItems] (
    [InvoiceLineItemId] INT             IDENTITY(1,1) PRIMARY KEY,
    [InvoiceId]         INT             NOT NULL REFERENCES [dbo].[Invoices]([InvoiceId]),
    [SubjectId]         INT             NULL REFERENCES [dbo].[Subjects]([SubjectId]),
    [Description]       NVARCHAR(200)   NOT NULL,
    [Amount]            DECIMAL(10,2)   NOT NULL
);
GO

CREATE TABLE [dbo].[Payments] (
    [PaymentId]          INT            IDENTITY(1,1) PRIMARY KEY,
    [InvoiceId]          INT            NOT NULL REFERENCES [dbo].[Invoices]([InvoiceId]),
    [AmountPaid]         DECIMAL(10,2)  NOT NULL,
    [PaymentMethod]      NVARCHAR(30)   NOT NULL,               -- Card | OnlineBanking | EWallet
    -- PCI-DSS aligned: the raw card/bank number is NEVER stored here. Only the
    -- non-sensitive tokenised reference returned by the third-party payment
    -- gateway is stored, and it is additionally AES-256 encrypted at rest.
    [GatewayTokenEncrypted] NVARCHAR(500) NOT NULL,
    [PayerNoteEncrypted] NVARCHAR(500)  NULL,
    [Status]              NVARCHAR(20)  NOT NULL DEFAULT ('Pending'),  -- Pending|Success|Failed
    [FailureReason]       NVARCHAR(300) NULL,
    [ProcessedAt]         DATETIME2     NULL,
    [CreatedAt]           DATETIME2     NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE [dbo].[Receipts] (
    [ReceiptId]          INT            IDENTITY(1,1) PRIMARY KEY,
    [PaymentId]          INT            NOT NULL REFERENCES [dbo].[Payments]([PaymentId]),
    [ReceiptNumber]      NVARCHAR(30)   NOT NULL,
    [IssuedAt]           DATETIME2      NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO
CREATE UNIQUE INDEX [IX_Receipts_ReceiptNumber] ON [dbo].[Receipts]([ReceiptNumber]);
GO

-- One-time secure, time-limited payment link issued after enrollment approval
CREATE TABLE [dbo].[PaymentLinks] (
    [PaymentLinkId]      INT            IDENTITY(1,1) PRIMARY KEY,
    [InvoiceId]          INT            NOT NULL REFERENCES [dbo].[Invoices]([InvoiceId]),
    [Token]              NVARCHAR(200)  NOT NULL,               -- cryptographically random, single-use
    [ExpiresAt]          DATETIME2      NOT NULL,
    [UsedAt]             DATETIME2      NULL
);
GO
CREATE UNIQUE INDEX [IX_PaymentLinks_Token] ON [dbo].[PaymentLinks]([Token]);
GO

/* ============================================================================
   4. COMMUNICATION & ANNOUNCEMENT MODULE
   ============================================================================ */

CREATE TABLE [dbo].[Notifications] (
    [NotificationId]     INT            IDENTITY(1,1) PRIMARY KEY,
    [UserId]             NVARCHAR(450)  NOT NULL REFERENCES [dbo].[AspNetUsers]([Id]),
    [NotificationType]   NVARCHAR(40)   NOT NULL,   -- BillGenerated|PaymentConfirmed|PaymentFailed|PaymentOverdueWarning|PaymentOverdueFinal|
                                                     -- AccountSuspended|AccountReactivated|MaterialUploaded|AssignmentReminder|
                                                     -- AssignmentOverdue|AssignmentGraded|AnnouncementPosted|ScheduleChange|
                                                     -- EnrollmentApproved|EnrollmentRejected|ChatEscalation|SubjectAddCharge|
                                                     -- SubjectDropCredit|NewMessage
    [ReferenceId]        INT            NULL,       -- id of the related Invoice/Assignment/Enrollment/etc.
    [Title]              NVARCHAR(150)  NOT NULL,
    [Message]            NVARCHAR(500)  NOT NULL,
    [IsRead]             BIT            NOT NULL DEFAULT (0),
    [CreatedAt]           DATETIME2     NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO
-- Enforces the "once per notification-type per user per day" rule efficiently
CREATE INDEX [IX_Notifications_User_Type_Date] ON [dbo].[Notifications]([UserId],[NotificationType],[CreatedAt]);
GO

CREATE TABLE [dbo].[Conversations] (
    [ConversationId]     INT            IDENTITY(1,1) PRIMARY KEY,
    [Subject]            NVARCHAR(150)  NULL,
    [IsArchived]         BIT            NOT NULL DEFAULT (0),
    [CreatedAt]           DATETIME2     NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE [dbo].[ConversationParticipants] (
    [ConversationParticipantId] INT     IDENTITY(1,1) PRIMARY KEY,
    [ConversationId]     INT            NOT NULL REFERENCES [dbo].[Conversations]([ConversationId]),
    [UserId]             NVARCHAR(450)  NOT NULL REFERENCES [dbo].[AspNetUsers]([Id]),
    [IsDeletedForUser]    BIT           NOT NULL DEFAULT (0)
);
GO
CREATE UNIQUE INDEX [IX_ConversationParticipants_Unique] ON [dbo].[ConversationParticipants]([ConversationId],[UserId]);
GO

CREATE TABLE [dbo].[Messages] (
    [MessageId]          INT            IDENTITY(1,1) PRIMARY KEY,
    [ConversationId]     INT            NOT NULL REFERENCES [dbo].[Conversations]([ConversationId]),
    [SenderUserId]       NVARCHAR(450)  NOT NULL REFERENCES [dbo].[AspNetUsers]([Id]),
    [Body]               NVARCHAR(2000) NOT NULL,               -- Razor auto-encodes on render; never trusted raw HTML
    [SentAt]             DATETIME2      NOT NULL DEFAULT (SYSUTCDATETIME()),
    [IsDeliveredRealtime] BIT           NOT NULL DEFAULT (0)
);
GO
CREATE INDEX [IX_Messages_Conversation_SentAt] ON [dbo].[Messages]([ConversationId],[SentAt]);
GO

CREATE TABLE [dbo].[MessageAttachments] (
    [MessageAttachmentId] INT           IDENTITY(1,1) PRIMARY KEY,
    [MessageId]           INT           NOT NULL REFERENCES [dbo].[Messages]([MessageId]),
    [FileName]            NVARCHAR(260) NOT NULL,
    [ContentType]         NVARCHAR(100) NOT NULL,
    [FileSizeBytes]       INT           NOT NULL,
    [StoragePath]         NVARCHAR(400) NOT NULL
);
GO

CREATE TABLE [dbo].[ChatbotFaqs] (
    [ChatbotFaqId]       INT            IDENTITY(1,1) PRIMARY KEY,
    [MenuCategory]       NVARCHAR(60)   NOT NULL,               -- e.g. "Fees", "Enrollment", "Schedule"
    [Question]           NVARCHAR(300)  NOT NULL,
    [Answer]             NVARCHAR(1000) NOT NULL,
    [Language]           NVARCHAR(10)   NOT NULL DEFAULT ('EN'), -- EN | BM etc. (bilingual)
    [DisplayOrder]        INT           NOT NULL DEFAULT (0),
    [IsActive]            BIT           NOT NULL DEFAULT (1)
);
GO

CREATE TABLE [dbo].[ChatbotKeywords] (
    [ChatbotKeywordId]   INT            IDENTITY(1,1) PRIMARY KEY,
    [ChatbotFaqId]       INT            NOT NULL REFERENCES [dbo].[ChatbotFaqs]([ChatbotFaqId]),
    [Keyword]            NVARCHAR(60)   NOT NULL,
    [MatchMode]          NVARCHAR(10)   NOT NULL DEFAULT ('Any') -- 'Any' (one-match) | 'All' (all-match)
);
GO
CREATE INDEX [IX_ChatbotKeywords_Keyword] ON [dbo].[ChatbotKeywords]([Keyword]);
GO

CREATE TABLE [dbo].[ChatbotLogs] (
    [ChatbotLogId]       INT            IDENTITY(1,1) PRIMARY KEY,
    [UserId]             NVARCHAR(450)  NOT NULL REFERENCES [dbo].[AspNetUsers]([Id]),
    [QuestionText]       NVARCHAR(500)  NOT NULL,
    [MatchedChatbotFaqId] INT           NULL REFERENCES [dbo].[ChatbotFaqs]([ChatbotFaqId]),
    [WasEscalated]       BIT            NOT NULL DEFAULT (0),
    [AskedAt]            DATETIME2      NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE [dbo].[Announcements] (
    [AnnouncementId]     INT            IDENTITY(1,1) PRIMARY KEY,
    [Title]              NVARCHAR(150)  NOT NULL,
    [Body]                NVARCHAR(2000) NOT NULL,
    [PostedByUserId]     NVARCHAR(450)  NOT NULL REFERENCES [dbo].[AspNetUsers]([Id]),
    [PostedAt]           DATETIME2      NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE [dbo].[AnnouncementRecipients] (
    [AnnouncementRecipientId] INT       IDENTITY(1,1) PRIMARY KEY,
    [AnnouncementId]     INT            NOT NULL REFERENCES [dbo].[Announcements]([AnnouncementId]),
    [UserId]             NVARCHAR(450)  NOT NULL REFERENCES [dbo].[AspNetUsers]([Id]),
    [DeliveredAt]        DATETIME2      NULL,
    [ReadAt]             DATETIME2      NULL
);
GO
CREATE UNIQUE INDEX [IX_AnnouncementRecipients_Unique] ON [dbo].[AnnouncementRecipients]([AnnouncementId],[UserId]);
GO

/* ============================================================================
   5. SYSTEM SECURITY MODULE
   ============================================================================ */

CREATE TABLE [dbo].[ActivityLogs] (
    [ActivityLogId]      BIGINT         IDENTITY(1,1) PRIMARY KEY,
    [UserId]             NVARCHAR(450)  NULL REFERENCES [dbo].[AspNetUsers]([Id]),  -- NULL for unauthenticated/system actions
    [Action]             NVARCHAR(100)  NOT NULL,               -- e.g. "Enrollment.Approve", "Payment.Confirm"
    [EntityName]         NVARCHAR(60)   NULL,
    [EntityId]           NVARCHAR(60)   NULL,
    [IpAddress]          NVARCHAR(45)   NULL,
    [Details]            NVARCHAR(1000) NULL,
    [IsSuccess]          BIT            NOT NULL DEFAULT (1),
    [OccurredAt]          DATETIME2     NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO
CREATE INDEX [IX_ActivityLogs_OccurredAt] ON [dbo].[ActivityLogs]([OccurredAt]);
GO

CREATE TABLE [dbo].[ErrorLogs] (
    [ErrorLogId]         BIGINT         IDENTITY(1,1) PRIMARY KEY,
    [TraceId]            NVARCHAR(60)   NOT NULL,               -- shown to the user; correlates to the full server-side log
    [Path]               NVARCHAR(300)  NULL,
    [ExceptionType]      NVARCHAR(200)  NULL,
    [Message]            NVARCHAR(2000) NULL,
    [StackTrace]         NVARCHAR(MAX)  NULL,
    [OccurredAt]         DATETIME2      NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO

PRINT 'Secure Smart Tuition System schema created successfully.';
GO
