BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE TABLE [Goals] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Level] nvarchar(20) NOT NULL,
        [DepartmentId] int NULL,
        [OwnerEmployeeId] int NULL,
        [ParentGoalId] int NULL,
        [StartsOn] date NOT NULL,
        [DueOn] date NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Progress] decimal(5,1) NOT NULL,
        [CreatedByName] nvarchar(150) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Goals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Goals_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Goals_Employees_OwnerEmployeeId] FOREIGN KEY ([OwnerEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Goals_Goals_ParentGoalId] FOREIGN KEY ([ParentGoalId]) REFERENCES [Goals] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Goals_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE TABLE [ReviewCycles] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [PeriodStart] date NOT NULL,
        [PeriodEnd] date NOT NULL,
        [SelfReviewDue] date NOT NULL,
        [ManagerReviewDue] date NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [RatingLabels] nvarchar(400) NOT NULL,
        [IncludeGoals] bit NOT NULL,
        [JoinedOnOrBefore] date NULL,
        [DepartmentId] int NULL,
        [SkipSelfReview] bit NOT NULL,
        [LaunchedAtUtc] datetime2 NULL,
        [ReleasedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ReviewCycles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReviewCycles_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReviewCycles_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE TABLE [GoalCheckIns] (
        [Id] int NOT NULL IDENTITY,
        [GoalId] int NOT NULL,
        [Progress] decimal(5,1) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Note] nvarchar(1000) NULL,
        [ByName] nvarchar(150) NOT NULL,
        [AtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_GoalCheckIns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GoalCheckIns_Goals_GoalId] FOREIGN KEY ([GoalId]) REFERENCES [Goals] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GoalCheckIns_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE TABLE [KeyResults] (
        [Id] int NOT NULL IDENTITY,
        [GoalId] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        [StartValue] decimal(18,2) NOT NULL,
        [TargetValue] decimal(18,2) NOT NULL,
        [CurrentValue] decimal(18,2) NOT NULL,
        [Unit] nvarchar(20) NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_KeyResults] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_KeyResults_Goals_GoalId] FOREIGN KEY ([GoalId]) REFERENCES [Goals] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_KeyResults_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE TABLE [PerformanceReviews] (
        [Id] int NOT NULL IDENTITY,
        [ReviewCycleId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [ReviewerEmployeeId] int NULL,
        [Status] nvarchar(20) NOT NULL,
        [SelfSubmittedAtUtc] datetime2 NULL,
        [ManagerSubmittedAtUtc] datetime2 NULL,
        [ReviewedByName] nvarchar(150) NULL,
        [OverallRating] int NULL,
        [FinalRating] int NULL,
        [AdjustmentNote] nvarchar(500) NULL,
        [AcknowledgedAtUtc] datetime2 NULL,
        [EmployeeComment] nvarchar(2000) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PerformanceReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PerformanceReviews_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PerformanceReviews_Employees_ReviewerEmployeeId] FOREIGN KEY ([ReviewerEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PerformanceReviews_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PerformanceReviews_ReviewCycles_ReviewCycleId] FOREIGN KEY ([ReviewCycleId]) REFERENCES [ReviewCycles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE TABLE [ReviewQuestions] (
        [Id] int NOT NULL IDENTITY,
        [ReviewCycleId] int NOT NULL,
        [Text] nvarchar(500) NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        [Audience] nvarchar(20) NOT NULL,
        [Required] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ReviewQuestions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReviewQuestions_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReviewQuestions_ReviewCycles_ReviewCycleId] FOREIGN KEY ([ReviewCycleId]) REFERENCES [ReviewCycles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE TABLE [ReviewAnswers] (
        [Id] int NOT NULL IDENTITY,
        [PerformanceReviewId] int NOT NULL,
        [ReviewQuestionId] int NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [Rating] int NULL,
        [Text] nvarchar(4000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ReviewAnswers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReviewAnswers_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReviewAnswers_PerformanceReviews_PerformanceReviewId] FOREIGN KEY ([PerformanceReviewId]) REFERENCES [PerformanceReviews] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ReviewAnswers_ReviewQuestions_ReviewQuestionId] FOREIGN KEY ([ReviewQuestionId]) REFERENCES [ReviewQuestions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_GoalCheckIns_GoalId_AtUtc] ON [GoalCheckIns] ([GoalId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_GoalCheckIns_OrganizationId] ON [GoalCheckIns] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_Goals_DepartmentId] ON [Goals] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_Goals_OrganizationId_Level_DueOn] ON [Goals] ([OrganizationId], [Level], [DueOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_Goals_OrganizationId_OwnerEmployeeId] ON [Goals] ([OrganizationId], [OwnerEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_Goals_OwnerEmployeeId] ON [Goals] ([OwnerEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_Goals_ParentGoalId] ON [Goals] ([ParentGoalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_KeyResults_GoalId] ON [KeyResults] ([GoalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_KeyResults_OrganizationId] ON [KeyResults] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_PerformanceReviews_EmployeeId] ON [PerformanceReviews] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_PerformanceReviews_OrganizationId_ReviewerEmployeeId_Status] ON [PerformanceReviews] ([OrganizationId], [ReviewerEmployeeId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PerformanceReviews_ReviewCycleId_EmployeeId] ON [PerformanceReviews] ([ReviewCycleId], [EmployeeId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_PerformanceReviews_ReviewerEmployeeId] ON [PerformanceReviews] ([ReviewerEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_ReviewAnswers_OrganizationId] ON [ReviewAnswers] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ReviewAnswers_PerformanceReviewId_ReviewQuestionId_Role] ON [ReviewAnswers] ([PerformanceReviewId], [ReviewQuestionId], [Role]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_ReviewAnswers_ReviewQuestionId] ON [ReviewAnswers] ([ReviewQuestionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_ReviewCycles_DepartmentId] ON [ReviewCycles] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_ReviewCycles_OrganizationId] ON [ReviewCycles] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_ReviewQuestions_OrganizationId] ON [ReviewQuestions] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    CREATE INDEX [IX_ReviewQuestions_ReviewCycleId] ON [ReviewQuestions] ([ReviewCycleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114041547_GoalsAndReviews'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261114041547_GoalsAndReviews', N'9.0.10');
END;

COMMIT;
GO

