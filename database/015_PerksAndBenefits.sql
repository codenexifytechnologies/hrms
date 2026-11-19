BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114035330_PerksAndBenefits'
)
BEGIN
    CREATE TABLE [BenefitPlans] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Category] nvarchar(30) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Provider] nvarchar(150) NULL,
        [PolicyNumber] nvarchar(100) NULL,
        [CoverageAmount] decimal(18,2) NULL,
        [PolicyRenewsOn] date NULL,
        [EmployerCostPerMonth] decimal(18,2) NOT NULL,
        [EmployeeContributionPerMonth] decimal(18,2) NOT NULL,
        [DependentContributionPerMonth] decimal(18,2) NOT NULL,
        [AllowancePerMonth] decimal(18,2) NOT NULL,
        [AllowanceTaxable] bit NOT NULL,
        [EnrollmentMode] nvarchar(20) NOT NULL,
        [CoversDependents] bit NOT NULL,
        [MaxDependents] int NOT NULL,
        [EnrollmentOpensOn] date NULL,
        [EnrollmentClosesOn] date NULL,
        [EligibleEmploymentTypes] nvarchar(200) NULL,
        [MinServiceDays] int NOT NULL,
        [BranchId] int NULL,
        [DepartmentId] int NULL,
        [ExcludeProbation] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [DocumentStorageKey] nvarchar(300) NULL,
        [DocumentFileName] nvarchar(200) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_BenefitPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BenefitPlans_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BenefitPlans_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BenefitPlans_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114035330_PerksAndBenefits'
)
BEGIN
    CREATE TABLE [BenefitEnrollments] (
        [Id] int NOT NULL IDENTITY,
        [BenefitPlanId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [StartsOn] date NOT NULL,
        [EndsOn] date NULL,
        [EndReason] nvarchar(300) NULL,
        [EnrolledBy] nvarchar(150) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_BenefitEnrollments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BenefitEnrollments_BenefitPlans_BenefitPlanId] FOREIGN KEY ([BenefitPlanId]) REFERENCES [BenefitPlans] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BenefitEnrollments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BenefitEnrollments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114035330_PerksAndBenefits'
)
BEGIN
    CREATE TABLE [BenefitDependents] (
        [Id] int NOT NULL IDENTITY,
        [BenefitEnrollmentId] int NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Relation] nvarchar(20) NOT NULL,
        [DateOfBirth] date NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_BenefitDependents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BenefitDependents_BenefitEnrollments_BenefitEnrollmentId] FOREIGN KEY ([BenefitEnrollmentId]) REFERENCES [BenefitEnrollments] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_BenefitDependents_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114035330_PerksAndBenefits'
)
BEGIN
    CREATE INDEX [IX_BenefitDependents_BenefitEnrollmentId] ON [BenefitDependents] ([BenefitEnrollmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114035330_PerksAndBenefits'
)
BEGIN
    CREATE INDEX [IX_BenefitDependents_OrganizationId] ON [BenefitDependents] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114035330_PerksAndBenefits'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BenefitEnrollments_BenefitPlanId_EmployeeId] ON [BenefitEnrollments] ([BenefitPlanId], [EmployeeId]) WHERE [IsDeleted] = 0 AND [Status] = ''Active''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114035330_PerksAndBenefits'
)
BEGIN
    CREATE INDEX [IX_BenefitEnrollments_EmployeeId] ON [BenefitEnrollments] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114035330_PerksAndBenefits'
)
BEGIN
    CREATE INDEX [IX_BenefitEnrollments_OrganizationId_EmployeeId_Status] ON [BenefitEnrollments] ([OrganizationId], [EmployeeId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114035330_PerksAndBenefits'
)
BEGIN
    CREATE INDEX [IX_BenefitPlans_BranchId] ON [BenefitPlans] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114035330_PerksAndBenefits'
)
BEGIN
    CREATE INDEX [IX_BenefitPlans_DepartmentId] ON [BenefitPlans] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114035330_PerksAndBenefits'
)
BEGIN
    CREATE INDEX [IX_BenefitPlans_OrganizationId_IsActive] ON [BenefitPlans] ([OrganizationId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114035330_PerksAndBenefits'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261114035330_PerksAndBenefits', N'9.0.10');
END;

COMMIT;
GO

