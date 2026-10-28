BEGIN TRANSACTION;
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

COMMIT;
GO

