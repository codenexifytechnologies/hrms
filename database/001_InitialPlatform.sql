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

COMMIT;
GO

