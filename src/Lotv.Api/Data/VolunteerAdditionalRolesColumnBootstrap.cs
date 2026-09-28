using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// Adds Volunteers.AdditionalRoles to a database created before a volunteer could hold more than one role
/// (startup uses EnsureCreated, which never alters an existing table). Safe to run on every startup.
/// </summary>
public static class VolunteerAdditionalRolesColumnBootstrap
{
    public static void EnsureColumn(LotvDbContext db)
    {
        if (db.Database.IsSqlServer())
        {
            db.Database.ExecuteSqlRaw("""
                IF OBJECT_ID(N'[dbo].[Volunteers]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Volunteers', N'AdditionalRoles') IS NULL
                    ALTER TABLE [dbo].[Volunteers] ADD [AdditionalRoles] NVARCHAR(200) NULL;
                """);
        }
        else if (db.Database.IsSqlite())
        {
            var exists = db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'Volunteers'").AsEnumerable().First();
            if (exists == 0) return;
            var has = db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('Volunteers') WHERE name = 'AdditionalRoles'").AsEnumerable().First();
            if (has == 0)
                db.Database.ExecuteSqlRaw("""ALTER TABLE "Volunteers" ADD COLUMN "AdditionalRoles" TEXT NULL;""");
        }
    }
}
