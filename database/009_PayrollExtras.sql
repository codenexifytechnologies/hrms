BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE TABLE [ExpenseCategories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(80) NOT NULL,
        [MaxPerItem] decimal(18,2) NULL,
        [ReceiptRequired] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ExpenseCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpenseCategories_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE TABLE [PayrollAdjustments] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [Category] nvarchar(40) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [IsTaxable] bit NOT NULL,
        [Notes] nvarchar(300) NULL,
        [Source] nvarchar(40) NOT NULL,
        [SourceId] int NULL,
        [CoversFrom] date NULL,
        [CoversTo] date NULL,
        [SettledRunId] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PayrollAdjustments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayrollAdjustments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayrollAdjustments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayrollAdjustments_PayrollRuns_SettledRunId] FOREIGN KEY ([SettledRunId]) REFERENCES [PayrollRuns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE TABLE [ExpenseClaims] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [ManagerUserId] int NULL,
        [ManagerDecisionBy] nvarchar(150) NULL,
        [ManagerDecidedAtUtc] datetime2 NULL,
        [DecidedBy] nvarchar(150) NULL,
        [DecidedAtUtc] datetime2 NULL,
        [DecisionNotes] nvarchar(500) NULL,
        [Payout] nvarchar(40) NULL,
        [AdjustmentId] int NULL,
        [PaidOn] date NULL,
        [PaymentReference] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ExpenseClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpenseClaims_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExpenseClaims_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExpenseClaims_PayrollAdjustments_AdjustmentId] FOREIGN KEY ([AdjustmentId]) REFERENCES [PayrollAdjustments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExpenseClaims_Users_ManagerUserId] FOREIGN KEY ([ManagerUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE TABLE [ExpenseItems] (
        [Id] int NOT NULL IDENTITY,
        [ClaimId] int NOT NULL,
        [CategoryId] int NOT NULL,
        [Date] date NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ReceiptKey] nvarchar(300) NULL,
        [ReceiptFileName] nvarchar(200) NULL,
        [ReceiptContentType] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ExpenseItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpenseItems_ExpenseCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [ExpenseCategories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExpenseItems_ExpenseClaims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [ExpenseClaims] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExpenseItems_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ExpenseCategories_OrganizationId_Name] ON [ExpenseCategories] ([OrganizationId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseClaims_AdjustmentId] ON [ExpenseClaims] ([AdjustmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseClaims_EmployeeId] ON [ExpenseClaims] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseClaims_ManagerUserId_Status] ON [ExpenseClaims] ([ManagerUserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseClaims_OrganizationId_Status] ON [ExpenseClaims] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseItems_CategoryId] ON [ExpenseItems] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseItems_ClaimId] ON [ExpenseItems] ([ClaimId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseItems_OrganizationId] ON [ExpenseItems] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_PayrollAdjustments_EmployeeId_SettledRunId] ON [PayrollAdjustments] ([EmployeeId], [SettledRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_PayrollAdjustments_OrganizationId_Year_Month] ON [PayrollAdjustments] ([OrganizationId], [Year], [Month]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_PayrollAdjustments_SettledRunId] ON [PayrollAdjustments] ([SettledRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112054051_PayrollExtras', N'9.0.10');
END;

COMMIT;
GO

