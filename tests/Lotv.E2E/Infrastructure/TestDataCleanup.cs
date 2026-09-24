using Microsoft.Data.Sqlite;

namespace Lotv.E2E.Infrastructure;

/// <summary>
/// The browser tests submit real requests and create records in the local dev database. This remembers how far
/// each table had grown before the run and removes everything newer afterward, so a run leaves the database as it
/// found it. Only used against a local SQLite dev database (found automatically, or set E2E_DB_PATH); a run against
/// any other environment is left alone. Best effort: a failure never fails the tests.
/// </summary>
public sealed class TestDataCleanup
{
    private readonly string _path;
    private readonly Dictionary<string, long> _before;

    private static readonly string[] Tracked = ["Families", "Requests", "Volunteers", "AssignmentRules", "MailingListEntries"];

    private TestDataCleanup(string path, Dictionary<string, long> before)
    {
        _path = path;
        _before = before;
    }

    public static TestDataCleanup? Snapshot()
    {
        try
        {
            var host = new Uri(E2ESettings.BaseUrl).Host;
            if (host is not ("localhost" or "127.0.0.1")) return null;
            var path = Environment.GetEnvironmentVariable("E2E_DB_PATH") ?? FindDevDatabase();
            if (path is null || !File.Exists(path)) return null;

            using var conn = Open(path);
            var before = new Dictionary<string, long>();
            foreach (var table in Tracked)
            {
                if (!TableExists(conn, table)) continue;
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT COALESCE(MAX(Id), 0) FROM \"{table}\"";
                before[table] = Convert.ToInt64(cmd.ExecuteScalar());
            }
            return new TestDataCleanup(path, before);
        }
        catch { return null; }
    }

    public void Run()
    {
        try
        {
            using var conn = Open(_path);
            Exec(conn, "PRAGMA foreign_keys = OFF");
            using var tx = conn.BeginTransaction();
            var fks = ForeignKeys(conn);

            // Families first (their requests and everything hanging off them go too), then any other new requests.
            DeleteNew(conn, fks, "Families");
            DeleteNew(conn, fks, "Requests");
            DeleteNew(conn, fks, "AssignmentRules");
            DeleteNew(conn, fks, "MailingListEntries");

            // New volunteers: take their cases back rather than deleting cases that existed before the run.
            if (_before.TryGetValue("Volunteers", out var maxVol))
            {
                var ids = Ids(conn, $"SELECT Id FROM Volunteers WHERE Id > {maxVol}");
                if (ids.Count > 0)
                {
                    var list = string.Join(",", ids);
                    Exec(conn, $"UPDATE Requests SET AssignedToId = NULL, AssignedTo = NULL WHERE AssignedToId IN ({list})");
                    Exec(conn, $"DELETE FROM RequestAssignments WHERE AssignedToId IN ({list})");
                    Delete(conn, fks, "Volunteers", ids, new HashSet<(string, long)>(), skipChild: ("Requests", "AssignedToId"));
                }
            }

            Exec(conn, "UPDATE Volunteers SET ActiveCases = (SELECT COUNT(*) FROM Requests r WHERE r.AssignedToId = Volunteers.Id AND r.Status NOT IN (4, 6))");
            tx.Commit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[E2E] Test data cleanup skipped: {ex.Message}");
        }
    }

    // ── internals ─────────────────────────────────────────────────────────────

    private void DeleteNew(SqliteConnection conn, Dictionary<string, List<(string Child, string Column)>> fks, string table)
    {
        if (!_before.TryGetValue(table, out var max)) return;
        Delete(conn, fks, table, Ids(conn, $"SELECT Id FROM \"{table}\" WHERE Id > {max}"), new HashSet<(string, long)>());
    }

    private static void Delete(SqliteConnection conn, Dictionary<string, List<(string Child, string Column)>> fks,
        string table, List<long> ids, HashSet<(string, long)> seen, (string Table, string Column)? skipChild = null)
    {
        ids = ids.Where(i => seen.Add((table, i))).ToList();
        if (ids.Count == 0) return;
        var list = string.Join(",", ids);

        if (fks.TryGetValue(table, out var children))
            foreach (var (child, column) in children)
            {
                if (child == table || (skipChild is { } s && s.Table == child && s.Column == column)) continue;
                if (HasIdColumn(conn, child))
                    Delete(conn, fks, child, Ids(conn, $"SELECT Id FROM \"{child}\" WHERE \"{column}\" IN ({list})"), seen);
                else
                    Exec(conn, $"DELETE FROM \"{child}\" WHERE \"{column}\" IN ({list})");
            }
        Exec(conn, $"DELETE FROM \"{table}\" WHERE Id IN ({list})");
    }

    private static Dictionary<string, List<(string Child, string Column)>> ForeignKeys(SqliteConnection conn)
    {
        var map = new Dictionary<string, List<(string, string)>>();
        foreach (var table in Names(conn, "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'"))
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA foreign_key_list(\"{table}\")";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var parent = r.GetString(2);
                if (!map.TryGetValue(parent, out var list)) map[parent] = list = [];
                list.Add((table, r.GetString(3)));
            }
        }
        return map;
    }

    private static bool HasIdColumn(SqliteConnection conn, string table) =>
        Names(conn, $"SELECT name FROM pragma_table_info('{table}')").Contains("Id");

    private static bool TableExists(SqliteConnection conn, string table) =>
        Names(conn, $"SELECT name FROM sqlite_master WHERE type = 'table' AND name = '{table}'").Count > 0;

    private static List<string> Names(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var list = new List<string>();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    private static List<long> Ids(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var list = new List<long>();
        while (r.Read()) list.Add(r.GetInt64(0));
        return list;
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static SqliteConnection Open(string path)
    {
        var conn = new SqliteConnection($"Data Source={path};Pooling=False;Default Timeout=30");
        conn.Open();
        return conn;
    }

    private static string? FindDevDatabase()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Lotv.Api", "lotv-dev.db");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
