BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [DocumentSequences] (
        [Id] int NOT NULL IDENTITY,
        [DocumentType] nvarchar(20) NOT NULL,
        [FinancialYear] int NOT NULL,
        [NextNumber] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_DocumentSequences] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [EmailLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [CreatedAtUtc] datetime2 NOT NULL,
        [OrganizationId] int NULL,
        [To] nvarchar(200) NOT NULL,
        [Subject] nvarchar(300) NOT NULL,
        [Category] nvarchar(50) NOT NULL,
        [Succeeded] bit NOT NULL,
        [Error] nvarchar(1000) NULL,
        CONSTRAINT [PK_EmailLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [Invoices] (
        [Id] int NOT NULL IDENTITY,
        [Number] nvarchar(16) NOT NULL,
        [OrganizationId] int NOT NULL,
        [Purpose] nvarchar(40) NOT NULL,
        [PlanId] int NULL,
        [BillingCycle] nvarchar(40) NULL,
        [Seats] int NULL,
        [InvoiceDate] date NOT NULL,
        [DueDate] date NOT NULL,
        [PeriodStart] date NULL,
        [PeriodEnd] date NULL,
        [Status] nvarchar(40) NOT NULL,
        [Fulfilled] bit NOT NULL,
        [BillToName] nvarchar(200) NOT NULL,
        [BillToGstin] nvarchar(15) NULL,
        [BillToAddress] nvarchar(500) NULL,
        [BillToState] nvarchar(100) NULL,
        [BillToStateCode] nvarchar(2) NULL,
        [SupplierStateCode] nvarchar(2) NOT NULL,
        [PlaceOfSupply] nvarchar(120) NOT NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [CgstRate] decimal(5,2) NOT NULL,
        [CgstAmount] decimal(18,2) NOT NULL,
        [SgstRate] decimal(5,2) NOT NULL,
        [SgstAmount] decimal(18,2) NOT NULL,
        [IgstRate] decimal(5,2) NOT NULL,
        [IgstAmount] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [AmountPaid] decimal(18,2) NOT NULL,
        [AmountCredited] decimal(18,2) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CancelReason] nvarchar(500) NULL,
        [LastReminderAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Invoices_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoices_SubscriptionPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [SubscriptionPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [PlatformSettings] (
        [Id] int NOT NULL IDENTITY,
        [LegalName] nvarchar(200) NOT NULL,
        [Gstin] nvarchar(15) NULL,
        [Pan] nvarchar(10) NULL,
        [AddressLine] nvarchar(300) NULL,
        [City] nvarchar(100) NULL,
        [State] nvarchar(100) NOT NULL,
        [PostalCode] nvarchar(10) NULL,
        [BillingEmail] nvarchar(200) NULL,
        [Phone] nvarchar(20) NULL,
        [InvoicePrefix] nvarchar(6) NOT NULL,
        [CreditNotePrefix] nvarchar(6) NOT NULL,
        [SacCode] nvarchar(8) NOT NULL,
        [GstRate] decimal(5,2) NOT NULL,
        [InvoiceDueDays] int NOT NULL,
        [RenewalInvoiceDaysBefore] int NOT NULL,
        [ReminderSchedule] nvarchar(60) NOT NULL,
        [BankDetails] nvarchar(500) NULL,
        [RazorpayEnabled] bit NOT NULL,
        [RazorpayKeyId] nvarchar(100) NULL,
        [RazorpayKeySecretProtected] nvarchar(1000) NULL,
        [RazorpayWebhookSecretProtected] nvarchar(1000) NULL,
        [SmtpEnabled] bit NOT NULL,
        [SmtpHost] nvarchar(200) NULL,
        [SmtpPort] int NOT NULL,
        [SmtpUseSsl] bit NOT NULL,
        [SmtpUsername] nvarchar(200) NULL,
        [SmtpPasswordProtected] nvarchar(1000) NULL,
        [SmtpFromEmail] nvarchar(200) NULL,
        [SmtpFromName] nvarchar(100) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PlatformSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [InvoiceLines] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [Description] nvarchar(300) NOT NULL,
        [SacCode] nvarchar(8) NOT NULL,
        [Quantity] decimal(12,2) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_InvoiceLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvoiceLines_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Method] nvarchar(40) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Reference] nvarchar(100) NULL,
        [PaidAtUtc] datetime2 NULL,
        [GatewayOrderId] nvarchar(60) NULL,
        [GatewayPaymentId] nvarchar(60) NULL,
        [FailureReason] nvarchar(300) NULL,
        [RefundedAmount] decimal(18,2) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [CreditNotes] (
        [Id] int NOT NULL IDENTITY,
        [Number] nvarchar(16) NOT NULL,
        [InvoiceId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        [Date] date NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [TaxableAmount] decimal(18,2) NOT NULL,
        [CgstAmount] decimal(18,2) NOT NULL,
        [SgstAmount] decimal(18,2) NOT NULL,
        [IgstAmount] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [RefundAmount] decimal(18,2) NOT NULL,
        [RefundPaymentId] int NULL,
        [GatewayRefundId] nvarchar(60) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_CreditNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreditNotes_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CreditNotes_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CreditNotes_Payments_RefundPaymentId] FOREIGN KEY ([RefundPaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_CreditNotes_InvoiceId] ON [CreditNotes] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CreditNotes_Number] ON [CreditNotes] ([Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_CreditNotes_OrganizationId] ON [CreditNotes] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_CreditNotes_RefundPaymentId] ON [CreditNotes] ([RefundPaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DocumentSequences_DocumentType_FinancialYear] ON [DocumentSequences] ([DocumentType], [FinancialYear]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_EmailLogs_CreatedAtUtc] ON [EmailLogs] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_InvoiceLines_InvoiceId] ON [InvoiceLines] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Invoices_Number] ON [Invoices] ([Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Invoices_OrganizationId_Status] ON [Invoices] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Invoices_PlanId] ON [Invoices] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Invoices_Status_DueDate] ON [Invoices] ([Status], [DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Payments_GatewayOrderId] ON [Payments] ([GatewayOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_GatewayPaymentId] ON [Payments] ([GatewayPaymentId]) WHERE [GatewayPaymentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Payments_InvoiceId] ON [Payments] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Payments_OrganizationId] ON [Payments] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Payments_Status_PaidAtUtc] ON [Payments] ([Status], [PaidAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112035629_Billing', N'9.0.10');
END;

COMMIT;
GO

