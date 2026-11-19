BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE TABLE [FeedbackRequests] (
        [Id] int NOT NULL IDENTITY,
        [RequestedByEmployeeId] int NOT NULL,
        [AboutEmployeeId] int NOT NULL,
        [Question] nvarchar(500) NOT NULL,
        [DueOn] date NULL,
        [AtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_FeedbackRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FeedbackRequests_Employees_AboutEmployeeId] FOREIGN KEY ([AboutEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FeedbackRequests_Employees_RequestedByEmployeeId] FOREIGN KEY ([RequestedByEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FeedbackRequests_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE TABLE [OneOnOnes] (
        [Id] int NOT NULL IDENTITY,
        [ManagerEmployeeId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [OnDate] date NOT NULL,
        [StartsAt] time NOT NULL,
        [DurationMinutes] int NOT NULL,
        [Repeat] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [SharedNotes] nvarchar(max) NULL,
        [ManagerPrivateNotes] nvarchar(4000) NULL,
        [EmployeePrivateNotes] nvarchar(4000) NULL,
        [CompletedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_OneOnOnes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OneOnOnes_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OneOnOnes_Employees_ManagerEmployeeId] FOREIGN KEY ([ManagerEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OneOnOnes_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE TABLE [FeedbackNotes] (
        [Id] int NOT NULL IDENTITY,
        [AuthorEmployeeId] int NOT NULL,
        [AboutEmployeeId] int NOT NULL,
        [Tone] nvarchar(20) NOT NULL,
        [Visibility] nvarchar(20) NOT NULL,
        [Body] nvarchar(2000) NOT NULL,
        [FeedbackRequestId] int NULL,
        [AtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_FeedbackNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FeedbackNotes_Employees_AboutEmployeeId] FOREIGN KEY ([AboutEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FeedbackNotes_Employees_AuthorEmployeeId] FOREIGN KEY ([AuthorEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FeedbackNotes_FeedbackRequests_FeedbackRequestId] FOREIGN KEY ([FeedbackRequestId]) REFERENCES [FeedbackRequests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FeedbackNotes_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE TABLE [FeedbackRequestRecipients] (
        [Id] int NOT NULL IDENTITY,
        [FeedbackRequestId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [RespondedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_FeedbackRequestRecipients] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FeedbackRequestRecipients_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FeedbackRequestRecipients_FeedbackRequests_FeedbackRequestId] FOREIGN KEY ([FeedbackRequestId]) REFERENCES [FeedbackRequests] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_FeedbackRequestRecipients_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE TABLE [MeetingItems] (
        [Id] int NOT NULL IDENTITY,
        [OneOnOneId] int NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [Text] nvarchar(500) NOT NULL,
        [AddedByEmployeeId] int NOT NULL,
        [OwnerEmployeeId] int NULL,
        [DueOn] date NULL,
        [IsDone] bit NOT NULL,
        [DoneAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_MeetingItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MeetingItems_Employees_OwnerEmployeeId] FOREIGN KEY ([OwnerEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MeetingItems_OneOnOnes_OneOnOneId] FOREIGN KEY ([OneOnOneId]) REFERENCES [OneOnOnes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MeetingItems_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_FeedbackNotes_AboutEmployeeId] ON [FeedbackNotes] ([AboutEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_FeedbackNotes_AuthorEmployeeId] ON [FeedbackNotes] ([AuthorEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_FeedbackNotes_FeedbackRequestId] ON [FeedbackNotes] ([FeedbackRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_FeedbackNotes_OrganizationId_AboutEmployeeId_AtUtc] ON [FeedbackNotes] ([OrganizationId], [AboutEmployeeId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_FeedbackNotes_OrganizationId_AuthorEmployeeId_AtUtc] ON [FeedbackNotes] ([OrganizationId], [AuthorEmployeeId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_FeedbackRequestRecipients_EmployeeId] ON [FeedbackRequestRecipients] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_FeedbackRequestRecipients_FeedbackRequestId_EmployeeId] ON [FeedbackRequestRecipients] ([FeedbackRequestId], [EmployeeId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_FeedbackRequestRecipients_OrganizationId_EmployeeId_Status] ON [FeedbackRequestRecipients] ([OrganizationId], [EmployeeId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_FeedbackRequests_AboutEmployeeId] ON [FeedbackRequests] ([AboutEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_FeedbackRequests_OrganizationId] ON [FeedbackRequests] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_FeedbackRequests_RequestedByEmployeeId] ON [FeedbackRequests] ([RequestedByEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_MeetingItems_OneOnOneId] ON [MeetingItems] ([OneOnOneId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_MeetingItems_OrganizationId] ON [MeetingItems] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_MeetingItems_OwnerEmployeeId] ON [MeetingItems] ([OwnerEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_OneOnOnes_EmployeeId] ON [OneOnOnes] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_OneOnOnes_ManagerEmployeeId] ON [OneOnOnes] ([ManagerEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_OneOnOnes_OrganizationId_EmployeeId_OnDate] ON [OneOnOnes] ([OrganizationId], [EmployeeId], [OnDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    CREATE INDEX [IX_OneOnOnes_OrganizationId_ManagerEmployeeId_OnDate] ON [OneOnOnes] ([OrganizationId], [ManagerEmployeeId], [OnDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114075836_FeedbackAndOneOnOnes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261114075836_FeedbackAndOneOnOnes', N'9.0.10');
END;

COMMIT;
GO

