/* =====================================================================================================================
   Codenexify HRMS - full database script (schema for every module, 22 migrations, latest: 20261114093445_HeadcountAndPreboarding)
   Generated 2026-11-16 21:52 by database/Generate-FullDatabaseScript.ps1. Do not edit by hand.

   USE IT FOR
     * A brand-new database: creates every table, index and key in one go (instead of running 001-0NN one by one).
     * An existing database: safe to run too. Each migration checks __EFMigrationsHistory and is skipped if applied,
       so only what is missing is added. Back up first, as with any schema change.

   HOW
     1. Create an empty database (SQL Server 2019+ / Azure SQL), e.g.:
            CREATE DATABASE CodenexifyHRMS COLLATE SQL_Latin1_General_CP1_CI_AS;
     2. Run this file against it (SSMS: open and Execute, or):
            sqlcmd -S <server> -d CodenexifyHRMS -E -b -i database\000_FullDatabase.sql
     3. Start the application. On start it fills in the starting data that this script does not contain:
        feature catalog, permissions, the default plans, statutory rates, legal page drafts and the first Super Admin
        (Seed__SuperAdminEmail / Seed__SuperAdminPassword). This happens even with Database__AutoMigrate=false;
        the app only needs db_datareader + db_datawriter for it.

   The script holds structure only: no company data, no passwords.
   ===================================================================================================================== */
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [TimestampUtc] datetime2 NOT NULL,
        [OrganizationId] int NULL,
        [ActorType] nvarchar(40) NULL,
        [ActorId] int NULL,
        [ActorName] nvarchar(200) NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(100) NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [Details] nvarchar(1000) NULL,
        [IpAddress] nvarchar(64) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [Features] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Description] nvarchar(500) NULL,
        [ModuleGroup] nvarchar(100) NOT NULL,
        [Tier] nvarchar(40) NOT NULL,
        [IsGloballyEnabled] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Features] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [LoginAudits] (
        [Id] bigint NOT NULL IDENTITY,
        [TimestampUtc] datetime2 NOT NULL,
        [PrincipalType] nvarchar(40) NOT NULL,
        [OrganizationId] int NULL,
        [UserId] int NULL,
        [Email] nvarchar(200) NOT NULL,
        [Succeeded] bit NOT NULL,
        [FailureReason] nvarchar(300) NULL,
        [IpAddress] nvarchar(64) NULL,
        [UserAgent] nvarchar(500) NULL,
        CONSTRAINT [PK_LoginAudits] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [Organizations] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [LegalName] nvarchar(200) NULL,
        [Email] nvarchar(200) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [Website] nvarchar(200) NULL,
        [Gstin] nvarchar(15) NULL,
        [Pan] nvarchar(10) NULL,
        [AddressLine] nvarchar(300) NULL,
        [City] nvarchar(100) NULL,
        [State] nvarchar(100) NULL,
        [Country] nvarchar(100) NOT NULL,
        [PostalCode] nvarchar(10) NULL,
        [TimeZone] nvarchar(100) NOT NULL,
        [Industry] nvarchar(100) NULL,
        [Status] nvarchar(40) NOT NULL,
        [StatusReason] nvarchar(500) NULL,
        [StatusChangedAtUtc] datetime2 NULL,
        [MaxEmployeesOverride] int NULL,
        [MaxBranchesOverride] int NULL,
        [StorageLimitGbOverride] int NULL,
        [StorageUsedMb] int NOT NULL,
        [BrandColor] nvarchar(7) NOT NULL,
        [LogoUrl] nvarchar(500) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Organizations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [Permissions] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(100) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Module] nvarchar(100) NOT NULL,
        [FeatureCode] nvarchar(50) NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [PlatformUsers] (
        [Id] int NOT NULL IDENTITY,
        [Email] nvarchar(200) NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [Role] nvarchar(40) NOT NULL,
        [IsActive] bit NOT NULL,
        [MustChangePassword] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [TwoFactorSecret] nvarchar(1000) NULL,
        [FailedLoginCount] int NOT NULL,
        [LockoutEndUtc] datetime2 NULL,
        [LastLoginAtUtc] datetime2 NULL,
        [LastLoginIp] nvarchar(64) NULL,
        [AllowedIps] nvarchar(500) NULL,
        [SecurityStamp] nvarchar(64) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PlatformUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [SubscriptionPlans] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [ParentPlanId] int NULL,
        [MonthlyPrice] decimal(18,2) NOT NULL,
        [YearlyPrice] decimal(18,2) NOT NULL,
        [IsPerEmployeePricing] bit NOT NULL,
        [TrialDays] int NOT NULL,
        [MaxEmployees] int NOT NULL,
        [MaxBranches] int NOT NULL,
        [StorageLimitGb] int NOT NULL,
        [IsCustom] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [IsPublic] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SubscriptionPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubscriptionPlans_SubscriptionPlans_ParentPlanId] FOREIGN KEY ([ParentPlanId]) REFERENCES [SubscriptionPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [OrganizationFeatureOverrides] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [FeatureId] int NOT NULL,
        [IsEnabled] bit NOT NULL,
        [Reason] nvarchar(300) NULL,
        [ExpiresOn] date NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_OrganizationFeatureOverrides] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationFeatureOverrides_Features_FeatureId] FOREIGN KEY ([FeatureId]) REFERENCES [Features] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrganizationFeatureOverrides_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(300) NULL,
        [IsSystem] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Roles_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [Email] nvarchar(200) NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [IsActive] bit NOT NULL,
        [IsOrganizationOwner] bit NOT NULL,
        [EmailConfirmed] bit NOT NULL,
        [MustChangePassword] bit NOT NULL,
        [FailedLoginCount] int NOT NULL,
        [LockoutEndUtc] datetime2 NULL,
        [LastLoginAtUtc] datetime2 NULL,
        [SecurityStamp] nvarchar(64) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [PlatformUserPermissions] (
        [PlatformUserId] int NOT NULL,
        [Permission] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_PlatformUserPermissions] PRIMARY KEY ([PlatformUserId], [Permission]),
        CONSTRAINT [FK_PlatformUserPermissions_PlatformUsers_PlatformUserId] FOREIGN KEY ([PlatformUserId]) REFERENCES [PlatformUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [OrganizationSubscriptions] (
        [Id] int NOT NULL IDENTITY,
        [OrganizationId] int NOT NULL,
        [PlanId] int NOT NULL,
        [BillingCycle] nvarchar(40) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [TrialEndsOn] date NULL,
        [GraceEndsOn] date NULL,
        [PausedOn] date NULL,
        [CancelledOn] date NULL,
        [CancellationReason] nvarchar(500) NULL,
        [AutoRenew] bit NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [MaxEmployeesOverride] int NULL,
        [MaxBranchesOverride] int NULL,
        [IsCurrent] bit NOT NULL,
        [Notes] nvarchar(500) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_OrganizationSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationSubscriptions_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrganizationSubscriptions_SubscriptionPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [SubscriptionPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [PlanFeatures] (
        [PlanId] int NOT NULL,
        [FeatureId] int NOT NULL,
        CONSTRAINT [PK_PlanFeatures] PRIMARY KEY ([PlanId], [FeatureId]),
        CONSTRAINT [FK_PlanFeatures_Features_FeatureId] FOREIGN KEY ([FeatureId]) REFERENCES [Features] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PlanFeatures_SubscriptionPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [SubscriptionPlans] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [RoleId] int NOT NULL,
        [PermissionId] int NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] int NOT NULL,
        [RoleId] int NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_OrganizationId_TimestampUtc] ON [AuditLogs] ([OrganizationId], [TimestampUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_TimestampUtc] ON [AuditLogs] ([TimestampUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Features_Code] ON [Features] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE INDEX [IX_LoginAudits_PrincipalType_Succeeded_TimestampUtc] ON [LoginAudits] ([PrincipalType], [Succeeded], [TimestampUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE INDEX [IX_LoginAudits_TimestampUtc] ON [LoginAudits] ([TimestampUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE INDEX [IX_OrganizationFeatureOverrides_FeatureId] ON [OrganizationFeatureOverrides] ([FeatureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrganizationFeatureOverrides_OrganizationId_FeatureId] ON [OrganizationFeatureOverrides] ([OrganizationId], [FeatureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Organizations_Code] ON [Organizations] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE INDEX [IX_Organizations_Status] ON [Organizations] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE INDEX [IX_OrganizationSubscriptions_PlanId] ON [OrganizationSubscriptions] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE INDEX [IX_OrganizationSubscriptions_Status_EndDate] ON [OrganizationSubscriptions] ([Status], [EndDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_OrganizationSubscriptions_Current] ON [OrganizationSubscriptions] ([OrganizationId]) WHERE [IsCurrent] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_Code] ON [Permissions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE INDEX [IX_PlanFeatures_FeatureId] ON [PlanFeatures] ([FeatureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlatformUsers_Email] ON [PlatformUsers] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE INDEX [IX_Roles_OrganizationId_Name] ON [Roles] ([OrganizationId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubscriptionPlans_Code] ON [SubscriptionPlans] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE INDEX [IX_SubscriptionPlans_ParentPlanId] ON [SubscriptionPlans] ([ParentPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Users_OrganizationId_Email] ON [Users] ([OrganizationId], [Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182347_InitialPlatform'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111182347_InitialPlatform', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [Branches] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [AddressLine] nvarchar(300) NULL,
        [City] nvarchar(100) NULL,
        [State] nvarchar(100) NULL,
        [Country] nvarchar(100) NOT NULL,
        [PostalCode] nvarchar(10) NULL,
        [Phone] nvarchar(20) NULL,
        [IsHeadOffice] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Branches_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [Designations] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Level] int NULL,
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
        CONSTRAINT [PK_Designations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Designations_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [ExitChecklistTemplateItems] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [Owner] nvarchar(50) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ExitChecklistTemplateItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExitChecklistTemplateItems_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [OrganizationSettings] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeIdPrefix] nvarchar(10) NOT NULL,
        [EmployeeIdPadding] int NOT NULL,
        [NextEmployeeNumber] int NOT NULL,
        [DefaultNoticePeriodDays] int NOT NULL,
        [DefaultProbationMonths] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_OrganizationSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationSettings_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Code] nvarchar(20) NULL,
        [Description] nvarchar(500) NULL,
        [ParentDepartmentId] int NULL,
        [HeadEmployeeId] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Departments_Departments_ParentDepartmentId] FOREIGN KEY ([ParentDepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Departments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [Employees] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeCode] nvarchar(30) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [MiddleName] nvarchar(100) NULL,
        [LastName] nvarchar(100) NOT NULL,
        [WorkEmail] nvarchar(200) NOT NULL,
        [PersonalEmail] nvarchar(200) NULL,
        [MobilePhone] nvarchar(20) NULL,
        [Gender] nvarchar(40) NULL,
        [DateOfBirth] date NULL,
        [MaritalStatus] nvarchar(40) NULL,
        [BloodGroup] nvarchar(5) NULL,
        [Nationality] nvarchar(60) NOT NULL,
        [CurrentAddress] nvarchar(500) NULL,
        [PermanentAddress] nvarchar(500) NULL,
        [EmergencyContactName] nvarchar(150) NULL,
        [EmergencyContactRelation] nvarchar(50) NULL,
        [EmergencyContactPhone] nvarchar(20) NULL,
        [PanEncrypted] nvarchar(1000) NULL,
        [PanMasked] nvarchar(10) NULL,
        [Uan] nvarchar(12) NULL,
        [BranchId] int NULL,
        [DepartmentId] int NULL,
        [DesignationId] int NULL,
        [ReportingManagerId] int NULL,
        [EmploymentType] nvarchar(40) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [DateOfJoining] date NOT NULL,
        [ProbationEndDate] date NULL,
        [ConfirmationDate] date NULL,
        [DateOfExit] date NULL,
        [NoticePeriodDays] int NOT NULL,
        [UserId] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Employees_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Employees_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Employees_Designations_DesignationId] FOREIGN KEY ([DesignationId]) REFERENCES [Designations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Employees_Employees_ReportingManagerId] FOREIGN KEY ([ReportingManagerId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Employees_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Employees_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [EmployeeBankDetails] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [BankName] nvarchar(150) NOT NULL,
        [AccountHolderName] nvarchar(150) NOT NULL,
        [AccountNumberEncrypted] nvarchar(1000) NOT NULL,
        [AccountNumberLast4] nvarchar(4) NOT NULL,
        [Ifsc] nvarchar(11) NOT NULL,
        [BranchName] nvarchar(150) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_EmployeeBankDetails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeBankDetails_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeBankDetails_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [EmployeeDocuments] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Category] nvarchar(40) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [StorageKey] nvarchar(300) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [ExpiresOn] date NULL,
        [IsVerified] bit NOT NULL,
        [VerifiedBy] nvarchar(200) NULL,
        [VerifiedAtUtc] datetime2 NULL,
        [VisibleToEmployee] bit NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_EmployeeDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeDocuments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeDocuments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [EmployeeEducation] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Qualification] nvarchar(150) NOT NULL,
        [Institution] nvarchar(200) NOT NULL,
        [Specialization] nvarchar(150) NULL,
        [YearOfPassing] int NULL,
        [Grade] nvarchar(30) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_EmployeeEducation] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeEducation_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeEducation_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [EmployeeExperience] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Company] nvarchar(200) NOT NULL,
        [Designation] nvarchar(150) NULL,
        [FromDate] date NOT NULL,
        [ToDate] date NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_EmployeeExperience] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeExperience_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeExperience_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [ExitRequests] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Type] nvarchar(40) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [InitiatedByEmployee] bit NOT NULL,
        [RequestedOn] date NOT NULL,
        [RequestedLastWorkingDay] date NOT NULL,
        [ApprovedLastWorkingDay] date NULL,
        [Status] nvarchar(40) NOT NULL,
        [DecidedBy] nvarchar(200) NULL,
        [DecidedAtUtc] datetime2 NULL,
        [DecisionNotes] nvarchar(1000) NULL,
        [ExitInterviewNotes] nvarchar(2000) NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ExitRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExitRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExitRequests_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [ExitChecklistItems] (
        [Id] int NOT NULL IDENTITY,
        [ExitRequestId] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Owner] nvarchar(50) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsCompleted] bit NOT NULL,
        [CompletedBy] nvarchar(200) NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ExitChecklistItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExitChecklistItems_ExitRequests_ExitRequestId] FOREIGN KEY ([ExitRequestId]) REFERENCES [ExitRequests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExitChecklistItems_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Branches_OrganizationId_Code] ON [Branches] ([OrganizationId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Departments_HeadEmployeeId] ON [Departments] ([HeadEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Departments_OrganizationId_Name] ON [Departments] ([OrganizationId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Departments_ParentDepartmentId] ON [Departments] ([ParentDepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Designations_OrganizationId_Name] ON [Designations] ([OrganizationId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EmployeeBankDetails_EmployeeId] ON [EmployeeBankDetails] ([EmployeeId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeBankDetails_OrganizationId] ON [EmployeeBankDetails] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeDocuments_EmployeeId] ON [EmployeeDocuments] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeDocuments_OrganizationId_ExpiresOn] ON [EmployeeDocuments] ([OrganizationId], [ExpiresOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeEducation_EmployeeId] ON [EmployeeEducation] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeEducation_OrganizationId] ON [EmployeeEducation] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeExperience_EmployeeId] ON [EmployeeExperience] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeExperience_OrganizationId] ON [EmployeeExperience] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Employees_BranchId] ON [Employees] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Employees_DepartmentId] ON [Employees] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Employees_DesignationId] ON [Employees] ([DesignationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Employees_OrganizationId_Status] ON [Employees] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Employees_OrganizationId_WorkEmail] ON [Employees] ([OrganizationId], [WorkEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Employees_ReportingManagerId] ON [Employees] ([ReportingManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Employees_UserId] ON [Employees] ([UserId]) WHERE [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Employees_OrganizationId_Code] ON [Employees] ([OrganizationId], [EmployeeCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_ExitChecklistItems_ExitRequestId] ON [ExitChecklistItems] ([ExitRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_ExitChecklistItems_OrganizationId] ON [ExitChecklistItems] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_ExitChecklistTemplateItems_OrganizationId] ON [ExitChecklistTemplateItems] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_ExitRequests_EmployeeId] ON [ExitRequests] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_ExitRequests_OrganizationId_Status] ON [ExitRequests] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrganizationSettings_OrganizationId] ON [OrganizationSettings] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    ALTER TABLE [Departments] ADD CONSTRAINT [FK_Departments_Employees_HeadEmployeeId] FOREIGN KEY ([HeadEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112020634_CoreHr', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    ALTER TABLE [OrganizationSettings] ADD [LatePoints] int NOT NULL DEFAULT 3;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    ALTER TABLE [OrganizationSettings] ADD [OnTimePoints] int NOT NULL DEFAULT 10;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [AttendancePunches] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [TimestampUtc] datetime2 NOT NULL,
        [AttendanceDate] date NOT NULL,
        [Direction] nvarchar(40) NOT NULL,
        [Source] nvarchar(40) NOT NULL,
        [IpAddress] nvarchar(64) NULL,
        [Note] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_AttendancePunches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AttendancePunches_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AttendancePunches_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [Holidays] (
        [Id] int NOT NULL IDENTITY,
        [Date] date NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [IsOptional] bit NOT NULL,
        [BranchId] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Holidays] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Holidays_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Holidays_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [LeaveTypes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Code] nvarchar(10) NOT NULL,
        [Color] nvarchar(7) NOT NULL,
        [IsPaid] bit NOT NULL,
        [AnnualQuota] decimal(6,2) NOT NULL,
        [AccrualMethod] nvarchar(40) NOT NULL,
        [CarryForwardLimit] decimal(6,2) NOT NULL,
        [MaxEncashableDaysPerYear] decimal(6,2) NOT NULL,
        [AllowHalfDay] bit NOT NULL,
        [MinNoticeDays] int NOT NULL,
        [ApplicableGender] nvarchar(40) NULL,
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
        CONSTRAINT [PK_LeaveTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LeaveTypes_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [Link] nvarchar(300) NULL,
        [IsRead] bit NOT NULL,
        [ReadAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [OvertimeRequests] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Date] date NOT NULL,
        [Minutes] int NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ApproverUserId] int NULL,
        [ApprovedMinutes] int NULL,
        [DecidedBy] nvarchar(200) NULL,
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
        CONSTRAINT [PK_OvertimeRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OvertimeRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OvertimeRequests_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OvertimeRequests_Users_ApproverUserId] FOREIGN KEY ([ApproverUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [Shifts] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Code] nvarchar(10) NOT NULL,
        [StartTime] time NOT NULL,
        [EndTime] time NOT NULL,
        [BreakMinutes] int NOT NULL,
        [GraceMinutes] int NOT NULL,
        [MinHoursFullDay] decimal(4,2) NOT NULL,
        [MinHoursHalfDay] decimal(4,2) NOT NULL,
        [OvertimeThresholdMinutes] int NOT NULL,
        [WeeklyOffs] nvarchar(100) NOT NULL,
        [IsDefault] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Shifts_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [LeaveApplications] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [LeaveTypeId] int NOT NULL,
        [FromDate] date NOT NULL,
        [ToDate] date NOT NULL,
        [IsHalfDay] bit NOT NULL,
        [HalfDaySession] nvarchar(40) NULL,
        [Days] decimal(6,2) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ApproverUserId] int NULL,
        [DecidedBy] nvarchar(200) NULL,
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
        CONSTRAINT [PK_LeaveApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LeaveApplications_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LeaveApplications_LeaveTypes_LeaveTypeId] FOREIGN KEY ([LeaveTypeId]) REFERENCES [LeaveTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LeaveApplications_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LeaveApplications_Users_ApproverUserId] FOREIGN KEY ([ApproverUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [LeaveBalances] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [LeaveTypeId] int NOT NULL,
        [Year] int NOT NULL,
        [Opening] decimal(6,2) NOT NULL,
        [Accrued] decimal(6,2) NOT NULL,
        [Adjusted] decimal(6,2) NOT NULL,
        [Used] decimal(6,2) NOT NULL,
        [Encashed] decimal(6,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_LeaveBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LeaveBalances_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LeaveBalances_LeaveTypes_LeaveTypeId] FOREIGN KEY ([LeaveTypeId]) REFERENCES [LeaveTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LeaveBalances_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [Attendance] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Date] date NOT NULL,
        [ShiftId] int NULL,
        [FirstInUtc] datetime2 NULL,
        [LastOutUtc] datetime2 NULL,
        [WorkedMinutes] int NOT NULL,
        [LateMinutes] int NOT NULL,
        [OvertimeMinutes] int NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [IsRegularized] bit NOT NULL,
        [Remarks] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Attendance] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Attendance_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Attendance_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Attendance_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [ShiftAssignments] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [ShiftId] int NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ShiftAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShiftAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShiftAssignments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShiftAssignments_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Attendance_EmployeeId_Date] ON [Attendance] ([EmployeeId], [Date]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_Attendance_OrganizationId_Date] ON [Attendance] ([OrganizationId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_Attendance_ShiftId] ON [Attendance] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_AttendancePunches_EmployeeId_AttendanceDate] ON [AttendancePunches] ([EmployeeId], [AttendanceDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_AttendancePunches_OrganizationId] ON [AttendancePunches] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_Holidays_BranchId] ON [Holidays] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_Holidays_OrganizationId_Date] ON [Holidays] ([OrganizationId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_LeaveApplications_ApproverUserId_Status] ON [LeaveApplications] ([ApproverUserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_LeaveApplications_EmployeeId_FromDate_ToDate] ON [LeaveApplications] ([EmployeeId], [FromDate], [ToDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_LeaveApplications_LeaveTypeId] ON [LeaveApplications] ([LeaveTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_LeaveApplications_OrganizationId_Status] ON [LeaveApplications] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_LeaveBalances_EmployeeId_LeaveTypeId_Year] ON [LeaveBalances] ([EmployeeId], [LeaveTypeId], [Year]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_LeaveBalances_LeaveTypeId] ON [LeaveBalances] ([LeaveTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_LeaveBalances_OrganizationId] ON [LeaveBalances] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_LeaveTypes_OrganizationId_Code] ON [LeaveTypes] ([OrganizationId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_Notifications_OrganizationId] ON [Notifications] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead] ON [Notifications] ([UserId], [IsRead]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_OvertimeRequests_ApproverUserId] ON [OvertimeRequests] ([ApproverUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_OvertimeRequests_EmployeeId_Date] ON [OvertimeRequests] ([EmployeeId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_OvertimeRequests_OrganizationId_Status] ON [OvertimeRequests] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_ShiftAssignments_EmployeeId_EffectiveFrom] ON [ShiftAssignments] ([EmployeeId], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_ShiftAssignments_OrganizationId] ON [ShiftAssignments] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_ShiftAssignments_ShiftId] ON [ShiftAssignments] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Shifts_OrganizationId_Code] ON [Shifts] ([OrganizationId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112023952_TimeAttendance', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    ALTER TABLE [LeaveBalances] ADD [EncashmentPaid] decimal(6,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [EmployeeLoans] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Type] nvarchar(40) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [InstallmentAmount] decimal(18,2) NOT NULL,
        [RecoveryStart] date NOT NULL,
        [Outstanding] decimal(18,2) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [DecidedBy] nvarchar(200) NULL,
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
        CONSTRAINT [PK_EmployeeLoans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeLoans_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeLoans_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [FullAndFinalSettlements] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [LastWorkingDay] date NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [TotalPayable] decimal(18,2) NOT NULL,
        [TotalRecoverable] decimal(18,2) NOT NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [ApprovedBy] nvarchar(200) NULL,
        [ApprovedAtUtc] datetime2 NULL,
        [PaidOn] date NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_FullAndFinalSettlements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FullAndFinalSettlements_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FullAndFinalSettlements_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [IncomeTaxRules] (
        [Id] int NOT NULL IDENTITY,
        [FinancialYear] int NOT NULL,
        [Regime] nvarchar(40) NOT NULL,
        [StandardDeduction] decimal(18,2) NOT NULL,
        [RebateIncomeLimit] decimal(18,2) NOT NULL,
        [RebateMaxAmount] decimal(18,2) NOT NULL,
        [CessRate] decimal(9,4) NOT NULL,
        [Section80CLimit] decimal(18,2) NOT NULL,
        [Section80DLimit] decimal(18,2) NOT NULL,
        [Section80CCD1BLimit] decimal(18,2) NOT NULL,
        [HomeLoanInterestLimit] decimal(18,2) NOT NULL,
        [IsVerified] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_IncomeTaxRules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [LabourWelfareFundRules] (
        [Id] int NOT NULL IDENTITY,
        [State] nvarchar(100) NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EmployeeAmount] decimal(18,2) NOT NULL,
        [EmployerAmount] decimal(18,2) NOT NULL,
        [Months] nvarchar(40) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_LabourWelfareFundRules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [PayrollRuns] (
        [Id] int NOT NULL IDENTITY,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ProcessedAtUtc] datetime2 NULL,
        [FinalizedAtUtc] datetime2 NULL,
        [FinalizedBy] nvarchar(200) NULL,
        [PaidOn] date NULL,
        [EmployeeCount] int NOT NULL,
        [TotalGross] decimal(18,2) NOT NULL,
        [TotalDeductions] decimal(18,2) NOT NULL,
        [TotalNet] decimal(18,2) NOT NULL,
        [TotalEmployerCost] decimal(18,2) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PayrollRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayrollRuns_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [PayrollSettings] (
        [Id] int NOT NULL IDENTITY,
        [PfEnabled] bit NOT NULL,
        [PfRestrictToCeiling] bit NOT NULL,
        [EsiEnabled] bit NOT NULL,
        [PtEnabled] bit NOT NULL,
        [LwfEnabled] bit NOT NULL,
        [TdsEnabled] bit NOT NULL,
        [DefaultState] nvarchar(100) NULL,
        [PfEstablishmentCode] nvarchar(30) NULL,
        [EsiEmployerCode] nvarchar(30) NULL,
        [Tan] nvarchar(10) NULL,
        [OvertimeRateMultiplier] decimal(9,4) NOT NULL,
        [DailyRateDivisor] decimal(9,4) NOT NULL,
        [PayDay] int NOT NULL,
        [SalaryExpenseAccount] nvarchar(100) NOT NULL,
        [EmployerContributionAccount] nvarchar(100) NOT NULL,
        [SalaryPayableAccount] nvarchar(100) NOT NULL,
        [PfPayableAccount] nvarchar(100) NOT NULL,
        [EsiPayableAccount] nvarchar(100) NOT NULL,
        [PtPayableAccount] nvarchar(100) NOT NULL,
        [LwfPayableAccount] nvarchar(100) NOT NULL,
        [TdsPayableAccount] nvarchar(100) NOT NULL,
        [LoanRecoveryAccount] nvarchar(100) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PayrollSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayrollSettings_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [ProfessionalTaxSlabs] (
        [Id] int NOT NULL IDENTITY,
        [State] nvarchar(100) NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [Gender] nvarchar(40) NULL,
        [MinMonthlyGross] decimal(18,2) NOT NULL,
        [MaxMonthlyGross] decimal(18,2) NULL,
        [MonthlyAmount] decimal(18,2) NOT NULL,
        [SpecialMonth] int NULL,
        [SpecialMonthAmount] decimal(18,2) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ProfessionalTaxSlabs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [SalaryComponents] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Type] nvarchar(40) NOT NULL,
        [IsTaxable] bit NOT NULL,
        [IsPfWage] bit NOT NULL,
        [IsEsiWage] bit NOT NULL,
        [IsProrated] bit NOT NULL,
        [IsHra] bit NOT NULL,
        [IsBasic] bit NOT NULL,
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
        CONSTRAINT [PK_SalaryComponents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalaryComponents_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [SalaryStructures] (
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
        CONSTRAINT [PK_SalaryStructures] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalaryStructures_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [StatutoryRateSets] (
        [Id] int NOT NULL IDENTITY,
        [EffectiveFrom] date NOT NULL,
        [PfEmployeeRate] decimal(9,4) NOT NULL,
        [PfEmployerRate] decimal(9,4) NOT NULL,
        [EpsRate] decimal(9,4) NOT NULL,
        [PfWageCeiling] decimal(18,2) NOT NULL,
        [EdliRate] decimal(9,4) NOT NULL,
        [PfAdminChargesRate] decimal(9,4) NOT NULL,
        [EsiEmployeeRate] decimal(9,4) NOT NULL,
        [EsiEmployerRate] decimal(9,4) NOT NULL,
        [EsiWageLimit] decimal(18,2) NOT NULL,
        [GratuityMinServiceYears] decimal(9,4) NOT NULL,
        [GratuityDaysPerYear] decimal(9,4) NOT NULL,
        [GratuityWageDivisor] decimal(9,4) NOT NULL,
        [GratuityCeiling] decimal(18,2) NOT NULL,
        [IsVerified] bit NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_StatutoryRateSets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [TaxDeclarations] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [FinancialYear] int NOT NULL,
        [Regime] nvarchar(40) NOT NULL,
        [Section80C] decimal(18,2) NOT NULL,
        [Section80D] decimal(18,2) NOT NULL,
        [Section80CCD1B] decimal(18,2) NOT NULL,
        [HomeLoanInterest] decimal(18,2) NOT NULL,
        [OtherDeductions] decimal(18,2) NOT NULL,
        [MonthlyRentPaid] decimal(18,2) NOT NULL,
        [MetroCity] bit NOT NULL,
        [PreviousEmployerIncome] decimal(18,2) NOT NULL,
        [PreviousEmployerTds] decimal(18,2) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ReviewedBy] nvarchar(200) NULL,
        [ReviewedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_TaxDeclarations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaxDeclarations_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaxDeclarations_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [SettlementLines] (
        [Id] int NOT NULL IDENTITY,
        [FullAndFinalSettlementId] int NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [IsManual] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SettlementLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SettlementLines_FullAndFinalSettlements_FullAndFinalSettlementId] FOREIGN KEY ([FullAndFinalSettlementId]) REFERENCES [FullAndFinalSettlements] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SettlementLines_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [IncomeTaxSlabs] (
        [Id] int NOT NULL IDENTITY,
        [IncomeTaxRuleId] int NOT NULL,
        [FromIncome] decimal(18,2) NOT NULL,
        [ToIncome] decimal(18,2) NULL,
        [Rate] decimal(9,4) NOT NULL,
        CONSTRAINT [PK_IncomeTaxSlabs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IncomeTaxSlabs_IncomeTaxRules_IncomeTaxRuleId] FOREIGN KEY ([IncomeTaxRuleId]) REFERENCES [IncomeTaxRules] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [IncomeTaxSurcharges] (
        [Id] int NOT NULL IDENTITY,
        [IncomeTaxRuleId] int NOT NULL,
        [AboveIncome] decimal(18,2) NOT NULL,
        [Rate] decimal(9,4) NOT NULL,
        CONSTRAINT [PK_IncomeTaxSurcharges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IncomeTaxSurcharges_IncomeTaxRules_IncomeTaxRuleId] FOREIGN KEY ([IncomeTaxRuleId]) REFERENCES [IncomeTaxRules] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [LoanRepayments] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeLoanId] int NOT NULL,
        [Date] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PayrollRunId] int NULL,
        [Note] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_LoanRepayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LoanRepayments_EmployeeLoans_EmployeeLoanId] FOREIGN KEY ([EmployeeLoanId]) REFERENCES [EmployeeLoans] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LoanRepayments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LoanRepayments_PayrollRuns_PayrollRunId] FOREIGN KEY ([PayrollRunId]) REFERENCES [PayrollRuns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [PayrollDetails] (
        [Id] int NOT NULL IDENTITY,
        [PayrollRunId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [DaysInMonth] int NOT NULL,
        [PaidDays] decimal(6,2) NOT NULL,
        [LopDays] decimal(6,2) NOT NULL,
        [LopOverride] decimal(6,2) NULL,
        [OnHold] bit NOT NULL,
        [HoldReason] nvarchar(300) NULL,
        [MonthlyGross] decimal(18,2) NOT NULL,
        [GrossEarnings] decimal(18,2) NOT NULL,
        [TotalDeductions] decimal(18,2) NOT NULL,
        [NetPay] decimal(18,2) NOT NULL,
        [EmployerContributions] decimal(18,2) NOT NULL,
        [PfWages] decimal(18,2) NOT NULL,
        [EsiWages] decimal(18,2) NOT NULL,
        [TaxableEarnings] decimal(18,2) NOT NULL,
        [ProjectedAnnualTax] decimal(18,2) NOT NULL,
        [LeaveEncashmentDays] decimal(6,2) NOT NULL,
        [Regime] nvarchar(40) NOT NULL,
        [State] nvarchar(100) NULL,
        [Warnings] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PayrollDetails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayrollDetails_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayrollDetails_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayrollDetails_PayrollRuns_PayrollRunId] FOREIGN KEY ([PayrollRunId]) REFERENCES [PayrollRuns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [EmployeeSalaries] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [SalaryStructureId] int NOT NULL,
        [AnnualGross] decimal(18,2) NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [PfApplicable] bit NOT NULL,
        [Remarks] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_EmployeeSalaries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeSalaries_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeSalaries_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeSalaries_SalaryStructures_SalaryStructureId] FOREIGN KEY ([SalaryStructureId]) REFERENCES [SalaryStructures] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [SalaryStructureLines] (
        [Id] int NOT NULL IDENTITY,
        [SalaryStructureId] int NOT NULL,
        [SalaryComponentId] int NOT NULL,
        [Calculation] nvarchar(40) NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SalaryStructureLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalaryStructureLines_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalaryStructureLines_SalaryComponents_SalaryComponentId] FOREIGN KEY ([SalaryComponentId]) REFERENCES [SalaryComponents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalaryStructureLines_SalaryStructures_SalaryStructureId] FOREIGN KEY ([SalaryStructureId]) REFERENCES [SalaryStructures] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [PayrollLines] (
        [Id] int NOT NULL IDENTITY,
        [PayrollDetailId] int NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Kind] nvarchar(40) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [FullAmount] decimal(18,2) NULL,
        [IsTaxable] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PayrollLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayrollLines_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayrollLines_PayrollDetails_PayrollDetailId] FOREIGN KEY ([PayrollDetailId]) REFERENCES [PayrollDetails] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeLoans_EmployeeId] ON [EmployeeLoans] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeLoans_OrganizationId_Status] ON [EmployeeLoans] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeSalaries_EmployeeId_EffectiveFrom] ON [EmployeeSalaries] ([EmployeeId], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeSalaries_OrganizationId] ON [EmployeeSalaries] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeSalaries_SalaryStructureId] ON [EmployeeSalaries] ([SalaryStructureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_FullAndFinalSettlements_EmployeeId] ON [FullAndFinalSettlements] ([EmployeeId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_FullAndFinalSettlements_OrganizationId] ON [FullAndFinalSettlements] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE UNIQUE INDEX [IX_IncomeTaxRules_FinancialYear_Regime] ON [IncomeTaxRules] ([FinancialYear], [Regime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_IncomeTaxSlabs_IncomeTaxRuleId] ON [IncomeTaxSlabs] ([IncomeTaxRuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_IncomeTaxSurcharges_IncomeTaxRuleId] ON [IncomeTaxSurcharges] ([IncomeTaxRuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_LabourWelfareFundRules_State_EffectiveFrom] ON [LabourWelfareFundRules] ([State], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_LoanRepayments_EmployeeLoanId] ON [LoanRepayments] ([EmployeeLoanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_LoanRepayments_OrganizationId] ON [LoanRepayments] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_LoanRepayments_PayrollRunId] ON [LoanRepayments] ([PayrollRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_PayrollDetails_EmployeeId_PayrollRunId] ON [PayrollDetails] ([EmployeeId], [PayrollRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_PayrollDetails_OrganizationId] ON [PayrollDetails] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PayrollDetails_PayrollRunId_EmployeeId] ON [PayrollDetails] ([PayrollRunId], [EmployeeId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_PayrollLines_OrganizationId] ON [PayrollLines] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_PayrollLines_PayrollDetailId] ON [PayrollLines] ([PayrollDetailId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PayrollRuns_OrganizationId_Year_Month] ON [PayrollRuns] ([OrganizationId], [Year], [Month]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PayrollSettings_OrganizationId] ON [PayrollSettings] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_ProfessionalTaxSlabs_State_EffectiveFrom] ON [ProfessionalTaxSlabs] ([State], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SalaryComponents_OrganizationId_Code] ON [SalaryComponents] ([OrganizationId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_SalaryStructureLines_OrganizationId] ON [SalaryStructureLines] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_SalaryStructureLines_SalaryComponentId] ON [SalaryStructureLines] ([SalaryComponentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_SalaryStructureLines_SalaryStructureId] ON [SalaryStructureLines] ([SalaryStructureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_SalaryStructures_OrganizationId] ON [SalaryStructures] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_SettlementLines_FullAndFinalSettlementId] ON [SettlementLines] ([FullAndFinalSettlementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_SettlementLines_OrganizationId] ON [SettlementLines] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StatutoryRateSets_EffectiveFrom] ON [StatutoryRateSets] ([EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TaxDeclarations_EmployeeId_FinancialYear] ON [TaxDeclarations] ([EmployeeId], [FinancialYear]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_TaxDeclarations_OrganizationId] ON [TaxDeclarations] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112032206_Payroll', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [DocumentSequences] (
        [Id] int NOT NULL IDENTITY,
        [DocumentType] nvarchar(20) NOT NULL,
        [FinancialYear] int NOT NULL,
        [NextNumber] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_DocumentSequences] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [EmailLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [CreatedAtUtc] datetime2 NOT NULL,
        [OrganizationId] int NULL,
        [To] nvarchar(200) NOT NULL,
        [Subject] nvarchar(300) NOT NULL,
        [Category] nvarchar(50) NOT NULL,
        [Succeeded] bit NOT NULL,
        [Error] nvarchar(1000) NULL,
        CONSTRAINT [PK_EmailLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [Invoices] (
        [Id] int NOT NULL IDENTITY,
        [Number] nvarchar(16) NOT NULL,
        [OrganizationId] int NOT NULL,
        [Purpose] nvarchar(40) NOT NULL,
        [PlanId] int NULL,
        [BillingCycle] nvarchar(40) NULL,
        [Seats] int NULL,
        [InvoiceDate] date NOT NULL,
        [DueDate] date NOT NULL,
        [PeriodStart] date NULL,
        [PeriodEnd] date NULL,
        [Status] nvarchar(40) NOT NULL,
        [Fulfilled] bit NOT NULL,
        [BillToName] nvarchar(200) NOT NULL,
        [BillToGstin] nvarchar(15) NULL,
        [BillToAddress] nvarchar(500) NULL,
        [BillToState] nvarchar(100) NULL,
        [BillToStateCode] nvarchar(2) NULL,
        [SupplierStateCode] nvarchar(2) NOT NULL,
        [PlaceOfSupply] nvarchar(120) NOT NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [CgstRate] decimal(5,2) NOT NULL,
        [CgstAmount] decimal(18,2) NOT NULL,
        [SgstRate] decimal(5,2) NOT NULL,
        [SgstAmount] decimal(18,2) NOT NULL,
        [IgstRate] decimal(5,2) NOT NULL,
        [IgstAmount] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [AmountPaid] decimal(18,2) NOT NULL,
        [AmountCredited] decimal(18,2) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CancelReason] nvarchar(500) NULL,
        [LastReminderAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Invoices_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoices_SubscriptionPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [SubscriptionPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [PlatformSettings] (
        [Id] int NOT NULL IDENTITY,
        [LegalName] nvarchar(200) NOT NULL,
        [Gstin] nvarchar(15) NULL,
        [Pan] nvarchar(10) NULL,
        [AddressLine] nvarchar(300) NULL,
        [City] nvarchar(100) NULL,
        [State] nvarchar(100) NOT NULL,
        [PostalCode] nvarchar(10) NULL,
        [BillingEmail] nvarchar(200) NULL,
        [Phone] nvarchar(20) NULL,
        [InvoicePrefix] nvarchar(6) NOT NULL,
        [CreditNotePrefix] nvarchar(6) NOT NULL,
        [SacCode] nvarchar(8) NOT NULL,
        [GstRate] decimal(5,2) NOT NULL,
        [InvoiceDueDays] int NOT NULL,
        [RenewalInvoiceDaysBefore] int NOT NULL,
        [ReminderSchedule] nvarchar(60) NOT NULL,
        [BankDetails] nvarchar(500) NULL,
        [RazorpayEnabled] bit NOT NULL,
        [RazorpayKeyId] nvarchar(100) NULL,
        [RazorpayKeySecretProtected] nvarchar(1000) NULL,
        [RazorpayWebhookSecretProtected] nvarchar(1000) NULL,
        [SmtpEnabled] bit NOT NULL,
        [SmtpHost] nvarchar(200) NULL,
        [SmtpPort] int NOT NULL,
        [SmtpUseSsl] bit NOT NULL,
        [SmtpUsername] nvarchar(200) NULL,
        [SmtpPasswordProtected] nvarchar(1000) NULL,
        [SmtpFromEmail] nvarchar(200) NULL,
        [SmtpFromName] nvarchar(100) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PlatformSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [InvoiceLines] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [Description] nvarchar(300) NOT NULL,
        [SacCode] nvarchar(8) NOT NULL,
        [Quantity] decimal(12,2) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_InvoiceLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvoiceLines_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Method] nvarchar(40) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Reference] nvarchar(100) NULL,
        [PaidAtUtc] datetime2 NULL,
        [GatewayOrderId] nvarchar(60) NULL,
        [GatewayPaymentId] nvarchar(60) NULL,
        [FailureReason] nvarchar(300) NULL,
        [RefundedAmount] decimal(18,2) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE TABLE [CreditNotes] (
        [Id] int NOT NULL IDENTITY,
        [Number] nvarchar(16) NOT NULL,
        [InvoiceId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        [Date] date NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [TaxableAmount] decimal(18,2) NOT NULL,
        [CgstAmount] decimal(18,2) NOT NULL,
        [SgstAmount] decimal(18,2) NOT NULL,
        [IgstAmount] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [RefundAmount] decimal(18,2) NOT NULL,
        [RefundPaymentId] int NULL,
        [GatewayRefundId] nvarchar(60) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_CreditNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreditNotes_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CreditNotes_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CreditNotes_Payments_RefundPaymentId] FOREIGN KEY ([RefundPaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_CreditNotes_InvoiceId] ON [CreditNotes] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CreditNotes_Number] ON [CreditNotes] ([Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_CreditNotes_OrganizationId] ON [CreditNotes] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_CreditNotes_RefundPaymentId] ON [CreditNotes] ([RefundPaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DocumentSequences_DocumentType_FinancialYear] ON [DocumentSequences] ([DocumentType], [FinancialYear]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_EmailLogs_CreatedAtUtc] ON [EmailLogs] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_InvoiceLines_InvoiceId] ON [InvoiceLines] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Invoices_Number] ON [Invoices] ([Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Invoices_OrganizationId_Status] ON [Invoices] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Invoices_PlanId] ON [Invoices] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Invoices_Status_DueDate] ON [Invoices] ([Status], [DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Payments_GatewayOrderId] ON [Payments] ([GatewayOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_GatewayPaymentId] ON [Payments] ([GatewayPaymentId]) WHERE [GatewayPaymentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Payments_InvoiceId] ON [Payments] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Payments_OrganizationId] ON [Payments] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    CREATE INDEX [IX_Payments_Status_PaidAtUtc] ON [Payments] ([Status], [PaidAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112035629_Billing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112035629_Billing', N'9.0.10');
END;

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

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112051112_Letters'
)
BEGIN
    CREATE TABLE [LetterTemplates] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Kind] nvarchar(40) NOT NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_LetterTemplates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LetterTemplates_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112051112_Letters'
)
BEGIN
    CREATE TABLE [GeneratedLetters] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [TemplateId] int NULL,
        [Kind] nvarchar(40) NOT NULL,
        [Number] nvarchar(40) NOT NULL,
        [FinancialYear] int NOT NULL,
        [Sequence] int NOT NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [IssuedOn] date NOT NULL,
        [IssuedByName] nvarchar(150) NOT NULL,
        [SignatoryName] nvarchar(150) NULL,
        [SignatoryDesignation] nvarchar(150) NULL,
        [DocumentId] int NOT NULL,
        [SharedWithEmployee] bit NOT NULL,
        [RevokedAtUtc] datetime2 NULL,
        [RevokedReason] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_GeneratedLetters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GeneratedLetters_EmployeeDocuments_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [EmployeeDocuments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GeneratedLetters_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GeneratedLetters_LetterTemplates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [LetterTemplates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GeneratedLetters_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112051112_Letters'
)
BEGIN
    CREATE INDEX [IX_GeneratedLetters_DocumentId] ON [GeneratedLetters] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112051112_Letters'
)
BEGIN
    CREATE INDEX [IX_GeneratedLetters_EmployeeId] ON [GeneratedLetters] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112051112_Letters'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GeneratedLetters_OrganizationId_FinancialYear_Sequence] ON [GeneratedLetters] ([OrganizationId], [FinancialYear], [Sequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112051112_Letters'
)
BEGIN
    CREATE INDEX [IX_GeneratedLetters_TemplateId] ON [GeneratedLetters] ([TemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112051112_Letters'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_LetterTemplates_OrganizationId_Name] ON [LetterTemplates] ([OrganizationId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112051112_Letters'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112051112_Letters', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE TABLE [ExpenseCategories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(80) NOT NULL,
        [MaxPerItem] decimal(18,2) NULL,
        [ReceiptRequired] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ExpenseCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpenseCategories_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE TABLE [PayrollAdjustments] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [Category] nvarchar(40) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [IsTaxable] bit NOT NULL,
        [Notes] nvarchar(300) NULL,
        [Source] nvarchar(40) NOT NULL,
        [SourceId] int NULL,
        [CoversFrom] date NULL,
        [CoversTo] date NULL,
        [SettledRunId] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PayrollAdjustments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayrollAdjustments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayrollAdjustments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayrollAdjustments_PayrollRuns_SettledRunId] FOREIGN KEY ([SettledRunId]) REFERENCES [PayrollRuns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE TABLE [ExpenseClaims] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [ManagerUserId] int NULL,
        [ManagerDecisionBy] nvarchar(150) NULL,
        [ManagerDecidedAtUtc] datetime2 NULL,
        [DecidedBy] nvarchar(150) NULL,
        [DecidedAtUtc] datetime2 NULL,
        [DecisionNotes] nvarchar(500) NULL,
        [Payout] nvarchar(40) NULL,
        [AdjustmentId] int NULL,
        [PaidOn] date NULL,
        [PaymentReference] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ExpenseClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpenseClaims_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExpenseClaims_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExpenseClaims_PayrollAdjustments_AdjustmentId] FOREIGN KEY ([AdjustmentId]) REFERENCES [PayrollAdjustments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExpenseClaims_Users_ManagerUserId] FOREIGN KEY ([ManagerUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE TABLE [ExpenseItems] (
        [Id] int NOT NULL IDENTITY,
        [ClaimId] int NOT NULL,
        [CategoryId] int NOT NULL,
        [Date] date NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ReceiptKey] nvarchar(300) NULL,
        [ReceiptFileName] nvarchar(200) NULL,
        [ReceiptContentType] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ExpenseItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpenseItems_ExpenseCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [ExpenseCategories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExpenseItems_ExpenseClaims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [ExpenseClaims] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExpenseItems_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ExpenseCategories_OrganizationId_Name] ON [ExpenseCategories] ([OrganizationId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseClaims_AdjustmentId] ON [ExpenseClaims] ([AdjustmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseClaims_EmployeeId] ON [ExpenseClaims] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseClaims_ManagerUserId_Status] ON [ExpenseClaims] ([ManagerUserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseClaims_OrganizationId_Status] ON [ExpenseClaims] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseItems_CategoryId] ON [ExpenseItems] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseItems_ClaimId] ON [ExpenseItems] ([ClaimId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_ExpenseItems_OrganizationId] ON [ExpenseItems] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_PayrollAdjustments_EmployeeId_SettledRunId] ON [PayrollAdjustments] ([EmployeeId], [SettledRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_PayrollAdjustments_OrganizationId_Year_Month] ON [PayrollAdjustments] ([OrganizationId], [Year], [Month]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    CREATE INDEX [IX_PayrollAdjustments_SettledRunId] ON [PayrollAdjustments] ([SettledRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112054051_PayrollExtras'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112054051_PayrollExtras', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [OrganizationSettings] ADD [GeoMode] nvarchar(20) NOT NULL DEFAULT N'Off';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [Employees] ADD [GeoFenceExempt] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD [AccuracyMeters] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD [DistanceMeters] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD [Latitude] decimal(9,6) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD [LocationStatus] nvarchar(20) NOT NULL DEFAULT N'NotCaptured';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD [Longitude] decimal(9,6) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD [OfficeLocationId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [Assets] (
        [Id] int NOT NULL IDENTITY,
        [AssetTag] nvarchar(40) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Category] nvarchar(60) NOT NULL,
        [SerialNumber] nvarchar(100) NULL,
        [Description] nvarchar(500) NULL,
        [PurchaseDate] date NULL,
        [PurchaseCost] decimal(18,2) NULL,
        [WarrantyUntil] date NULL,
        [Status] nvarchar(40) NOT NULL,
        [Condition] nvarchar(40) NOT NULL,
        [CurrentEmployeeId] int NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Assets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Assets_Employees_CurrentEmployeeId] FOREIGN KEY ([CurrentEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Assets_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [FeedbackThreads] (
        [Id] int NOT NULL IDENTITY,
        [Category] nvarchar(60) NOT NULL,
        [Subject] nvarchar(150) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [AccessCodeHash] nvarchar(64) NOT NULL,
        [CreatedOn] date NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_FeedbackThreads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FeedbackThreads_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [OfficeLocations] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [BranchId] int NULL,
        [Latitude] decimal(9,6) NOT NULL,
        [Longitude] decimal(9,6) NOT NULL,
        [RadiusMeters] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_OfficeLocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OfficeLocations_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OfficeLocations_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [Surveys] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(150) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Kind] nvarchar(40) NOT NULL,
        [IsAnonymous] bit NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [DepartmentId] int NULL,
        [DurationDays] int NOT NULL,
        [OpenedAtUtc] datetime2 NULL,
        [ClosesAtUtc] datetime2 NULL,
        [MinResponses] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Surveys] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Surveys_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Surveys_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [AssetAssignments] (
        [Id] int NOT NULL IDENTITY,
        [AssetId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [AssignedOn] date NOT NULL,
        [ExpectedReturnOn] date NULL,
        [ConditionOut] nvarchar(40) NOT NULL,
        [AssignedBy] nvarchar(150) NOT NULL,
        [AcknowledgedAtUtc] datetime2 NULL,
        [ReturnedOn] date NULL,
        [ConditionIn] nvarchar(40) NULL,
        [ReturnedTo] nvarchar(150) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_AssetAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssetAssignments_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AssetAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AssetAssignments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [FeedbackMessages] (
        [Id] int NOT NULL IDENTITY,
        [ThreadId] int NOT NULL,
        [FromHr] bit NOT NULL,
        [AuthorName] nvarchar(150) NULL,
        [Body] nvarchar(4000) NOT NULL,
        [SentOn] date NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_FeedbackMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FeedbackMessages_FeedbackThreads_ThreadId] FOREIGN KEY ([ThreadId]) REFERENCES [FeedbackThreads] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FeedbackMessages_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [SurveyParticipations] (
        [SurveyId] int NOT NULL,
        [UserId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SurveyParticipations] PRIMARY KEY ([SurveyId], [UserId]),
        CONSTRAINT [FK_SurveyParticipations_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveyParticipations_Surveys_SurveyId] FOREIGN KEY ([SurveyId]) REFERENCES [Surveys] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveyParticipations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [SurveyQuestions] (
        [Id] int NOT NULL IDENTITY,
        [SurveyId] int NOT NULL,
        [SortOrder] int NOT NULL,
        [Text] nvarchar(300) NOT NULL,
        [Type] nvarchar(40) NOT NULL,
        [Options] nvarchar(1000) NULL,
        [Required] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SurveyQuestions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SurveyQuestions_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveyQuestions_Surveys_SurveyId] FOREIGN KEY ([SurveyId]) REFERENCES [Surveys] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [SurveyResponses] (
        [Id] int NOT NULL IDENTITY,
        [SurveyId] int NOT NULL,
        [RespondentUserId] int NULL,
        [RespondentName] nvarchar(150) NULL,
        [SubmittedOn] date NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SurveyResponses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SurveyResponses_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveyResponses_Surveys_SurveyId] FOREIGN KEY ([SurveyId]) REFERENCES [Surveys] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [SurveyAnswers] (
        [Id] int NOT NULL IDENTITY,
        [ResponseId] int NOT NULL,
        [QuestionId] int NOT NULL,
        [Number] int NULL,
        [Text] nvarchar(2000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SurveyAnswers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SurveyAnswers_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveyAnswers_SurveyQuestions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [SurveyQuestions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveyAnswers_SurveyResponses_ResponseId] FOREIGN KEY ([ResponseId]) REFERENCES [SurveyResponses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_AttendancePunches_OfficeLocationId] ON [AttendancePunches] ([OfficeLocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_AssetAssignments_AssetId] ON [AssetAssignments] ([AssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_AssetAssignments_EmployeeId_ReturnedOn] ON [AssetAssignments] ([EmployeeId], [ReturnedOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_AssetAssignments_OrganizationId] ON [AssetAssignments] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_Assets_CurrentEmployeeId] ON [Assets] ([CurrentEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Assets_OrganizationId_AssetTag] ON [Assets] ([OrganizationId], [AssetTag]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_Assets_OrganizationId_Status] ON [Assets] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_FeedbackMessages_OrganizationId] ON [FeedbackMessages] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_FeedbackMessages_ThreadId] ON [FeedbackMessages] ([ThreadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FeedbackThreads_AccessCodeHash] ON [FeedbackThreads] ([AccessCodeHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_FeedbackThreads_OrganizationId] ON [FeedbackThreads] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_OfficeLocations_BranchId] ON [OfficeLocations] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_OfficeLocations_OrganizationId] ON [OfficeLocations] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyAnswers_OrganizationId] ON [SurveyAnswers] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyAnswers_QuestionId] ON [SurveyAnswers] ([QuestionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyAnswers_ResponseId] ON [SurveyAnswers] ([ResponseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyParticipations_OrganizationId] ON [SurveyParticipations] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyParticipations_UserId] ON [SurveyParticipations] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyQuestions_OrganizationId] ON [SurveyQuestions] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyQuestions_SurveyId] ON [SurveyQuestions] ([SurveyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyResponses_OrganizationId] ON [SurveyResponses] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyResponses_SurveyId] ON [SurveyResponses] ([SurveyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_Surveys_DepartmentId] ON [Surveys] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_Surveys_OrganizationId_Status] ON [Surveys] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD CONSTRAINT [FK_AttendancePunches_OfficeLocations_OfficeLocationId] FOREIGN KEY ([OfficeLocationId]) REFERENCES [OfficeLocations] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112092632_Strength', N'9.0.10');
END;

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

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112142402_BrandDefaults'
)
BEGIN
    UPDATE [Organizations] SET [BrandColor] = '#1F5EFF' WHERE LOWER([BrandColor]) = '#0f766e';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112142402_BrandDefaults'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112142402_BrandDefaults', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113172924_PasswordResetTokens'
)
BEGIN
    CREATE TABLE [PasswordResetTokens] (
        [Id] bigint NOT NULL IDENTITY,
        [PrincipalType] nvarchar(20) NOT NULL,
        [OrganizationId] int NULL,
        [UserId] int NOT NULL,
        [TokenHash] varchar(64) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [UsedAtUtc] datetime2 NULL,
        [RequestIp] nvarchar(64) NULL,
        CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113172924_PasswordResetTokens'
)
BEGIN
    CREATE INDEX [IX_PasswordResetTokens_PrincipalType_UserId_CreatedAtUtc] ON [PasswordResetTokens] ([PrincipalType], [UserId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113172924_PasswordResetTokens'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PasswordResetTokens_TokenHash] ON [PasswordResetTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113172924_PasswordResetTokens'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261113172924_PasswordResetTokens', N'9.0.10');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113174254_LegalPages'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [GrievanceOfficerEmail] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113174254_LegalPages'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [GrievanceOfficerName] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113174254_LegalPages'
)
BEGIN
    ALTER TABLE [PlatformSettings] ADD [LegalJurisdictionCity] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113174254_LegalPages'
)
BEGIN
    ALTER TABLE [Organizations] ADD [TermsAcceptedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113174254_LegalPages'
)
BEGIN
    ALTER TABLE [Organizations] ADD [TermsAcceptedIp] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113174254_LegalPages'
)
BEGIN
    ALTER TABLE [Organizations] ADD [TermsAcceptedVersions] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113174254_LegalPages'
)
BEGIN
    CREATE TABLE [LegalPages] (
        [Id] int NOT NULL IDENTITY,
        [Slug] nvarchar(40) NOT NULL,
        [Title] nvarchar(120) NOT NULL,
        [Summary] nvarchar(300) NULL,
        [Body] nvarchar(max) NOT NULL,
        [IsPublished] bit NOT NULL,
        [EffectiveOn] date NOT NULL,
        [Version] int NOT NULL,
        [IsReviewed] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_LegalPages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113174254_LegalPages'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LegalPages_Slug] ON [LegalPages] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113174254_LegalPages'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261113174254_LegalPages', N'9.0.10');
END;

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

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE TABLE [CoreValues] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(80) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Icon] nvarchar(40) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_CoreValues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CoreValues_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE TABLE [Skills] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Category] nvarchar(60) NOT NULL,
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
        CONSTRAINT [PK_Skills] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Skills_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE TABLE [ValueShoutouts] (
        [Id] int NOT NULL IDENTITY,
        [CoreValueId] int NOT NULL,
        [FromEmployeeId] int NOT NULL,
        [ToEmployeeId] int NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [AtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ValueShoutouts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ValueShoutouts_CoreValues_CoreValueId] FOREIGN KEY ([CoreValueId]) REFERENCES [CoreValues] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ValueShoutouts_Employees_FromEmployeeId] FOREIGN KEY ([FromEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ValueShoutouts_Employees_ToEmployeeId] FOREIGN KEY ([ToEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ValueShoutouts_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE TABLE [EmployeeSkills] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [SkillId] int NOT NULL,
        [SelfLevel] int NULL,
        [ManagerLevel] int NULL,
        [ManagerNote] nvarchar(500) NULL,
        [AssessedByName] nvarchar(150) NULL,
        [SelfUpdatedAtUtc] datetime2 NULL,
        [AssessedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_EmployeeSkills] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeSkills_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeSkills_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeSkills_Skills_SkillId] FOREIGN KEY ([SkillId]) REFERENCES [Skills] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE TABLE [SkillRequirements] (
        [Id] int NOT NULL IDENTITY,
        [DesignationId] int NOT NULL,
        [SkillId] int NOT NULL,
        [RequiredLevel] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SkillRequirements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SkillRequirements_Designations_DesignationId] FOREIGN KEY ([DesignationId]) REFERENCES [Designations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SkillRequirements_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SkillRequirements_Skills_SkillId] FOREIGN KEY ([SkillId]) REFERENCES [Skills] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CoreValues_OrganizationId_Name] ON [CoreValues] ([OrganizationId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EmployeeSkills_EmployeeId_SkillId] ON [EmployeeSkills] ([EmployeeId], [SkillId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE INDEX [IX_EmployeeSkills_OrganizationId_SkillId] ON [EmployeeSkills] ([OrganizationId], [SkillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE INDEX [IX_EmployeeSkills_SkillId] ON [EmployeeSkills] ([SkillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SkillRequirements_DesignationId_SkillId] ON [SkillRequirements] ([DesignationId], [SkillId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE INDEX [IX_SkillRequirements_OrganizationId] ON [SkillRequirements] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE INDEX [IX_SkillRequirements_SkillId] ON [SkillRequirements] ([SkillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Skills_OrganizationId_Name] ON [Skills] ([OrganizationId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE INDEX [IX_ValueShoutouts_CoreValueId] ON [ValueShoutouts] ([CoreValueId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE INDEX [IX_ValueShoutouts_FromEmployeeId] ON [ValueShoutouts] ([FromEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE INDEX [IX_ValueShoutouts_OrganizationId_AtUtc] ON [ValueShoutouts] ([OrganizationId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE INDEX [IX_ValueShoutouts_OrganizationId_ToEmployeeId] ON [ValueShoutouts] ([OrganizationId], [ToEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    CREATE INDEX [IX_ValueShoutouts_ToEmployeeId] ON [ValueShoutouts] ([ToEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114082018_ValuesAndSkills'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261114082018_ValuesAndSkills', N'9.0.10');
END;

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

