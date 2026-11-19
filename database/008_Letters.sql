BEGIN TRANSACTION;
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

COMMIT;
GO

