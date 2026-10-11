using Microsoft.Data.Sqlite;
using SalesWeb.Server.Services;

namespace SalesWeb.Server.Data;

/// <summary>SQLite local: tài khoản web, phân quyền, audit log.
/// B1 là source of truth cho dữ liệu đơn hàng — DB này KHÔNG lưu đơn hàng.</summary>
public static class Db
{
    private static string _dbPath = "./data/app.db";

    public static void Configure(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _dbPath = Path.Combine(dataDir, "app.db");
    }

    public static string DbPath => _dbPath;

    public static SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    public static void InitAll(AppConfig cfg)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT UNIQUE NOT NULL,
                password_hash TEXT NOT NULL,
                full_name TEXT DEFAULT '',
                role TEXT NOT NULL DEFAULT 'viewer',
                sales_person_code INTEGER,
                active INTEGER NOT NULL DEFAULT 1,
                created_at TEXT DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS audit_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL,
                action TEXT NOT NULL,
                entity TEXT NOT NULL,
                entity_key TEXT,
                detail TEXT,
                ip TEXT,
                created_at TEXT DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_audit_created ON audit_logs(created_at);

            CREATE TABLE IF NOT EXISTS app_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL DEFAULT '',
                updated_at TEXT DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS md_sync_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                company_db TEXT NOT NULL DEFAULT '',
                entity TEXT NOT NULL,
                status TEXT NOT NULL,
                started_at TEXT DEFAULT (datetime('now')),
                finished_at TEXT,
                records INTEGER NOT NULL DEFAULT 0,
                error TEXT DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_md_sync_log_entity ON md_sync_log(company_db, entity, id);
            """;
        cmd.ExecuteNonQuery();

        // Master data tách theo CompanyDB (fix #3: cô lập dữ liệu đa DB)
        // Migration: bảng cũ không có company_db → drop để tạo lại (cache sync lại được)
        EnsureMdTable(conn, "md_customers", @"
            CREATE TABLE md_customers (
                company_db TEXT NOT NULL DEFAULT '',
                card_code TEXT NOT NULL,
                card_name TEXT NOT NULL DEFAULT '',
                phone TEXT DEFAULT '',
                sales_person_code INTEGER,
                price_list_num INTEGER,
                currency TEXT DEFAULT '',
                updated_at TEXT DEFAULT '',
                synced_at TEXT DEFAULT (datetime('now')),
                PRIMARY KEY (company_db, card_code)
            );",
            "CREATE INDEX idx_md_customers_name ON md_customers(company_db, card_name);");
        EnsureMdTable(conn, "md_items", @"
            CREATE TABLE md_items (
                company_db TEXT NOT NULL DEFAULT '',
                item_code TEXT NOT NULL,
                item_name TEXT NOT NULL DEFAULT '',
                sales_unit TEXT DEFAULT '',
                price REAL NOT NULL DEFAULT 0,
                item_type TEXT DEFAULT '',
                updated_at TEXT DEFAULT '',
                synced_at TEXT DEFAULT (datetime('now')),
                PRIMARY KEY (company_db, item_code)
            );",
            "CREATE INDEX idx_md_items_name ON md_items(company_db, item_name);");
        EnsureMdTable(conn, "md_tax_codes", @"
            CREATE TABLE md_tax_codes (
                company_db TEXT NOT NULL DEFAULT '',
                code TEXT NOT NULL,
                name TEXT NOT NULL DEFAULT '',
                rate REAL NOT NULL DEFAULT 0,
                synced_at TEXT DEFAULT (datetime('now')),
                PRIMARY KEY (company_db, code)
            );",
            null);

        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = """
            CREATE TABLE IF NOT EXISTS sl_audit (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                created_at TEXT DEFAULT (datetime('now')),
                operation TEXT NOT NULL,
                entity_key TEXT DEFAULT '',
                username TEXT DEFAULT '',
                request_json TEXT DEFAULT '{}',
                success INTEGER NOT NULL DEFAULT 0,
                http_status INTEGER,
                response_json TEXT DEFAULT '',
                error TEXT DEFAULT '',
                duration_ms INTEGER NOT NULL DEFAULT 0,
                retry_count INTEGER NOT NULL DEFAULT 0,
                retried_from INTEGER,
                note TEXT DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_sl_audit_created ON sl_audit(created_at DESC);
            CREATE INDEX IF NOT EXISTS idx_sl_audit_op ON sl_audit(operation);
            CREATE INDEX IF NOT EXISTS idx_sl_audit_success ON sl_audit(success);

            -- Refresh token (B5): lưu hash, xoay vòng mỗi lần dùng
            CREATE TABLE IF NOT EXISTS refresh_tokens (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                token_hash TEXT NOT NULL UNIQUE,
                username TEXT NOT NULL,
                company_db TEXT DEFAULT '',
                expires_at TEXT NOT NULL,
                created_at TEXT DEFAULT (datetime('now')),
                revoked INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_refresh_hash ON refresh_tokens(token_hash);
            CREATE INDEX IF NOT EXISTS idx_refresh_user ON refresh_tokens(username);
            """;
        cmd2.ExecuteNonQuery();

        // Migration M1: md_sync_log cũ chưa có company_db → thêm cột
        using var mig = conn.CreateCommand();
        mig.CommandText = "SELECT COUNT(*) FROM pragma_table_info('md_sync_log') WHERE name='company_db'";
        if (Convert.ToInt64(mig.ExecuteScalar() ?? 0) == 0)
        {
            using var add = conn.CreateCommand();
            add.CommandText = "ALTER TABLE md_sync_log ADD COLUMN company_db TEXT NOT NULL DEFAULT ''";
            add.ExecuteNonQuery();
        }

        // Seed tài khoản admin lần đầu
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT id FROM users WHERE username = $u";
        check.Parameters.AddWithValue("$u", cfg.AdminUsername);
        if (check.ExecuteScalar() is null)
        {
            using var ins = conn.CreateCommand();
            ins.CommandText = "INSERT INTO users (username, password_hash, full_name, role) VALUES ($u, $p, 'Administrator', 'admin')";
            ins.Parameters.AddWithValue("$u", cfg.AdminUsername);
            ins.Parameters.AddWithValue("$p", AuthService.HashPassword(cfg.AdminPassword));
            ins.ExecuteNonQuery();
        }
    }

    public static Dictionary<string, object?> RowToDict(SqliteDataReader r)
    {
        var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < r.FieldCount; i++)
            d[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
        return d;
    }

    /// <summary>
    /// Tạo bảng master data với company_db; nếu bảng cũ chưa có cột company_db
    /// thì drop để tạo lại (dữ liệu cache sẽ được sync lại).
    /// </summary>
    private static void EnsureMdTable(SqliteConnection con, string table, string createSql, string? indexSql)
    {
        using var check = con.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info(@t) WHERE name='company_db'";
        check.Parameters.AddWithValue("@t", table);
        var hasCol = Convert.ToInt64(check.ExecuteScalar() ?? 0) > 0;

        using var cmd = con.CreateCommand();
        if (!hasCol)
        {
            // Bảng chưa tồn tại (COUNT=0) hoặc bảng cũ không có company_db → tạo mới
            cmd.CommandText = $"DROP TABLE IF EXISTS {table}; {createSql}" +
                (indexSql is null ? "" : $" {indexSql}");
        }
        else
        {
            cmd.CommandText = $"{createSql.Replace("CREATE TABLE", "CREATE TABLE IF NOT EXISTS")}" +
                (indexSql is null ? "" : $" {indexSql.Replace("CREATE INDEX", "CREATE INDEX IF NOT EXISTS")}");
        }
        cmd.ExecuteNonQuery();
    }
}

/// <summary>Ghi audit log thao tác trên web (bảng audit_logs).</summary>
public static class AuditLog
{
    public static void LogAction(string username, string action, string entity = "Orders",
        string? entityKey = null, Dictionary<string, object?>? detail = null, string? ip = null)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO audit_logs (username, action, entity, entity_key, detail, ip) VALUES ($u,$a,$e,$k,$d,$ip)";
        cmd.Parameters.AddWithValue("$u", username);
        cmd.Parameters.AddWithValue("$a", action);
        cmd.Parameters.AddWithValue("$e", entity);
        cmd.Parameters.AddWithValue("$k", (object?)entityKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$d", detail is null ? "{}"
            : System.Text.Json.JsonSerializer.Serialize(detail, Services.ServerJson.Options));
        cmd.Parameters.AddWithValue("$ip", (object?)ip ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public static List<Dictionary<string, object?>> RecentLogs(int limit = 100)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT username, action, entity, entity_key, detail, ip, created_at FROM audit_logs ORDER BY id DESC LIMIT $l";
        cmd.Parameters.AddWithValue("$l", limit);
        using var r = cmd.ExecuteReader();
        var out_ = new List<Dictionary<string, object?>>();
        while (r.Read()) out_.Add(Db.RowToDict(r));
        return out_;
    }
}
