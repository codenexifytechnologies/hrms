BEGIN TRANSACTION;
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

COMMIT;
GO

