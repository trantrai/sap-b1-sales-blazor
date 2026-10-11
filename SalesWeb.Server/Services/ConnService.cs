using System.Security.Cryptography;
using System.Text;
using SalesWeb.Server.Data;
using SalesWeb.Server.Services.B1;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Server.Services;

/// <summary>Cài đặt kết nối SAP B1 Service Layer — lưu trong DB, chỉnh qua UI.
/// Thứ tự ưu tiên: DB (bảng app_settings) > appsettings/env.
/// Mật khẩu B1 được mã hoá AES-GCM (khoá suy từ jwt_secret) trước khi lưu.</summary>
public sealed class ConnService
{
    private readonly AppConfig _cfg;
    private static readonly byte[] KeySalt = Encoding.UTF8.GetBytes("b1-conn-settings-v1");

    public ConnService(AppConfig cfg) => _cfg = cfg;

    // ---------------- mã hoá mật khẩu ----------------
    private byte[] DeriveKey()
        => Rfc2898DeriveBytes.Pbkdf2(_cfg.JwtSecret, KeySalt, 200_000, HashAlgorithmName.SHA256, 32);

    private string Enc(string plain)
    {
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var pt = Encoding.UTF8.GetBytes(plain);
        var ct = new byte[pt.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        using var gcm = new AesGcm(DeriveKey(), AesGcm.TagByteSizes.MaxSize);
        gcm.Encrypt(nonce, pt, ct, tag);
        var buf = new byte[nonce.Length + ct.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, buf, 0, nonce.Length);
        Buffer.BlockCopy(ct, 0, buf, nonce.Length, ct.Length);
        Buffer.BlockCopy(tag, 0, buf, nonce.Length + ct.Length, tag.Length);
        return "agcm$" + Convert.ToBase64String(buf);
    }

    private string Dec(string token)
    {
        try
        {
            if (!token.StartsWith("agcm$")) return token; // dữ liệu cũ chưa mã hoá
            var buf = Convert.FromBase64String(token["agcm$".Length..]);
            var nonceLen = AesGcm.NonceByteSizes.MaxSize;
            var tagLen = AesGcm.TagByteSizes.MaxSize;
            var nonce = buf[..nonceLen];
            var tag = buf[^tagLen..];
            var ct = buf[nonceLen..^tagLen];
            var pt = new byte[ct.Length];
            using var gcm = new AesGcm(DeriveKey(), tagLen);
            gcm.Decrypt(nonce, ct, tag, pt);
            return Encoding.UTF8.GetString(pt);
        }
        catch { return token; } // giải mã lỗi -> trả nguyên (tương thích)
    }

    // ---------------- đọc / ghi ----------------
    private Dictionary<string, string> DbOverrides()
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM app_settings WHERE key LIKE 'b1_%'";
        using var r = cmd.ExecuteReader();
        var d = new Dictionary<string, string>();
        while (r.Read()) d[r.GetString(0)] = r.GetString(1);
        return d;
    }

    private static bool ToBool(string v)
        => v.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";

    public record ConnEffective(
        bool B1Mock, string B1ServiceLayerUrl, string B1CompanyDb, string B1Username,
        string B1Password, bool B1VerifySsl, double B1Timeout);

    public ConnEffective GetEffective()
    {
        var ov = DbOverrides();
        string G(string k, string d) => ov.TryGetValue(k, out var v) ? v : d;
        bool GB(string k, bool d) => ov.TryGetValue(k, out var v) ? ToBool(v) : d;
        var pwRaw = ov.TryGetValue("b1_password", out var pw) ? pw : null;
        return new ConnEffective(
            GB("b1_mock", _cfg.B1Mock),
            G("b1_service_layer_url", _cfg.B1ServiceLayerUrl),
            G("b1_company_db", _cfg.B1CompanyDb),
            G("b1_username", _cfg.B1Username),
            pwRaw is not null ? Dec(pwRaw) : _cfg.B1Password,
            GB("b1_verify_ssl", _cfg.B1VerifySsl),
            ov.TryGetValue("b1_timeout", out var t) && double.TryParse(t,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var tv) ? tv : _cfg.B1Timeout);
    }

    public ConnSettingsOut GetPublic()
    {
        var eff = GetEffective();
        return new ConnSettingsOut
        {
            B1Mock = eff.B1Mock,
            B1ServiceLayerUrl = eff.B1ServiceLayerUrl,
            B1CompanyDb = eff.B1CompanyDb,
            B1Username = eff.B1Username,
            B1Password = string.IsNullOrEmpty(eff.B1Password) ? "" : "********",
            B1VerifySsl = eff.B1VerifySsl,
            B1Timeout = eff.B1Timeout,
            FromDb = DbOverrides().Count > 0,
        };
    }

    public ConnSettingsOut Save(ConnSettingsIn data)
    {
        var mock = data.B1Mock;
        var url = (data.B1ServiceLayerUrl ?? "").Trim();
        if (!mock)
        {
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Service Layer URL phải bắt đầu bằng http:// hoặc https://");
            if (string.IsNullOrWhiteSpace(data.B1CompanyDb))
                throw new ArgumentException("CompanyDB không được để trống");
            if (string.IsNullOrWhiteSpace(data.B1Username))
                throw new ArgumentException("Tên đăng nhập B1 không được để trống");
            // P1-4: dùng chung policy với startup validation
            AppConfig.ValidateLiveSettings(
                url,
                data.B1Password ?? "",
                data.B1CompanyDb ?? "",
                data.B1Username ?? "",
                data.B1VerifySsl);
        }
        var timeout = Math.Min(Math.Max(data.B1Timeout == 0 ? 30 : data.B1Timeout, 5), 120);

        using var conn = Db.Open();
        using var tx = conn.BeginTransaction();
        void Put(string k, string v)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO app_settings (key, value, updated_at)
                VALUES ($k, $v, datetime('now'))
                ON CONFLICT(key) DO UPDATE SET value=excluded.value, updated_at=datetime('now')
                """;
            cmd.Parameters.AddWithValue("$k", k);
            cmd.Parameters.AddWithValue("$v", v);
            cmd.ExecuteNonQuery();
        }
        Put("b1_mock", mock ? "1" : "0");
        Put("b1_service_layer_url", url);
        Put("b1_company_db", (data.B1CompanyDb ?? "").Trim());
        Put("b1_username", (data.B1Username ?? "").Trim());
        var pw = data.B1Password ?? "";
        if (!string.IsNullOrEmpty(pw) && pw != "********")
            Put("b1_password", Enc(pw));
        Put("b1_verify_ssl", data.B1VerifySsl ? "1" : "0");
        Put("b1_timeout", timeout.ToString(System.Globalization.CultureInfo.InvariantCulture));
        tx.Commit();
        // P2-4: đổi connection settings → xoá toàn bộ master data cache
        MasterDataService.InvalidateAll();
        return GetPublic();
    }

    // ---------------- ngưỡng chiết khấu theo vai trò ----------------
    public record DiscountLimits(double Sales, double Manager);

    private double DbDouble(string key, double dflt)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_settings WHERE key = $k";
        cmd.Parameters.AddWithValue("$k", key);
        var v = cmd.ExecuteScalar() as string;
        return v is not null && double.TryParse(v,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : dflt;
    }

    /// <summary>Ngưỡng CK tối đa (%): sales mặc định 10, manager mặc định 20, admin không giới hạn.</summary>
    public DiscountLimits GetDiscountLimits()
        => new(DbDouble("disc_limit_sales", 10), DbDouble("disc_limit_manager", 20));

    public DiscountLimits SaveDiscountLimits(double sales, double manager)
    {
        if (sales < 0 || sales > 100 || manager < 0 || manager > 100)
            throw new ArgumentException("Ngưỡng chiết khấu phải nằm trong khoảng 0–100%.");
        if (sales > manager)
            throw new ArgumentException("Ngưỡng của vai trò sales không được lớn hơn manager.");
        using var conn = Db.Open();
        using var tx = conn.BeginTransaction();
        foreach (var (k, v) in new[] { ("disc_limit_sales", sales), ("disc_limit_manager", manager) })
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO app_settings (key, value, updated_at)
                VALUES ($k, $v, datetime('now'))
                ON CONFLICT(key) DO UPDATE SET value=excluded.value, updated_at=datetime('now')
                """;
            cmd.Parameters.AddWithValue("$k", k);
            cmd.Parameters.AddWithValue("$v", v.ToString(System.Globalization.CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return GetDiscountLimits();
    }

    // ---------------- danh sách database truy cập ----------------
    /// <summary>
    /// Danh sách CompanyDB được khai báo cho phép truy cập (lưu JSON trong app_settings).
    /// Nếu chưa khai báo, trả về 1 mục theo company_db hiện tại.
    /// </summary>
    public List<DbEntry> GetDatabases()
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_settings WHERE key = 'b1_databases'";
        var raw = cmd.ExecuteScalar() as string;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<DbEntry>>(raw,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (list is { Count: > 0 })
                    return list.Where(e => !string.IsNullOrWhiteSpace(e.Code))
                        .Select(e => new DbEntry
                        {
                            Code = e.Code.Trim(),
                            Name = string.IsNullOrWhiteSpace(e.Name) ? e.Code.Trim() : e.Name.Trim(),
                        }).ToList();
            }
            catch { /* JSON hỏng -> dùng mặc định */ }
        }
        var dflt = GetEffective().B1CompanyDb;
        return new List<DbEntry> { new() { Code = dflt, Name = dflt } };
    }

    public List<DbEntry> SaveDatabases(List<DbEntry> entries)
    {
        var clean = (entries ?? new())
            .Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Code))
            .Select(e => new DbEntry
            {
                Code = e.Code.Trim(),
                Name = string.IsNullOrWhiteSpace(e.Name) ? e.Code.Trim() : e.Name.Trim(),
            }).ToList();
        if (clean.Count == 0)
            throw new ArgumentException("Danh sách database phải có ít nhất một mục.");
        if (clean.Select(e => e.Code.ToUpperInvariant()).Distinct().Count() != clean.Count)
            throw new ArgumentException("Mã database không được trùng nhau.");
        var json = System.Text.Json.JsonSerializer.Serialize(clean);
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO app_settings (key, value, updated_at)
            VALUES ('b1_databases', $v, datetime('now'))
            ON CONFLICT(key) DO UPDATE SET value=excluded.value, updated_at=datetime('now')
            """;
        cmd.Parameters.AddWithValue("$v", json);
        cmd.ExecuteNonQuery();
        return clean;
    }

    // ---------------- dùng runtime ----------------
    public IB1Client BuildClient(string? companyDbOverride = null)
    {
        var eff = GetEffective();
        var db = string.IsNullOrWhiteSpace(companyDbOverride) ? eff.B1CompanyDb : companyDbOverride.Trim();
        if (eff.B1Mock)
            return new MockB1Client(_cfg.ExternalIdUdf);
        return new RealB1Client(
            eff.B1ServiceLayerUrl, db, eff.B1Username, eff.B1Password,
            eff.B1VerifySsl, eff.B1Timeout, _cfg.ExternalIdUdf);
    }

    /// <summary>CompanyDB mặc định của hệ thống (dùng khi user không chọn DB lúc login).</summary>
    public string DefaultCompanyDb() => GetEffective().B1CompanyDb;

    /// <summary>Thử đăng nhập B1 (không lưu). Trả về (ok, thông điệp).</summary>
    public async Task<(bool Ok, string Message)> TestConnectionAsync(ConnTestIn body)
    {
        var eff = GetEffective();
        var mock = body.B1Mock ?? eff.B1Mock;
        var url = !string.IsNullOrEmpty(body.B1ServiceLayerUrl) ? body.B1ServiceLayerUrl : eff.B1ServiceLayerUrl;
        var db = !string.IsNullOrEmpty(body.B1CompanyDb) ? body.B1CompanyDb : eff.B1CompanyDb;
        var user = !string.IsNullOrEmpty(body.B1Username) ? body.B1Username : eff.B1Username;
        var pw = body.B1Password is null or "" or "********" ? eff.B1Password : body.B1Password;
        var ssl = body.B1VerifySsl ?? eff.B1VerifySsl;

        if (mock)
            return (true, "Chế độ demo (dữ liệu giả) — không cần kết nối B1.");
        var client = new RealB1Client(url, db, user, pw, ssl, body.Timeout, _cfg.ExternalIdUdf);
        try
        {
            await client.LoginAsync();
            return (true, $"Kết nối thành công tới {url} (CompanyDB: {db}).");
        }
        catch (Exception ex)
        {
            return (false, $"Kết nối thất bại: {ex.Message}");
        }
        finally
        {
            await client.CloseAsync();
        }
    }
}
