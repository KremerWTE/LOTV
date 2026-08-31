-- Run this ONCE, directly against the ministry's existing SQL Server database
-- (10.100.1.87), before ever running `dotnet ef database update` against it.
--
-- Why: that database's schema was already created via EF's EnsureCreated()
-- plus a hand-written incremental script (see MASTER_TODO.md, 2026-07-27
-- session), not via migrations. This repo's new SQL-Server-specific
-- `InitialCreate` migration (src/Lotv.Migrations.SqlServer/Migrations)
-- reflects that same schema, so it must be marked as already-applied rather
-- than re-run - re-running it would try to CREATE TABLE over tables that
-- already exist and fail.
--
-- After this runs once, `dotnet ef database update` (and the CI deploy
-- workflows) will correctly apply only *future* migrations from here on.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '__EFMigrationsHistory')
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260831154533_InitialCreate')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260831154533_InitialCreate', '9.0.10');
END;
