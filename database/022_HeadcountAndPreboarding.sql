BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE TABLE [HeadcountPlans] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [StartsOn] date NOT NULL,
        [EndsOn] date NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [ApprovedByUserId] int NULL,
        [ApprovedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_HeadcountPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HeadcountPlans_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HeadcountPlans_Users_ApprovedByUserId] FOREIGN KEY ([ApprovedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE TABLE [PreboardingInvites] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [TokenHash] nvarchar(64) NOT NULL,
        [SentAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [SentByUserId] int NULL,
        [FirstOpenedAtUtc] datetime2 NULL,
        [LastOpenedAtUtc] datetime2 NULL,
        [PersonalSavedAtUtc] datetime2 NULL,
        [BankSavedAtUtc] datetime2 NULL,
        [SubmittedAtUtc] datetime2 NULL,
        [RevokedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PreboardingInvites] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PreboardingInvites_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PreboardingInvites_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PreboardingInvites_Users_SentByUserId] FOREIGN KEY ([SentByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE TABLE [PreboardingItems] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(150) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Kind] nvarchar(20) NOT NULL,
        [DocumentCategory] nvarchar(20) NOT NULL,
        [IsRequired] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PreboardingItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PreboardingItems_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE TABLE [PreboardingSettings] (
        [Id] int NOT NULL IDENTITY,
        [WelcomeMessage] nvarchar(4000) NULL,
        [FirstDayInfo] nvarchar(4000) NULL,
        [ContactName] nvarchar(150) NULL,
        [ContactEmail] nvarchar(200) NULL,
        [ContactPhone] nvarchar(20) NULL,
        [AskBankDetails] bit NOT NULL,
        [AskPan] bit NOT NULL,
        [LinkDaysAfterJoining] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PreboardingSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PreboardingSettings_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE TABLE [HeadcountPositions] (
        [Id] int NOT NULL IDENTITY,
        [PlanId] int NOT NULL,
        [DepartmentId] int NOT NULL,
        [BranchId] int NULL,
        [DesignationId] int NULL,
        [Title] nvarchar(150) NOT NULL,
        [Openings] int NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [TargetStartOn] date NOT NULL,
        [AnnualBudgetPerHire] decimal(18,2) NOT NULL,
        [Priority] nvarchar(10) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_HeadcountPositions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HeadcountPositions_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HeadcountPositions_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HeadcountPositions_Designations_DesignationId] FOREIGN KEY ([DesignationId]) REFERENCES [Designations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HeadcountPositions_HeadcountPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [HeadcountPlans] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_HeadcountPositions_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE TABLE [PreboardingResponses] (
        [Id] int NOT NULL IDENTITY,
        [InviteId] int NOT NULL,
        [ItemId] int NOT NULL,
        [EmployeeDocumentId] int NULL,
        [CompletedAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PreboardingResponses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PreboardingResponses_EmployeeDocuments_EmployeeDocumentId] FOREIGN KEY ([EmployeeDocumentId]) REFERENCES [EmployeeDocuments] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_PreboardingResponses_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PreboardingResponses_PreboardingInvites_InviteId] FOREIGN KEY ([InviteId]) REFERENCES [PreboardingInvites] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PreboardingResponses_PreboardingItems_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [PreboardingItems] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE TABLE [HeadcountHires] (
        [Id] int NOT NULL IDENTITY,
        [PositionId] int NOT NULL,
        [PlanId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_HeadcountHires] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HeadcountHires_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HeadcountHires_HeadcountPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [HeadcountPlans] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HeadcountHires_HeadcountPositions_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [HeadcountPositions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_HeadcountHires_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_HeadcountHires_EmployeeId] ON [HeadcountHires] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_HeadcountHires_OrganizationId] ON [HeadcountHires] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_HeadcountHires_PlanId_EmployeeId] ON [HeadcountHires] ([PlanId], [EmployeeId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_HeadcountHires_PositionId] ON [HeadcountHires] ([PositionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_HeadcountPlans_ApprovedByUserId] ON [HeadcountPlans] ([ApprovedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_HeadcountPlans_OrganizationId] ON [HeadcountPlans] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_HeadcountPositions_BranchId] ON [HeadcountPositions] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_HeadcountPositions_DepartmentId] ON [HeadcountPositions] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_HeadcountPositions_DesignationId] ON [HeadcountPositions] ([DesignationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_HeadcountPositions_OrganizationId] ON [HeadcountPositions] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_HeadcountPositions_PlanId] ON [HeadcountPositions] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_PreboardingInvites_EmployeeId] ON [PreboardingInvites] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_PreboardingInvites_OrganizationId_EmployeeId] ON [PreboardingInvites] ([OrganizationId], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_PreboardingInvites_SentByUserId] ON [PreboardingInvites] ([SentByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PreboardingInvites_TokenHash] ON [PreboardingInvites] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_PreboardingItems_OrganizationId] ON [PreboardingItems] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_PreboardingResponses_EmployeeDocumentId] ON [PreboardingResponses] ([EmployeeDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PreboardingResponses_InviteId_ItemId] ON [PreboardingResponses] ([InviteId], [ItemId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_PreboardingResponses_ItemId] ON [PreboardingResponses] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    CREATE INDEX [IX_PreboardingResponses_OrganizationId] ON [PreboardingResponses] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PreboardingSettings_OrganizationId] ON [PreboardingSettings] ([OrganizationId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261114093445_HeadcountAndPreboarding', N'9.0.10');
END;

COMMIT;
GO

