using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// Adds Requests.WantsPackage to a database created before a request could be prayer-only (startup uses
/// EnsureCreated, which never alters an existing table). Every existing row becomes true (a package request,
/// which is all any of them ever were). Safe to run on every startup.
/// </summary>
public static class RequestWantsPackageColumnBootstrap
{
    public static void EnsureColumn(LotvDbContext db)
    {
        if (db.Database.IsSqlServer())
        {
            db.Database.ExecuteSqlRaw("""
                IF OBJECT_ID(N'[dbo].[Requests]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Requests', N'WantsPackage') IS NULL
                    ALTER TABLE [dbo].[Requests] ADD [WantsPackage] BIT NOT NULL CONSTRAINT [DF_Requests_WantsPackage] DEFAULT (1);
                """);
        }
        else if (db.Database.IsSqlite())
        {
            var exists = db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'Requests'").AsEnumerable().First();
            if (exists == 0) return;
            var has = db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('Requests') WHERE name = 'WantsPackage'").AsEnumerable().First();
            if (has == 0)
                db.Database.ExecuteSqlRaw("""ALTER TABLE "Requests" ADD COLUMN "WantsPackage" INTEGER NOT NULL DEFAULT 1;""");
        }
    }
}
