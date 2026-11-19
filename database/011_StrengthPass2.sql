BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [GoogleClientId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [GoogleClientSecretProtected] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [MicrosoftClientId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [MicrosoftClientSecretProtected] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [Msg91AuthKeyProtected] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [Msg91OtpVariable] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [Msg91TemplateId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [SmsEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [OrganizationSettings] ADD [MobileOtpLogin] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [OrganizationSettings] ADD [SelfieRequired] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [OrganizationSettings] ADD [SelfieRetentionDays] int NOT NULL DEFAULT 90;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [OrganizationSettings] ADD [SsoDomains] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [OrganizationSettings] ADD [SsoGoogle] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [OrganizationSettings] ADD [SsoMicrosoft] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD [SelfieStorageKey] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE TABLE [LoginOtps] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [CodeHash] nvarchar(64) NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [Attempts] int NOT NULL,
        [ConsumedAtUtc] datetime2 NULL,
        [IpAddress] nvarchar(64) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_LoginOtps] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LoginOtps_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LoginOtps_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE TABLE [NotificationRules] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Event] nvarchar(30) NOT NULL,
        [Days] int NOT NULL,
        [NotifyEmployee] bit NOT NULL,
        [NotifyManager] bit NOT NULL,
        [NotifyHr] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_NotificationRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationRules_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE TABLE [OnboardingPlans] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [TemplateName] nvarchar(100) NOT NULL,
        [StartedOn] date NOT NULL,
        [BuddyEmployeeId] int NULL,
        [Status] nvarchar(20) NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_OnboardingPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OnboardingPlans_Employees_BuddyEmployeeId] FOREIGN KEY ([BuddyEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OnboardingPlans_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OnboardingPlans_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE TABLE [OnboardingTemplates] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_OnboardingTemplates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OnboardingTemplates_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE TABLE [ReportSchedules] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Kind] nvarchar(30) NOT NULL,
        [Frequency] nvarchar(20) NOT NULL,
        [DepartmentId] int NULL,
        [BranchId] int NULL,
        [RecipientUserIds] nvarchar(500) NOT NULL,
        [NextRunOn] date NOT NULL,
        [LastSentAtUtc] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ReportSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReportSchedules_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReportSchedules_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReportSchedules_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE TABLE [SignatureRequests] (
        [Id] int NOT NULL IDENTITY,
        [GeneratedLetterId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [DocumentHash] nvarchar(64) NOT NULL,
        [RequestedBy] nvarchar(150) NOT NULL,
        [Message] nvarchar(500) NULL,
        [OtpHash] nvarchar(64) NULL,
        [OtpExpiresAtUtc] datetime2 NULL,
        [OtpSentAtUtc] datetime2 NULL,
        [OtpSends] int NOT NULL,
        [OtpAttempts] int NOT NULL,
        [SignedName] nvarchar(150) NULL,
        [SignedAtUtc] datetime2 NULL,
        [SignedIp] nvarchar(64) NULL,
        [SignedUserAgent] nvarchar(300) NULL,
        [DeclineReason] nvarchar(500) NULL,
        [SignedDocumentId] int NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SignatureRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SignatureRequests_EmployeeDocuments_SignedDocumentId] FOREIGN KEY ([SignedDocumentId]) REFERENCES [EmployeeDocuments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SignatureRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SignatureRequests_GeneratedLetters_GeneratedLetterId] FOREIGN KEY ([GeneratedLetterId]) REFERENCES [GeneratedLetters] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SignatureRequests_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE TABLE [TravelRequests] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Purpose] nvarchar(300) NOT NULL,
        [FromCity] nvarchar(100) NOT NULL,
        [ToCity] nvarchar(100) NOT NULL,
        [DepartOn] date NOT NULL,
        [ReturnOn] date NULL,
        [Mode] nvarchar(20) NOT NULL,
        [NeedsAccommodation] bit NOT NULL,
        [EstimatedCost] decimal(18,2) NULL,
        [AdvanceRequested] decimal(18,2) NULL,
        [Notes] nvarchar(1000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [ApproverUserId] int NULL,
        [DecidedBy] nvarchar(150) NULL,
        [DecidedAtUtc] datetime2 NULL,
        [DecisionNote] nvarchar(500) NULL,
        [BookingDetails] nvarchar(2000) NULL,
        [BookedCost] decimal(18,2) NULL,
        [TicketStorageKey] nvarchar(300) NULL,
        [TicketFileName] nvarchar(200) NULL,
        [TicketContentType] nvarchar(100) NULL,
        [BookedBy] nvarchar(150) NULL,
        [BookedAtUtc] datetime2 NULL,
        [AdvancePaid] decimal(18,2) NULL,
        [AdvancePaidOn] date NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_TravelRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TravelRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TravelRequests_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TravelRequests_Users_ApproverUserId] FOREIGN KEY ([ApproverUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE TABLE [NotificationRuleLogs] (
        [Id] int NOT NULL IDENTITY,
        [RuleId] int NOT NULL,
        [SubjectKey] nvarchar(100) NOT NULL,
        [SentOn] date NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_NotificationRuleLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationRuleLogs_NotificationRules_RuleId] FOREIGN KEY ([RuleId]) REFERENCES [NotificationRules] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_NotificationRuleLogs_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE TABLE [OnboardingTasks] (
        [Id] int NOT NULL IDENTITY,
        [PlanId] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Assignee] nvarchar(20) NOT NULL,
        [DueOn] date NOT NULL,
        [SortOrder] int NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CompletedBy] nvarchar(150) NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_OnboardingTasks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OnboardingTasks_OnboardingPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [OnboardingPlans] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OnboardingTasks_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE TABLE [OnboardingTemplateTasks] (
        [Id] int NOT NULL IDENTITY,
        [TemplateId] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Assignee] nvarchar(20) NOT NULL,
        [DueOffsetDays] int NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_OnboardingTemplateTasks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OnboardingTemplateTasks_OnboardingTemplates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [OnboardingTemplates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OnboardingTemplateTasks_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_LoginOtps_OrganizationId] ON [LoginOtps] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_LoginOtps_UserId_CreatedAtUtc] ON [LoginOtps] ([UserId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_NotificationRuleLogs_OrganizationId] ON [NotificationRuleLogs] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationRuleLogs_RuleId_SubjectKey] ON [NotificationRuleLogs] ([RuleId], [SubjectKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_NotificationRules_OrganizationId] ON [NotificationRules] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_OnboardingPlans_BuddyEmployeeId] ON [OnboardingPlans] ([BuddyEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_OnboardingPlans_EmployeeId] ON [OnboardingPlans] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_OnboardingPlans_OrganizationId_EmployeeId] ON [OnboardingPlans] ([OrganizationId], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_OnboardingTasks_OrganizationId] ON [OnboardingTasks] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_OnboardingTasks_PlanId_CompletedAtUtc] ON [OnboardingTasks] ([PlanId], [CompletedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OnboardingTemplates_OrganizationId_Name] ON [OnboardingTemplates] ([OrganizationId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_OnboardingTemplateTasks_OrganizationId] ON [OnboardingTemplateTasks] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_OnboardingTemplateTasks_TemplateId] ON [OnboardingTemplateTasks] ([TemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_ReportSchedules_BranchId] ON [ReportSchedules] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_ReportSchedules_DepartmentId] ON [ReportSchedules] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_ReportSchedules_OrganizationId_NextRunOn] ON [ReportSchedules] ([OrganizationId], [NextRunOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_SignatureRequests_EmployeeId_Status] ON [SignatureRequests] ([EmployeeId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SignatureRequests_GeneratedLetterId] ON [SignatureRequests] ([GeneratedLetterId]) WHERE [IsDeleted] = 0 AND [Status] IN (''Pending'',''Signed'')');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_SignatureRequests_OrganizationId] ON [SignatureRequests] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_SignatureRequests_SignedDocumentId] ON [SignatureRequests] ([SignedDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_TravelRequests_ApproverUserId] ON [TravelRequests] ([ApproverUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_TravelRequests_EmployeeId_DepartOn] ON [TravelRequests] ([EmployeeId], [DepartOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    CREATE INDEX [IX_TravelRequests_OrganizationId_Status] ON [TravelRequests] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112141031_StrengthPass2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112141031_StrengthPass2', N'9.0.10');
END;

COMMIT;
GO

