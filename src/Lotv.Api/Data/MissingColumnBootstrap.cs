using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Lotv.Api.Data;

/// <summary>
/// Adds any column the EF model expects but an existing SQL Server table lacks (e.g. Volunteers.Level, Requests.ProcessStage).
/// Startup uses EnsureCreated, which never alters an existing table, so a database created before a column was added
/// fails with "Invalid column name" until the column exists. Additive only: never drops, renames or retypes anything,
/// never touches a missing table, and is safe to run on every startup. Returns the columns it added ("Table.Column").
/// </summary>
public static class MissingColumnBootstrap
{
    public static IReadOnlyList<string> EnsureColumns(LotvDbContext db)
    {
        var added = new List<string>();
        if (db.Database.IsSqlite()) return EnsureSqliteColumns(db);   // the local development database
        if (!db.Database.IsSqlServer()) return added;

        var live = db.Database.SqlQueryRaw<string>(
                "SELECT TABLE_SCHEMA + N'.' + TABLE_NAME + N'.' + COLUMN_NAME AS Value FROM INFORMATION_SCHEMA.COLUMNS")
            .AsEnumerable().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var liveTables = live.Select(c => c[..c.LastIndexOf('.')]).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null || entity.GetViewName() is not null) continue;
            var schema = entity.GetSchema() is { Length: > 0 } s ? s : "dbo";
            if (!liveTables.Contains($"{schema}.{table}")) continue;   // a missing table is EnsureCreated's/another bootstrap's job

            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());   // must use the model's own schema (null = default), not "dbo"
            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(store);
                if (column is null || property.IsPrimaryKey() || property.GetComputedColumnSql() is not null) continue;
                if (live.Contains($"{schema}.{table}.{column}")) continue;

                var type = property.GetColumnType(store);
                var nullable = property.IsColumnNullable(store);
                var sql = $"ALTER TABLE {Quote(schema)}.{Quote(table)} ADD {Quote(column)} {type}" +
                          (nullable ? " NULL" : $" NOT NULL CONSTRAINT {Quote($"DF_{table}_{column}")} DEFAULT {DefaultFor(property, type)}");
                db.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));
                added.Add($"{table}.{column}");
            }
        }
        return added;
    }

    /// <summary>The same job for the local SQLite database (development), whose tracked file predates newer columns.</summary>
    private static IReadOnlyList<string> EnsureSqliteColumns(LotvDbContext db)
    {
        var added = new List<string>();
        var live = db.Database.SqlQueryRaw<string>(
                "SELECT m.name || '.' || p.name AS Value FROM sqlite_master m JOIN pragma_table_info(m.name) p WHERE m.type = 'table'")
            .AsEnumerable().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var liveTables = live.Select(c => c[..c.IndexOf('.')]).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null || entity.GetViewName() is not null || !liveTables.Contains(table)) continue;

            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(store);
                if (column is null || property.IsPrimaryKey() || property.GetComputedColumnSql() is not null) continue;
                if (live.Contains($"{table}.{column}")) continue;

                var type = property.GetColumnType(store);
                var sql = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}" +
                          (property.IsColumnNullable(store) ? "" : $" NOT NULL DEFAULT {SqliteDefault(type)}");
                db.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));
                added.Add($"{table}.{column}");
            }
        }
        return added;
    }

    private static string SqliteDefault(string storeType)
    {
        var t = storeType.ToLowerInvariant();
        if (t.Contains("text") || t.Contains("char") || t.Contains("clob")) return "''";
        if (t.Contains("blob")) return "X''";
        return "0";   // INTEGER, REAL, NUMERIC and the date types SQLite stores as numbers or text
    }

    private static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";

    // Existing rows get the property's configured default when it has one, else the neutral value for the column's type.
    private static string DefaultFor(IProperty property, string storeType)
    {
        if (property.GetDefaultValueSql() is { Length: > 0 } sql) return sql;
        var t = storeType.ToLowerInvariant();
        if (t.Contains("char") || t.Contains("text")) return "N''";
        if (t.StartsWith("date") || t.StartsWith("time") || t.StartsWith("smalldate")) return "'0001-01-01'";
        if (t.StartsWith("uniqueidentifier")) return "'00000000-0000-0000-0000-000000000000'";
        if (t.Contains("binary") || t == "image") return "0x";
        return "0";
    }
}
