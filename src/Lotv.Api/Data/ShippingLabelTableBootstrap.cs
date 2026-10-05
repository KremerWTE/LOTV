using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// Creates the ShippingLabels table if it doesn't exist. Startup uses EnsureCreated, which never adds a
/// table to an existing database, so the live SQL Server database and long-lived SQLite files need this.
/// Safe to run on every startup.
/// </summary>
public static class ShippingLabelTableBootstrap
{
    public static void EnsureTable(LotvDbContext db)
    {
        if (db.Database.IsSqlServer())
        {
            db.Database.ExecuteSqlRaw("""
                IF OBJECT_ID(N'[dbo].[ShippingLabels]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[ShippingLabels] (
                        [Id]               INT NOT NULL IDENTITY(1,1),
                        [PackageRequestId] INT NOT NULL,
                        [TrackingNumber]   NVARCHAR(MAX) NOT NULL,
                        [IsPlaceholder]    BIT NOT NULL,
                        [Carrier]          NVARCHAR(MAX) NULL,
                        [ServiceLevel]     NVARCHAR(MAX) NULL,
                        [LabelFileUrl]     NVARCHAR(MAX) NULL,
                        [GeneratedAt]      DATETIME2 NOT NULL,
                        CONSTRAINT [PK_ShippingLabels] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_ShippingLabels_PackageRequestId] ON [dbo].[ShippingLabels] ([PackageRequestId]);
                END
                """);
        }
        else if (db.Database.IsSqlite())
        {
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "ShippingLabels" (
                    "Id"               INTEGER NOT NULL CONSTRAINT "PK_ShippingLabels" PRIMARY KEY AUTOINCREMENT,
                    "PackageRequestId" INTEGER NOT NULL,
                    "TrackingNumber"   TEXT NOT NULL,
                    "IsPlaceholder"    INTEGER NOT NULL,
                    "Carrier"          TEXT NULL,
                    "ServiceLevel"     TEXT NULL,
                    "LabelFileUrl"     TEXT NULL,
                    "GeneratedAt"      TEXT NOT NULL
                );
                """);
            db.Database.ExecuteSqlRaw("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_ShippingLabels_PackageRequestId" ON "ShippingLabels" ("PackageRequestId");""");
        }
    }
}
