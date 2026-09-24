using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// Adds FollowUpMilestones.ReminderSentAt to a database created before due-date reminders existed
/// (startup uses EnsureCreated, which never alters an existing table). Safe to run on every startup.
/// </summary>
public static class FollowUpReminderColumnBootstrap
{
    public static void EnsureColumn(LotvDbContext db)
    {
        if (db.Database.IsSqlServer())
        {
            db.Database.ExecuteSqlRaw("""
                IF OBJECT_ID(N'[dbo].[FollowUpMilestones]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.FollowUpMilestones', N'ReminderSentAt') IS NULL
                    ALTER TABLE [dbo].[FollowUpMilestones] ADD [ReminderSentAt] DATETIME2 NULL;
                """);
        }
        else if (db.Database.IsSqlite())
        {
            var exists = db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'FollowUpMilestones'").AsEnumerable().First();
            if (exists == 0) return;
            var has = db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('FollowUpMilestones') WHERE name = 'ReminderSentAt'").AsEnumerable().First();
            if (has == 0)
                db.Database.ExecuteSqlRaw("""ALTER TABLE "FollowUpMilestones" ADD COLUMN "ReminderSentAt" TEXT NULL;""");
        }
    }
}
