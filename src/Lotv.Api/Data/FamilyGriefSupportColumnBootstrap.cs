using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// Adds Families.GriefSupportRequested to a database created before the answer was stored as its own field
/// (startup uses EnsureCreated, which never alters an existing table), and fills it in from the sentence the
/// intake form used to write into ContactNotes. Safe to run on every startup.
/// </summary>
public static class FamilyGriefSupportColumnBootstrap
{
    public static void EnsureColumn(LotvDbContext db)
    {
        if (db.Database.IsSqlServer())
        {
            db.Database.ExecuteSqlRaw("""
                IF OBJECT_ID(N'[dbo].[Families]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Families', N'GriefSupportRequested') IS NULL
                    ALTER TABLE [dbo].[Families] ADD [GriefSupportRequested] BIT NULL;
                """);
            db.Database.ExecuteSqlRaw("""
                UPDATE [dbo].[Families] SET [GriefSupportRequested] = 1
                WHERE [GriefSupportRequested] IS NULL AND [ContactNotes] LIKE N'%Quarterly Grief Support requested: Yes%';
                UPDATE [dbo].[Families] SET [GriefSupportRequested] = 0
                WHERE [GriefSupportRequested] IS NULL AND [ContactNotes] LIKE N'%Quarterly Grief Support requested: No%';
                """);
        }
        else if (db.Database.IsSqlite())
        {
            var exists = db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'Families'").AsEnumerable().First();
            if (exists == 0) return;
            var has = db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('Families') WHERE name = 'GriefSupportRequested'").AsEnumerable().First();
            if (has == 0)
                db.Database.ExecuteSqlRaw("""ALTER TABLE "Families" ADD COLUMN "GriefSupportRequested" INTEGER NULL;""");
            db.Database.ExecuteSqlRaw("""
                UPDATE "Families" SET "GriefSupportRequested" = 1
                WHERE "GriefSupportRequested" IS NULL AND "ContactNotes" LIKE '%Quarterly Grief Support requested: Yes%';
                """);
            db.Database.ExecuteSqlRaw("""
                UPDATE "Families" SET "GriefSupportRequested" = 0
                WHERE "GriefSupportRequested" IS NULL AND "ContactNotes" LIKE '%Quarterly Grief Support requested: No%';
                """);
        }
    }
}
