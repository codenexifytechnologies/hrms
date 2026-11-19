BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [MaintenanceEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [MaintenanceMessage] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [MaintenanceUntilUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [SupportEmail] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE TABLE [PlatformAnnouncements] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(150) NOT NULL,
        [Body] nvarchar(1000) NOT NULL,
        [Severity] nvarchar(40) NOT NULL,
        [PlanId] int NULL,
        [StartsAtUtc] datetime2 NOT NULL,
        [EndsAtUtc] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [Dismissible] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PlatformAnnouncements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlatformAnnouncements_SubscriptionPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [SubscriptionPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE TABLE [SupportTickets] (
        [Id] int NOT NULL IDENTITY,
        [Subject] nvarchar(200) NOT NULL,
        [Category] nvarchar(40) NOT NULL,
        [Priority] nvarchar(40) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [RaisedByUserId] int NULL,
        [RaisedByName] nvarchar(150) NOT NULL,
        [RaisedByEmail] nvarchar(200) NULL,
        [AssignedToPlatformUserId] int NULL,
        [AssignedToName] nvarchar(150) NULL,
        [LastActivityAtUtc] datetime2 NOT NULL,
        [FirstResponseAtUtc] datetime2 NULL,
        [ResolvedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SupportTickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupportTickets_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupportTickets_PlatformUsers_AssignedToPlatformUserId] FOREIGN KEY ([AssignedToPlatformUserId]) REFERENCES [PlatformUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE TABLE [SupportAccessGrants] (
        [Id] int NOT NULL IDENTITY,
        [GrantedByUserId] int NOT NULL,
        [GrantedByName] nvarchar(150) NOT NULL,
        [Reason] nvarchar(300) NOT NULL,
        [TicketId] int NULL,
        [IncludePayroll] bit NOT NULL,
        [GrantedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [RevokedAtUtc] datetime2 NULL,
        [RevokedBy] nvarchar(150) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SupportAccessGrants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupportAccessGrants_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupportAccessGrants_SupportTickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [SupportTickets] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE TABLE [TicketMessages] (
        [Id] int NOT NULL IDENTITY,
        [TicketId] int NOT NULL,
        [Author] nvarchar(40) NOT NULL,
        [AuthorUserId] int NULL,
        [AuthorName] nvarchar(150) NOT NULL,
        [Body] nvarchar(4000) NOT NULL,
        [IsInternal] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_TicketMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketMessages_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TicketMessages_SupportTickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [SupportTickets] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE TABLE [SupportSessions] (
        [Id] int NOT NULL IDENTITY,
        [GrantId] int NOT NULL,
        [PlatformUserId] int NOT NULL,
        [PlatformUserName] nvarchar(150) NOT NULL,
        [StartedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [EndedAtUtc] datetime2 NULL,
        [PageViews] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SupportSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupportSessions_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupportSessions_PlatformUsers_PlatformUserId] FOREIGN KEY ([PlatformUserId]) REFERENCES [PlatformUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupportSessions_SupportAccessGrants_GrantId] FOREIGN KEY ([GrantId]) REFERENCES [SupportAccessGrants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE INDEX [IX_PlatformAnnouncements_IsActive_StartsAtUtc] ON [PlatformAnnouncements] ([IsActive], [StartsAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE INDEX [IX_PlatformAnnouncements_PlanId] ON [PlatformAnnouncements] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE INDEX [IX_SupportAccessGrants_OrganizationId_ExpiresAtUtc] ON [SupportAccessGrants] ([OrganizationId], [ExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE INDEX [IX_SupportAccessGrants_TicketId] ON [SupportAccessGrants] ([TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE INDEX [IX_SupportSessions_GrantId] ON [SupportSessions] ([GrantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE INDEX [IX_SupportSessions_OrganizationId] ON [SupportSessions] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE INDEX [IX_SupportSessions_PlatformUserId] ON [SupportSessions] ([PlatformUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE INDEX [IX_SupportTickets_AssignedToPlatformUserId] ON [SupportTickets] ([AssignedToPlatformUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE INDEX [IX_SupportTickets_OrganizationId_Status] ON [SupportTickets] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE INDEX [IX_SupportTickets_Status_LastActivityAtUtc] ON [SupportTickets] ([Status], [LastActivityAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE INDEX [IX_TicketMessages_OrganizationId] ON [TicketMessages] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    CREATE INDEX [IX_TicketMessages_TicketId] ON [TicketMessages] ([TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112042136_Support'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112042136_Support', N'9.0.10');
END;

COMMIT;
GO

