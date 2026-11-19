BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    CREATE TABLE [CalibrationTargets] (
        [Id] int NOT NULL IDENTITY,
        [ReviewCycleId] int NOT NULL,
        [Rating] int NOT NULL,
        [Percent] decimal(5,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_CalibrationTargets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CalibrationTargets_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CalibrationTargets_ReviewCycles_ReviewCycleId] FOREIGN KEY ([ReviewCycleId]) REFERENCES [ReviewCycles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    CREATE TABLE [CompensationCycles] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [EffectiveOn] date NOT NULL,
        [BudgetPercent] decimal(6,2) NOT NULL,
        [ReviewCycleId] int NULL,
        [JoinedOnOrBefore] date NULL,
        [DepartmentId] int NULL,
        [PlanningDueOn] date NULL,
        [Status] nvarchar(20) NOT NULL,
        [ApprovedByName] nvarchar(150) NULL,
        [ApprovedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_CompensationCycles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CompensationCycles_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompensationCycles_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompensationCycles_ReviewCycles_ReviewCycleId] FOREIGN KEY ([ReviewCycleId]) REFERENCES [ReviewCycles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    CREATE TABLE [CompensationGuidelines] (
        [Id] int NOT NULL IDENTITY,
        [CompensationCycleId] int NOT NULL,
        [Rating] int NOT NULL,
        [MinPercent] decimal(6,2) NOT NULL,
        [MaxPercent] decimal(6,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_CompensationGuidelines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CompensationGuidelines_CompensationCycles_CompensationCycleId] FOREIGN KEY ([CompensationCycleId]) REFERENCES [CompensationCycles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CompensationGuidelines_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    CREATE TABLE [CompensationLines] (
        [Id] int NOT NULL IDENTITY,
        [CompensationCycleId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [PlannerEmployeeId] int NULL,
        [CurrentAnnualGross] decimal(18,2) NOT NULL,
        [Rating] int NULL,
        [ProposedPercent] decimal(6,2) NULL,
        [ProposedAnnualGross] decimal(18,2) NULL,
        [Note] nvarchar(500) NULL,
        [UpdatedByName] nvarchar(150) NULL,
        [AppliedAtUtc] datetime2 NULL,
        [ApplyError] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_CompensationLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CompensationLines_CompensationCycles_CompensationCycleId] FOREIGN KEY ([CompensationCycleId]) REFERENCES [CompensationCycles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CompensationLines_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompensationLines_Employees_PlannerEmployeeId] FOREIGN KEY ([PlannerEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompensationLines_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    CREATE INDEX [IX_CalibrationTargets_OrganizationId] ON [CalibrationTargets] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CalibrationTargets_ReviewCycleId_Rating] ON [CalibrationTargets] ([ReviewCycleId], [Rating]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    CREATE INDEX [IX_CompensationCycles_DepartmentId] ON [CompensationCycles] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    CREATE INDEX [IX_CompensationCycles_OrganizationId] ON [CompensationCycles] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    CREATE INDEX [IX_CompensationCycles_ReviewCycleId] ON [CompensationCycles] ([ReviewCycleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CompensationGuidelines_CompensationCycleId_Rating] ON [CompensationGuidelines] ([CompensationCycleId], [Rating]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    CREATE INDEX [IX_CompensationGuidelines_OrganizationId] ON [CompensationGuidelines] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CompensationLines_CompensationCycleId_EmployeeId] ON [CompensationLines] ([CompensationCycleId], [EmployeeId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    CREATE INDEX [IX_CompensationLines_EmployeeId] ON [CompensationLines] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    CREATE INDEX [IX_CompensationLines_OrganizationId_PlannerEmployeeId] ON [CompensationLines] ([OrganizationId], [PlannerEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    CREATE INDEX [IX_CompensationLines_PlannerEmployeeId] ON [CompensationLines] ([PlannerEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114085839_CompensationAndCalibration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261114085839_CompensationAndCalibration', N'9.0.10');
END;

COMMIT;
GO

