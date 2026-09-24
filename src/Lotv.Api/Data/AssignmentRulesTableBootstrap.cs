using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// Creates the AssignmentRules table if it doesn't exist. Startup uses EnsureCreated, which never adds a
/// table to an existing database, so the live SQL Server database and long-lived SQLite files need this.
/// Safe to run on every startup.
/// </summary>
public static class AssignmentRulesTableBootstrap
{
    public static void EnsureTable(LotvDbContext db)
    {
        if (db.Database.IsSqlServer())
        {
            db.Database.ExecuteSqlRaw("""
                IF OBJECT_ID(N'[dbo].[AssignmentRules]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[AssignmentRules] (
                        [Id]                  INT            NOT NULL IDENTITY(1,1),
                        [Name]                NVARCHAR(200)  NOT NULL,
                        [Priority]            INT            NOT NULL,
                        [IsActive]            BIT            NOT NULL,
                        [Reasons]             NVARCHAR(500)  NULL,
                        [State]               NVARCHAR(10)   NULL,
                        [City]                NVARCHAR(100)  NULL,
                        [ZipPrefix]           NVARCHAR(10)   NULL,
                        [ChapterId]           INT            NULL,
                        [ForSelf]             BIT            NULL,
                        [AssignToVolunteerId] INT            NOT NULL,
                        [CreatedAt]           DATETIME2      NOT NULL,
                        [UpdatedAt]           DATETIME2      NOT NULL,
                        [UpdatedBy]           NVARCHAR(200)  NULL,
                        CONSTRAINT [PK_AssignmentRules] PRIMARY KEY ([Id])
                    );
                END
                """);
        }
        else if (db.Database.IsSqlite())
        {
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "AssignmentRules" (
                    "Id"                  INTEGER NOT NULL CONSTRAINT "PK_AssignmentRules" PRIMARY KEY AUTOINCREMENT,
                    "Name"                TEXT    NOT NULL,
                    "Priority"            INTEGER NOT NULL,
                    "IsActive"            INTEGER NOT NULL,
                    "Reasons"             TEXT    NULL,
                    "State"               TEXT    NULL,
                    "City"                TEXT    NULL,
                    "ZipPrefix"           TEXT    NULL,
                    "ChapterId"           INTEGER NULL,
                    "ForSelf"             INTEGER NULL,
                    "AssignToVolunteerId" INTEGER NOT NULL,
                    "CreatedAt"           TEXT    NOT NULL,
                    "UpdatedAt"           TEXT    NOT NULL,
                    "UpdatedBy"           TEXT    NULL
                );
                """);
        }
    }
}
