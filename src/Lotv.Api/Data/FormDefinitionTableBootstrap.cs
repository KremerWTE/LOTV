using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// Creates the FormDefinitions table if it doesn't exist. Startup uses
/// EnsureCreated (see Program.cs), which builds the whole schema only when the
/// database is brand new — it never adds a table to an existing one, so the
/// live SQL Server database and long-lived local SQLite files need this.
/// Safe to run on every startup.
/// </summary>
public static class FormDefinitionTableBootstrap
{
    public static void EnsureTable(LotvDbContext db)
    {
        if (db.Database.IsSqlServer())
        {
            db.Database.ExecuteSqlRaw("""
                IF OBJECT_ID(N'[dbo].[FormDefinitions]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[FormDefinitions] (
                        [Id]             INT            NOT NULL IDENTITY(1,1),
                        [Key]            NVARCHAR(100)  NOT NULL,
                        [DefinitionJson] NVARCHAR(MAX)  NOT NULL,
                        [UpdatedAt]      DATETIME2      NOT NULL,
                        [UpdatedBy]      NVARCHAR(200)  NULL,
                        CONSTRAINT [PK_FormDefinitions] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_FormDefinitions_Key] ON [dbo].[FormDefinitions] ([Key]);
                END
                """);
        }
        else if (db.Database.IsSqlite())
        {
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "FormDefinitions" (
                    "Id"             INTEGER NOT NULL CONSTRAINT "PK_FormDefinitions" PRIMARY KEY AUTOINCREMENT,
                    "Key"            TEXT    NOT NULL,
                    "DefinitionJson" TEXT    NOT NULL,
                    "UpdatedAt"      TEXT    NOT NULL,
                    "UpdatedBy"      TEXT    NULL
                );
                """);
            db.Database.ExecuteSqlRaw("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_FormDefinitions_Key" ON "FormDefinitions" ("Key");""");
        }
    }
}
