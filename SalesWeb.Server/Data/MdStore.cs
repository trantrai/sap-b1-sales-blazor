using SalesWeb.Server.Services.B1;

namespace SalesWeb.Server.Data;

/// <summary>SQLite local cho master data B1 (Khách hàng, Mặt hàng, Thuế suất).
/// B1 là source of truth — DB này là bản sao đọc để tra cứu nhanh khi tạo đơn.</summary>
public static class MdStore
{
    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    public static int UpsertCustomers(string companyDb, List<Dictionary<string, object?>> rows)
    {
        var data = rows.Where(r => B1Json.Str(r, "CardCode") != "")
            .Select(r => new object?[]
            {
                companyDb, B1Json.Str(r, "CardCode"), B1Json.Str(r, "CardName"),
                B1Json.Str(r, "Phone1"), B1Json.IntN(r, "SalesPersonCode"),
                B1Json.IntN(r, "PriceListNum"), B1Json.Str(r, "Currency"),
                B1Json.Str(r, "UpdateDate"), Now(),
            }).ToList();
        if (data.Count == 0) return 0;
        using var conn = Db.Open();
        using var tx = conn.BeginTransaction();
        foreach (var d in data)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO md_customers
                (company_db, card_code, card_name, phone, sales_person_code, price_list_num, currency, updated_at, synced_at)
                VALUES ($0,$1,$2,$3,$4,$5,$6,$7,$8)
                ON CONFLICT(company_db, card_code) DO UPDATE SET
                  card_name=excluded.card_name, phone=excluded.phone,
                  sales_person_code=excluded.sales_person_code,
                  price_list_num=excluded.price_list_num,
                  currency=excluded.currency, updated_at=excluded.updated_at,
                  synced_at=excluded.synced_at
                """;
            for (int i = 0; i < d.Length; i++)
                cmd.Parameters.AddWithValue($"${i}", d[i] ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return data.Count;
    }

    public static int UpsertItems(string companyDb, List<Dictionary<string, object?>> rows)
    {
        var data = rows.Where(r => B1Json.Str(r, "ItemCode") != "")
            .Select(r => new object?[]
            {
                companyDb, B1Json.Str(r, "ItemCode"), B1Json.Str(r, "ItemName"),
                B1Json.Str(r, "SalesUnit"), B1Json.Num(r, "Price"),
                B1Json.Str(r, "ItemType"), B1Json.Str(r, "UpdateDate"), Now(),
            }).ToList();
        if (data.Count == 0) return 0;
        using var conn = Db.Open();
        using var tx = conn.BeginTransaction();
        foreach (var d in data)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO md_items
                (company_db, item_code, item_name, sales_unit, price, item_type, updated_at, synced_at)
                VALUES ($0,$1,$2,$3,$4,$5,$6,$7)
                ON CONFLICT(company_db, item_code) DO UPDATE SET
                  item_name=excluded.item_name, sales_unit=excluded.sales_unit,
                  price=excluded.price, item_type=excluded.item_type,
                  updated_at=excluded.updated_at, synced_at=excluded.synced_at
                """;
            for (int i = 0; i < d.Length; i++)
                cmd.Parameters.AddWithValue($"${i}", d[i] ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return data.Count;
    }

    public static int UpsertTaxCodes(string companyDb, List<Dictionary<string, object?>> rows)
    {
        var data = rows.Where(r => B1Json.Str(r, "Code") != "")
            .Select(r => new object?[]
            {
                companyDb, B1Json.Str(r, "Code"),
                B1Json.Str(r, "Name") != "" ? B1Json.Str(r, "Name") : B1Json.Str(r, "Code"),
                B1Json.Num(r, "Rate"), Now(),
            }).ToList();
        if (data.Count == 0) return 0;
        using var conn = Db.Open();
        using var tx = conn.BeginTransaction();
        foreach (var d in data)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO md_tax_codes (company_db, code, name, rate, synced_at)
                VALUES ($0,$1,$2,$3,$4)
                ON CONFLICT(company_db, code) DO UPDATE SET
                  name=excluded.name, rate=excluded.rate, synced_at=excluded.synced_at
                """;
            for (int i = 0; i < d.Length; i++)
                cmd.Parameters.AddWithValue($"${i}", d[i] ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return data.Count;
    }

    public static int Count(string entity, string companyDb)
    {
        var table = entity switch
        {
            "customers" => "md_customers",
            "items" => "md_items",
            "tax_codes" => "md_tax_codes",
            _ => throw new ArgumentException($"Entity không hợp lệ: {entity}"),
        };
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE company_db = $db";
        cmd.Parameters.AddWithValue("$db", companyDb);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public static List<Dictionary<string, object?>> SearchCustomers(string companyDb, string q, int top = 20)
    {
        var like = $"%{q}%";
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT card_code, card_name, phone, sales_person_code, price_list_num
            FROM md_customers
            WHERE company_db = $db AND (card_code LIKE $q OR card_name LIKE $q)
            ORDER BY card_code LIMIT $top
            """;
        cmd.Parameters.AddWithValue("$db", companyDb);
        cmd.Parameters.AddWithValue("$q", like);
        cmd.Parameters.AddWithValue("$top", top);
        using var r = cmd.ExecuteReader();
        var out_ = new List<Dictionary<string, object?>>();
        while (r.Read()) out_.Add(Db.RowToDict(r));
        return out_;
    }

    public static List<Dictionary<string, object?>> SearchItems(string companyDb, string q, int top = 20)
    {
        var like = $"%{q}%";
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT item_code, item_name, sales_unit, price
            FROM md_items
            WHERE company_db = $db AND (item_code LIKE $q OR item_name LIKE $q)
            ORDER BY item_code LIMIT $top
            """;
        cmd.Parameters.AddWithValue("$db", companyDb);
        cmd.Parameters.AddWithValue("$q", like);
        cmd.Parameters.AddWithValue("$top", top);
        using var r = cmd.ExecuteReader();
        var out_ = new List<Dictionary<string, object?>>();
        while (r.Read()) out_.Add(Db.RowToDict(r));
        return out_;
    }

    public static List<Dictionary<string, object?>> ListTaxCodes(string companyDb)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT code, name, rate FROM md_tax_codes WHERE company_db = $db ORDER BY code";
        cmd.Parameters.AddWithValue("$db", companyDb);
        using var r = cmd.ExecuteReader();
        var out_ = new List<Dictionary<string, object?>>();
        while (r.Read()) out_.Add(Db.RowToDict(r));
        return out_;
    }

    public static long LogStart(string entity)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO md_sync_log (entity, status) VALUES ($e, 'running'); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$e", entity);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public static void LogFinish(long logId, int records, string error = "")
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE md_sync_log SET status=$s, finished_at=datetime('now'), records=$r, error=$e WHERE id=$id";
        cmd.Parameters.AddWithValue("$s", string.IsNullOrEmpty(error) ? "done" : "error");
        cmd.Parameters.AddWithValue("$r", records);
        cmd.Parameters.AddWithValue("$e", error);
        cmd.Parameters.AddWithValue("$id", logId);
        cmd.ExecuteNonQuery();
    }

    public static List<Dictionary<string, object?>> SyncStatus()
    {
        using var conn = Db.Open();
        var out_ = new List<Dictionary<string, object?>>();
        foreach (var (entity, table) in new[] { ("customers", "md_customers"), ("items", "md_items"), ("tax_codes", "md_tax_codes") })
        {
            using var c1 = conn.CreateCommand();
            c1.CommandText = $"SELECT COUNT(*) FROM {table}";
            var total = Convert.ToInt32(c1.ExecuteScalar());
            using var c2 = conn.CreateCommand();
            c2.CommandText = "SELECT status, started_at, finished_at, records, error FROM md_sync_log WHERE entity=$e ORDER BY id DESC LIMIT 1";
            c2.Parameters.AddWithValue("$e", entity);
            using var r = c2.ExecuteReader();
            var row = new Dictionary<string, object?>
            {
                ["entity"] = entity,
                ["count"] = total,
                ["last_status"] = null,
                ["last_started_at"] = null,
                ["last_finished_at"] = null,
                ["last_records"] = 0,
                ["last_error"] = "",
            };
            if (r.Read())
            {
                row["last_status"] = r.IsDBNull(0) ? null : r.GetString(0);
                row["last_started_at"] = r.IsDBNull(1) ? null : r.GetString(1);
                row["last_finished_at"] = r.IsDBNull(2) ? null : r.GetString(2);
                row["last_records"] = r.IsDBNull(3) ? 0 : r.GetInt32(3);
                row["last_error"] = r.IsDBNull(4) ? "" : r.GetString(4);
            }
            out_.Add(row);
        }
        return out_;
    }
}
