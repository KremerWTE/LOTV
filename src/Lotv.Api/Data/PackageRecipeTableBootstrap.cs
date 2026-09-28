using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// Creates the PackageRecipeItems table if it doesn't exist. Startup uses EnsureCreated, which never adds a
/// table to an existing database, so the live SQL Server database and long-lived SQLite files need this.
/// Safe to run on every startup.
/// </summary>
public static class PackageRecipeTableBootstrap
{
    public static void EnsureTable(LotvDbContext db)
    {
        if (db.Database.IsSqlServer())
        {
            db.Database.ExecuteSqlRaw("""
                IF OBJECT_ID(N'[dbo].[PackageRecipeItems]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[PackageRecipeItems] (
                        [Id]             INT NOT NULL IDENTITY(1,1),
                        [ChapterId]      INT NOT NULL,
                        [ResourceItemId] INT NOT NULL,
                        [QuantityPerBox] INT NOT NULL,
                        CONSTRAINT [PK_PackageRecipeItems] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_PackageRecipeItems_ChapterId_ResourceItemId] ON [dbo].[PackageRecipeItems] ([ChapterId], [ResourceItemId]);
                END
                """);
        }
        else if (db.Database.IsSqlite())
        {
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "PackageRecipeItems" (
                    "Id"             INTEGER NOT NULL CONSTRAINT "PK_PackageRecipeItems" PRIMARY KEY AUTOINCREMENT,
                    "ChapterId"      INTEGER NOT NULL,
                    "ResourceItemId" INTEGER NOT NULL,
                    "QuantityPerBox" INTEGER NOT NULL
                );
                """);
            db.Database.ExecuteSqlRaw("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_PackageRecipeItems_ChapterId_ResourceItemId" ON "PackageRecipeItems" ("ChapterId", "ResourceItemId");""");
        }
    }
}
