BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE TABLE [ImprovementPlans] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [ManagerEmployeeId] int NULL,
        [CreatedByName] nvarchar(150) NOT NULL,
        [StartsOn] date NOT NULL,
        [EndsOn] date NOT NULL,
        [Concerns] nvarchar(4000) NOT NULL,
        [Support] nvarchar(2000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [Extensions] int NOT NULL,
        [StartedAtUtc] datetime2 NULL,
        [AcknowledgedAtUtc] datetime2 NULL,
        [EmployeeComment] nvarchar(2000) NULL,
        [OutcomeNote] nvarchar(2000) NULL,
        [ClosedByName] nvarchar(150) NULL,
        [ClosedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ImprovementPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ImprovementPlans_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ImprovementPlans_Employees_ManagerEmployeeId] FOREIGN KEY ([ManagerEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ImprovementPlans_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE TABLE [PromotionCases] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [ProposedByEmployeeId] int NULL,
        [ProposedByName] nvarchar(150) NOT NULL,
        [FromDesignationId] int NULL,
        [ToDesignationId] int NULL,
        [CurrentAnnualGross] decimal(18,2) NULL,
        [ProposedAnnualGross] decimal(18,2) NULL,
        [EffectiveOn] date NOT NULL,
        [Justification] nvarchar(4000) NOT NULL,
        [PerformanceReviewId] int NULL,
        [Status] nvarchar(20) NOT NULL,
        [DecidedByName] nvarchar(150) NULL,
        [DecidedAtUtc] datetime2 NULL,
        [DecisionNote] nvarchar(1000) NULL,
        [AppliedAtUtc] datetime2 NULL,
        [ApplyError] nvarchar(500) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PromotionCases] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PromotionCases_Designations_FromDesignationId] FOREIGN KEY ([FromDesignationId]) REFERENCES [Designations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PromotionCases_Designations_ToDesignationId] FOREIGN KEY ([ToDesignationId]) REFERENCES [Designations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PromotionCases_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PromotionCases_Employees_ProposedByEmployeeId] FOREIGN KEY ([ProposedByEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PromotionCases_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PromotionCases_PerformanceReviews_PerformanceReviewId] FOREIGN KEY ([PerformanceReviewId]) REFERENCES [PerformanceReviews] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE TABLE [PipCheckIns] (
        [Id] int NOT NULL IDENTITY,
        [ImprovementPlanId] int NOT NULL,
        [OnDate] date NOT NULL,
        [Progress] nvarchar(20) NOT NULL,
        [Note] nvarchar(2000) NOT NULL,
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
        CONSTRAINT [PK_PipCheckIns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PipCheckIns_ImprovementPlans_ImprovementPlanId] FOREIGN KEY ([ImprovementPlanId]) REFERENCES [ImprovementPlans] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PipCheckIns_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE TABLE [PipObjectives] (
        [Id] int NOT NULL IDENTITY,
        [ImprovementPlanId] int NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [Measure] nvarchar(1000) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PipObjectives] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PipObjectives_ImprovementPlans_ImprovementPlanId] FOREIGN KEY ([ImprovementPlanId]) REFERENCES [ImprovementPlans] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PipObjectives_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_ImprovementPlans_EmployeeId] ON [ImprovementPlans] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_ImprovementPlans_ManagerEmployeeId] ON [ImprovementPlans] ([ManagerEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_ImprovementPlans_OrganizationId_EmployeeId_Status] ON [ImprovementPlans] ([OrganizationId], [EmployeeId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_PipCheckIns_ImprovementPlanId] ON [PipCheckIns] ([ImprovementPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_PipCheckIns_OrganizationId] ON [PipCheckIns] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_PipObjectives_ImprovementPlanId] ON [PipObjectives] ([ImprovementPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_PipObjectives_OrganizationId] ON [PipObjectives] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_PromotionCases_EmployeeId] ON [PromotionCases] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_PromotionCases_FromDesignationId] ON [PromotionCases] ([FromDesignationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_PromotionCases_OrganizationId_EmployeeId] ON [PromotionCases] ([OrganizationId], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_PromotionCases_OrganizationId_Status_EffectiveOn] ON [PromotionCases] ([OrganizationId], [Status], [EffectiveOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_PromotionCases_PerformanceReviewId] ON [PromotionCases] ([PerformanceReviewId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_PromotionCases_ProposedByEmployeeId] ON [PromotionCases] ([ProposedByEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    CREATE INDEX [IX_PromotionCases_ToDesignationId] ON [PromotionCases] ([ToDesignationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114084830_PromotionsAndPips'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261114084830_PromotionsAndPips', N'9.0.10');
END;

COMMIT;
GO

