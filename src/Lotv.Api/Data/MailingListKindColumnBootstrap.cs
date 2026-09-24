using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// Adds MailingListEntries.Kind (0 = Mother's Day, 1 = Father's Day) to a database created before
/// Father's Day existed. Startup uses EnsureCreated, which never alters an existing table.
/// Safe to run on every startup; existing rows become Mother's Day entries.
/// </summary>
public static class MailingListKindColumnBootstrap
{
    public static void EnsureColumn(LotvDbContext db)
    {
        if (db.Database.IsSqlServer())
        {
            db.Database.ExecuteSqlRaw("""
                IF OBJECT_ID(N'[dbo].[MailingListEntries]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.MailingListEntries', N'Kind') IS NULL
                    ALTER TABLE [dbo].[MailingListEntries] ADD [Kind] INT NOT NULL CONSTRAINT [DF_MailingListEntries_Kind] DEFAULT 0;
                """);
        }
        else if (db.Database.IsSqlite())
        {
            var tableExists = db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'MailingListEntries'").AsEnumerable().First();
            if (tableExists == 0) return;
            var hasKind = db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('MailingListEntries') WHERE name = 'Kind'").AsEnumerable().First();
            if (hasKind == 0)
                db.Database.ExecuteSqlRaw("""ALTER TABLE "MailingListEntries" ADD COLUMN "Kind" INTEGER NOT NULL DEFAULT 0;""");
        }
    }
}
