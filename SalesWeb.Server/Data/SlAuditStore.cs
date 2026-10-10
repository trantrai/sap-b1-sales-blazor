using System.Text.Json;
using SalesWeb.Server.Services;

namespace SalesWeb.Server.Data;

/// <summary>Audit mọi thao tác ghi lên SAP B1 qua Service Layer.</summary>
public static class SlAuditStore
{
    private const int MaxJson = 8000;

    private static string Trim(object? obj)
    {
        string s;
        try
        {
            s = JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                Encoder = ServerJson.Relaxed,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
            });
        }
        catch { s = obj?.ToString() ?? ""; }
        return s.Length <= MaxJson ? s : s[..MaxJson] + "…[cắt ngắn]";
    }

    public static long Log(string operation, string entityKey, string username,
        Dictionary<string, object?> requestData, bool success, object? response,
        string error, int? httpStatus, int durationMs, long? retriedFrom, string note)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sl_audit
            (operation, entity_key, username, request_json, success, http_status,
             response_json, error, duration_ms, retried_from, note)
            VALUES ($op,$key,$user,$req,$ok,$http,$resp,$err,$dur,$rf,$note)
            """;
        cmd.Parameters.AddWithValue("$op", operation);
        cmd.Parameters.AddWithValue("$key", entityKey ?? "");
        cmd.Parameters.AddWithValue("$user", username ?? "");
        cmd.Parameters.AddWithValue("$req", Trim(requestData));
        cmd.Parameters.AddWithValue("$ok", success ? 1 : 0);
        cmd.Parameters.AddWithValue("$http", (object?)httpStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$resp", response is null ? "" : Trim(response));
        cmd.Parameters.AddWithValue("$err", error ?? "");
        cmd.Parameters.AddWithValue("$dur", durationMs);
        cmd.Parameters.AddWithValue("$rf", (object?)retriedFrom ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$note", note ?? "");
        cmd.ExecuteNonQuery();
        using var idCmd = conn.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt64(idCmd.ExecuteScalar());
    }

    public static Dictionary<string, object?>? Get(long auditId)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM sl_audit WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", auditId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Db.RowToDict(r) : null;
    }

    public static void BumpRetryCount(long auditId)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE sl_audit SET retry_count = retry_count + 1 WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", auditId);
        cmd.ExecuteNonQuery();
    }

    public static (List<Dictionary<string, object?>> Rows, int Total) ListAudit(
        string operation = "", string success = "", string q = "",
        int limit = 100, int offset = 0)
    {
        var conds = new List<string>();
        var prms = new List<(string, object)>();
        if (!string.IsNullOrEmpty(operation)) { conds.Add("a.operation = $op"); prms.Add(("$op", (object)operation)); }
        if (success == "ok") conds.Add("a.success = 1");
        else if (success == "fail")
        {
            conds.Add("a.success = 0");
            conds.Add("NOT EXISTS (SELECT 1 FROM sl_audit r WHERE r.retried_from = a.id AND r.success = 1)");
        }
        if (!string.IsNullOrEmpty(q))
        {
            conds.Add("(a.entity_key LIKE $q OR a.username LIKE $q OR a.error LIKE $q)");
            prms.Add(("$q", (object)$"%{q}%"));
        }
        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        using var conn = Db.Open();
        using var c1 = conn.CreateCommand();
        c1.CommandText = $"SELECT COUNT(*) FROM sl_audit a {where}";
        foreach (var (k, v) in prms) c1.Parameters.AddWithValue(k, v);
        var total = Convert.ToInt32(c1.ExecuteScalar());

        using var c2 = conn.CreateCommand();
        c2.CommandText = $"""
            SELECT a.id, a.created_at, a.operation, a.entity_key, a.username, a.success,
                   a.http_status, a.error, a.duration_ms, a.retry_count, a.retried_from, a.note,
                   EXISTS (SELECT 1 FROM sl_audit r
                           WHERE r.retried_from = a.id AND r.success = 1) AS resolved
            FROM sl_audit a {where}
            ORDER BY a.id DESC LIMIT $lim OFFSET $off
            """;
        foreach (var (k, v) in prms) c2.Parameters.AddWithValue(k, v);
        c2.Parameters.AddWithValue("$lim", limit);
        c2.Parameters.AddWithValue("$off", offset);
        using var r = c2.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
        while (r.Read()) rows.Add(Db.RowToDict(r));
        return (rows, total);
    }

    public static int CountFailed()
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM sl_audit a
            WHERE a.success = 0
              AND NOT EXISTS (SELECT 1 FROM sl_audit r
                              WHERE r.retried_from = a.id AND r.success = 1)
            """;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public static List<long> FailedIds(int limit = 200)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT a.id FROM sl_audit a
            WHERE a.success = 0
              AND NOT EXISTS (SELECT 1 FROM sl_audit r
                              WHERE r.retried_from = a.id AND r.success = 1)
            ORDER BY a.id DESC LIMIT $l
            """;
        cmd.Parameters.AddWithValue("$l", limit);
        using var r = cmd.ExecuteReader();
        var out_ = new List<long>();
        while (r.Read()) out_.Add(r.GetInt64(0));
        return out_;
    }
}
