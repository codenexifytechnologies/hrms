BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    ALTER TABLE [Users] ADD [MuteEmailNotifications] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    ALTER TABLE [Notifications] ADD [EmailPending] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    ALTER TABLE [Employees] ADD [HideBirthday] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE TABLE [ProfileChangeRequests] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [RequestedByUserId] int NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ChangesJson] nvarchar(4000) NOT NULL,
        [PreviousJson] nvarchar(4000) NOT NULL,
        [Note] nvarchar(500) NULL,
        [DecidedBy] nvarchar(150) NULL,
        [DecidedAtUtc] datetime2 NULL,
        [DecisionNotes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ProfileChangeRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProfileChangeRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProfileChangeRequests_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE TABLE [WallPosts] (
        [Id] int NOT NULL IDENTITY,
        [Kind] nvarchar(40) NOT NULL,
        [AuthorUserId] int NOT NULL,
        [AuthorName] nvarchar(150) NOT NULL,
        [Title] nvarchar(150) NULL,
        [Body] nvarchar(2000) NOT NULL,
        [Badge] nvarchar(60) NULL,
        [IsPinned] bit NOT NULL,
        [PinnedUntilUtc] datetime2 NULL,
        [PollClosesAtUtc] datetime2 NULL,
        [CommentsEnabled] bit NOT NULL,
        [LikeCount] int NOT NULL,
        [CommentCount] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_WallPosts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WallPosts_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WallPosts_Users_AuthorUserId] FOREIGN KEY ([AuthorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE TABLE [PollOptions] (
        [Id] int NOT NULL IDENTITY,
        [PostId] int NOT NULL,
        [Text] nvarchar(150) NOT NULL,
        [SortOrder] int NOT NULL,
        [VoteCount] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PollOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PollOptions_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PollOptions_WallPosts_PostId] FOREIGN KEY ([PostId]) REFERENCES [WallPosts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE TABLE [WallComments] (
        [Id] int NOT NULL IDENTITY,
        [PostId] int NOT NULL,
        [AuthorUserId] int NOT NULL,
        [AuthorName] nvarchar(150) NOT NULL,
        [Body] nvarchar(1000) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_WallComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WallComments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WallComments_Users_AuthorUserId] FOREIGN KEY ([AuthorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WallComments_WallPosts_PostId] FOREIGN KEY ([PostId]) REFERENCES [WallPosts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE TABLE [WallPostRecipients] (
        [Id] int NOT NULL IDENTITY,
        [PostId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [EmployeeName] nvarchar(150) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_WallPostRecipients] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WallPostRecipients_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WallPostRecipients_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WallPostRecipients_WallPosts_PostId] FOREIGN KEY ([PostId]) REFERENCES [WallPosts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE TABLE [WallReactions] (
        [Id] int NOT NULL IDENTITY,
        [PostId] int NOT NULL,
        [UserId] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_WallReactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WallReactions_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WallReactions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WallReactions_WallPosts_PostId] FOREIGN KEY ([PostId]) REFERENCES [WallPosts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE TABLE [PollVotes] (
        [Id] int NOT NULL IDENTITY,
        [PostId] int NOT NULL,
        [OptionId] int NOT NULL,
        [UserId] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PollVotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PollVotes_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PollVotes_PollOptions_OptionId] FOREIGN KEY ([OptionId]) REFERENCES [PollOptions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PollVotes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PollVotes_WallPosts_PostId] FOREIGN KEY ([PostId]) REFERENCES [WallPosts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Notifications_EmailPending] ON [Notifications] ([EmailPending]) WHERE [EmailPending] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_PollOptions_OrganizationId] ON [PollOptions] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_PollOptions_PostId] ON [PollOptions] ([PostId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_PollVotes_OptionId] ON [PollVotes] ([OptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_PollVotes_OrganizationId] ON [PollVotes] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PollVotes_PostId_UserId] ON [PollVotes] ([PostId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_PollVotes_UserId] ON [PollVotes] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_ProfileChangeRequests_EmployeeId] ON [ProfileChangeRequests] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_ProfileChangeRequests_OrganizationId_Status] ON [ProfileChangeRequests] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_WallComments_AuthorUserId] ON [WallComments] ([AuthorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_WallComments_OrganizationId] ON [WallComments] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_WallComments_PostId] ON [WallComments] ([PostId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_WallPostRecipients_EmployeeId] ON [WallPostRecipients] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_WallPostRecipients_OrganizationId] ON [WallPostRecipients] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_WallPostRecipients_PostId] ON [WallPostRecipients] ([PostId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_WallPosts_AuthorUserId] ON [WallPosts] ([AuthorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_WallPosts_OrganizationId_CreatedAtUtc] ON [WallPosts] ([OrganizationId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_WallPosts_OrganizationId_IsPinned] ON [WallPosts] ([OrganizationId], [IsPinned]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_WallReactions_OrganizationId] ON [WallReactions] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WallReactions_PostId_UserId] ON [WallReactions] ([PostId], [UserId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    CREATE INDEX [IX_WallReactions_UserId] ON [WallReactions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045134_Engagement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112045134_Engagement', N'9.0.10');
END;

COMMIT;
GO

