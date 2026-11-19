BEGIN TRANSACTION;
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

COMMIT;
GO

