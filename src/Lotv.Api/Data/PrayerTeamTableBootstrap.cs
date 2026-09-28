using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// Creates the PrayerTeamMembers table if it doesn't exist. Startup uses EnsureCreated, which never adds a
/// table to an existing database, so the live SQL Server database and long-lived SQLite files need this.
/// Safe to run on every startup.
/// </summary>
public static class PrayerTeamTableBootstrap
{
    public static void EnsureTable(LotvDbContext db)
    {
        if (db.Database.IsSqlServer())
        {
            db.Database.ExecuteSqlRaw("""
                IF OBJECT_ID(N'[dbo].[PrayerTeamMembers]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[PrayerTeamMembers] (
                        [Id]           INT            NOT NULL IDENTITY(1,1),
                        [RequestId]    INT            NOT NULL,
                        [VolunteerId]  INT            NOT NULL,
                        [AddedById]    NVARCHAR(450)  NOT NULL,
                        [AddedByName]  NVARCHAR(200)  NOT NULL,
                        [AddedAt]      DATETIME2      NOT NULL,
                        CONSTRAINT [PK_PrayerTeamMembers] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_PrayerTeamMembers_RequestId_VolunteerId] ON [dbo].[PrayerTeamMembers] ([RequestId], [VolunteerId]);
                END
                """);
        }
        else if (db.Database.IsSqlite())
        {
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "PrayerTeamMembers" (
                    "Id"          INTEGER NOT NULL CONSTRAINT "PK_PrayerTeamMembers" PRIMARY KEY AUTOINCREMENT,
                    "RequestId"   INTEGER NOT NULL,
                    "VolunteerId" INTEGER NOT NULL,
                    "AddedById"   TEXT    NOT NULL,
                    "AddedByName" TEXT    NOT NULL,
                    "AddedAt"     TEXT    NOT NULL
                );
                """);
            db.Database.ExecuteSqlRaw("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_PrayerTeamMembers_RequestId_VolunteerId" ON "PrayerTeamMembers" ("RequestId", "VolunteerId");""");
        }
    }
}
