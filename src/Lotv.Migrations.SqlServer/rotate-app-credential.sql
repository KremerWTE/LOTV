-- Run this ONCE, directly against the ministry's SQL Server (10.100.1.87),
-- as an admin (e.g. still via `sa` for this one setup step). Creates a
-- dedicated least-privilege login for the app to use going forward, so the
-- app's connection string never needs the `sa` credential again.
--
-- After running this, update the app's connection string (wherever
-- ConnectionStrings__DefaultConnection / secrets.DB_CONNECTION_STRING is
-- set - GitHub environment secrets, App Service config, local
-- appsettings/user-secrets) to use the new lotv_app login, then rotate
-- (change) the `sa` password separately through your normal SQL Server
-- admin process - this script does not touch the `sa` account itself.

DECLARE @NewPassword nvarchar(128) = 'REPLACE_WITH_A_GENERATED_STRONG_PASSWORD';

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'lotv_app')
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE LOGIN [lotv_app] WITH PASSWORD = ''' + @NewPassword + N''', CHECK_POLICY = ON;';
    EXEC (@sql);
END;

USE [LOTV];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'lotv_app')
BEGIN
    CREATE USER [lotv_app] FOR LOGIN [lotv_app];
END;

-- Least privilege: read/write app data, no schema/ownership rights.
-- Migrations should still run under an admin login (e.g. sa, or a separate
-- lotv_migrator login) as part of the deploy step, not lotv_app.
ALTER ROLE db_datareader ADD MEMBER [lotv_app];
ALTER ROLE db_datawriter ADD MEMBER [lotv_app];
