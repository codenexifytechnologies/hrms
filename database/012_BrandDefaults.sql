BEGIN TRANSACTION;
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

COMMIT;
GO

