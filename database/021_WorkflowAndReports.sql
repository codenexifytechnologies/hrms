BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE TABLE [SavedReports] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Source] nvarchar(20) NOT NULL,
        [Definition] nvarchar(max) NOT NULL,
        [OwnerUserId] int NOT NULL,
        [IsShared] bit NOT NULL,
        [LastRunAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SavedReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SavedReports_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SavedReports_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE TABLE [WorkflowRules] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Trigger] nvarchar(30) NOT NULL,
        [DaysOffset] int NOT NULL,
        [DepartmentId] int NULL,
        [BranchId] int NULL,
        [EmploymentType] nvarchar(20) NULL,
        [IsActive] bit NOT NULL,
        [LastRunAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_WorkflowRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowRules_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkflowRules_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkflowRules_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE TABLE [WorkTasks] (
        [Id] int NOT NULL IDENTITY,
        [AssigneeUserId] int NOT NULL,
        [EmployeeId] int NULL,
        [Title] nvarchar(200) NOT NULL,
        [Details] nvarchar(1000) NULL,
        [DueOn] date NULL,
        [IsDone] bit NOT NULL,
        [DoneAtUtc] datetime2 NULL,
        [WorkflowRuleId] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_WorkTasks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkTasks_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkTasks_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkTasks_Users_AssigneeUserId] FOREIGN KEY ([AssigneeUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE TABLE [WorkflowActions] (
        [Id] int NOT NULL IDENTITY,
        [WorkflowRuleId] int NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        [Recipient] nvarchar(20) NOT NULL,
        [UserId] int NULL,
        [Title] nvarchar(200) NULL,
        [Message] nvarchar(1000) NULL,
        [DueInDays] int NOT NULL,
        [TargetId] int NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_WorkflowActions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowActions_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkflowActions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkflowActions_WorkflowRules_WorkflowRuleId] FOREIGN KEY ([WorkflowRuleId]) REFERENCES [WorkflowRules] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE TABLE [WorkflowRuns] (
        [Id] int NOT NULL IDENTITY,
        [WorkflowRuleId] int NOT NULL,
        [SubjectKey] nvarchar(100) NOT NULL,
        [EmployeeId] int NOT NULL,
        [RanAtUtc] datetime2 NOT NULL,
        [Succeeded] bit NOT NULL,
        [Detail] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_WorkflowRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowRuns_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkflowRuns_WorkflowRules_WorkflowRuleId] FOREIGN KEY ([WorkflowRuleId]) REFERENCES [WorkflowRules] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE INDEX [IX_SavedReports_OrganizationId] ON [SavedReports] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE INDEX [IX_SavedReports_OwnerUserId] ON [SavedReports] ([OwnerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE INDEX [IX_WorkflowActions_OrganizationId] ON [WorkflowActions] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE INDEX [IX_WorkflowActions_UserId] ON [WorkflowActions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE INDEX [IX_WorkflowActions_WorkflowRuleId] ON [WorkflowActions] ([WorkflowRuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE INDEX [IX_WorkflowRules_BranchId] ON [WorkflowRules] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE INDEX [IX_WorkflowRules_DepartmentId] ON [WorkflowRules] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE INDEX [IX_WorkflowRules_OrganizationId] ON [WorkflowRules] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE INDEX [IX_WorkflowRuns_OrganizationId] ON [WorkflowRuns] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WorkflowRuns_WorkflowRuleId_SubjectKey] ON [WorkflowRuns] ([WorkflowRuleId], [SubjectKey]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_AssigneeUserId] ON [WorkTasks] ([AssigneeUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_EmployeeId] ON [WorkTasks] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_OrganizationId_AssigneeUserId_IsDone] ON [WorkTasks] ([OrganizationId], [AssigneeUserId], [IsDone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114091355_WorkflowAndReports'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261114091355_WorkflowAndReports', N'9.0.10');
END;

COMMIT;
GO

