using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.IdentityModel.Tokens;
using SalesWeb.Server.Data;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Server.Services;

/// <summary>Bản ghi user nội bộ (từ DB).</summary>
public sealed record AppUser(string Username, string FullName, string Role, int? SalesPersonCode, bool Active, string CompanyDb = "");

/// <summary>Auth web: JWT + RBAC. Tài khoản web map tới B1 qua sales_person_code (SlpCode).</summary>
public sealed class AuthService
{
    public static readonly Dictionary<string, int> RoleRank = new()
    {
        ["viewer"] = 0, ["sales"] = 1, ["manager"] = 2, ["admin"] = 3,
    };

    public static readonly Dictionary<string, string> RoleLabels = new()
    {
        ["viewer"] = "Người xem",
        ["sales"] = "Nhân viên kinh doanh",
        ["manager"] = "Quản lý",
        ["admin"] = "Quản trị viên",
    };

    public static readonly List<(string Key, string Label, string[] Roles)> PermissionMatrix = new()
    {
        ("orders.view", "Xem danh sách & chi tiết đơn hàng", new[] { "viewer", "sales", "manager", "admin" }),
        ("masterdata.view", "Tra cứu KH / hàng hoá / thuế suất", new[] { "viewer", "sales", "manager", "admin" }),
        ("audit.view", "Xem giám sát tích hợp", new[] { "viewer", "sales", "manager", "admin" }),
        ("orders.create", "Tạo đơn hàng", new[] { "sales", "manager", "admin" }),
        ("orders.update", "Sửa đơn hàng (ghi chú, ngày giao…)", new[] { "sales", "manager", "admin" }),
        ("orders.cancel_close", "Huỷ / đóng đơn hàng", new[] { "manager", "admin" }),
        ("masterdata.sync", "Đồng bộ master data vào DB local", new[] { "manager", "admin" }),
        ("audit.retry", "Thử lại thao tác tích hợp lỗi", new[] { "manager", "admin" }),
        ("users.manage", "Quản lý người dùng & phân quyền", new[] { "admin" }),
        ("settings.connection", "Cài đặt kết nối Service Layer", new[] { "admin" }),
    };

    private readonly AppConfig _cfg;

    public AuthService(AppConfig cfg) => _cfg = cfg;

    // ---------------- Password (PBKDF2-HMAC-SHA256, tương thích bản Python) ----------------
    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var dk = Rfc2898DeriveBytes.Pbkdf2(password, salt, 200_000, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2$200000${Convert.ToHexString(salt).ToLowerInvariant()}${Convert.ToHexString(dk).ToLowerInvariant()}";
    }

    public static bool VerifyPassword(string password, string hashed)
    {
        try
        {
            var parts = hashed.Split('$');
            var iters = int.Parse(parts[1]);
            var salt = Convert.FromHexString(parts[2]);
            var expected = Convert.FromHexString(parts[3]);
            var dk = Rfc2898DeriveBytes.Pbkdf2(password, salt, iters, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(dk, expected);
        }
        catch { return false; }
    }

    // ---------------- JWT ----------------
    public string CreateToken(string username, string role, string companyDb = "")
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg.JwtSecret));
        var claims = new List<Claim>
        {
            new("sub", username),
            new("role", role),
        };
        if (!string.IsNullOrWhiteSpace(companyDb))
            claims.Add(new Claim("b1_db", companyDb.Trim()));
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_cfg.JwtExpireMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return handler.WriteToken(token);
    }

    public static byte[] JwtKey(AppConfig cfg) => Encoding.UTF8.GetBytes(cfg.JwtSecret);

    // ---------------- Refresh token (B5) ----------------
    /// <summary>Thời hạn refresh token: 7 ngày.</summary>
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public string IssueRefreshToken(string username, string companyDb = "")
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var hash = Sha256(raw);
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO refresh_tokens (token_hash, username, company_db, expires_at)
            VALUES ($h, $u, $db, datetime('now', '+7 days'))
            """;
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$u", username);
        cmd.Parameters.AddWithValue("$db", companyDb ?? "");
        cmd.ExecuteNonQuery();
        // Dọn token hết hạn/cũ của user (giữ tối đa 5)
        using var clean = conn.CreateCommand();
        clean.CommandText = """
            DELETE FROM refresh_tokens
            WHERE username = $u AND id NOT IN (
                SELECT id FROM refresh_tokens WHERE username = $u
                ORDER BY id DESC LIMIT 5)
            """;
        clean.Parameters.AddWithValue("$u", username);
        clean.ExecuteNonQuery();
        return raw;
    }

    /// <summary>Đổi refresh token lấy cặp token mới (xoay vòng: thu hồi token cũ).</summary>
    public (string Username, string CompanyDb)? ConsumeRefreshToken(string rawToken)
    {
        var hash = Sha256(rawToken ?? "");
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT username, company_db FROM refresh_tokens
            WHERE token_hash = $h AND revoked = 0 AND datetime(expires_at) > datetime('now')
            """;
        cmd.Parameters.AddWithValue("$h", hash);
        using var rd = cmd.ExecuteReader();
        if (!rd.Read()) return null;
        var username = rd.GetString(0);
        var companyDb = rd.IsDBNull(1) ? "" : rd.GetString(1);
        rd.Close();
        // Thu hồi token vừa dùng (xoay vòng)
        using var rev = conn.CreateCommand();
        rev.CommandText = "UPDATE refresh_tokens SET revoked = 1 WHERE token_hash = $h";
        rev.Parameters.AddWithValue("$h", hash);
        rev.ExecuteNonQuery();
        // User còn active không?
        var user = GetUser(username);
        if (user is null || !user.Active) return null;
        return (username, companyDb);
    }

    public void RevokeRefreshToken(string rawToken)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE refresh_tokens SET revoked = 1 WHERE token_hash = $h";
        cmd.Parameters.AddWithValue("$h", Sha256(rawToken ?? ""));
        cmd.ExecuteNonQuery();
    }

    private static string Sha256(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes);
    }

    // ---------------- Users ----------------
    public static AppUser? GetUser(string username)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT username, full_name, role, sales_person_code, active, password_hash FROM users WHERE username = $u";
        cmd.Parameters.AddWithValue("$u", username);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new AppUser(
            r.GetString(0),
            r.IsDBNull(1) ? "" : r.GetString(1),
            r.GetString(2),
            r.IsDBNull(3) ? null : r.GetInt32(3),
            r.GetInt32(4) == 1);
    }

    public static string? GetPasswordHash(string username)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT password_hash FROM users WHERE username = $u";
        cmd.Parameters.AddWithValue("$u", username);
        return cmd.ExecuteScalar()?.ToString();
    }

    public static AppUser? Authenticate(string username, string password)
    {
        var user = GetUser(username);
        if (user is null || !user.Active) return null;
        var hash = GetPasswordHash(username);
        if (hash is null || !VerifyPassword(password, hash)) return null;
        return user;
    }

    public static UserOut ToUserOut(AppUser u) => new()
    {
        Username = u.Username,
        FullName = u.FullName,
        Role = u.Role,
        SalesPersonCode = u.SalesPersonCode,
        Active = u.Active,
        CompanyDb = u.CompanyDb,
    };

    public static List<AppUser> ListUsers()
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT username, full_name, role, sales_person_code, active FROM users ORDER BY username";
        using var r = cmd.ExecuteReader();
        var out_ = new List<AppUser>();
        while (r.Read())
            out_.Add(new AppUser(r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1),
                r.GetString(2), r.IsDBNull(3) ? null : r.GetInt32(3), r.GetInt32(4) == 1));
        return out_;
    }

    public static RoleMatrix GetRoleMatrix() => new()
    {
        Roles = new[] { "viewer", "sales", "manager", "admin" }
            .Select(c => new RoleInfo { Code = c, Label = RoleLabels[c] }).ToList(),
        Permissions = PermissionMatrix
            .Select(p => new PermissionInfo { Key = p.Key, Label = p.Label, Roles = p.Roles.ToList() }).ToList(),
    };
}

/// <summary>Attribute phân quyền: [RequireRole("sales")] — chấp nhận role >= role thấp nhất.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireRoleAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _roles;
    public RequireRoleAttribute(params string[] roles) => _roles = roles;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var principal = http.User;
        var hasHeader = http.Request.Headers.TryGetValue("Authorization", out var hv)
            && !string.IsNullOrWhiteSpace(hv.ToString());

        if (principal?.Identity?.IsAuthenticated != true)
        {
            context.Result = new JsonResult(new
            {
                detail = hasHeader ? "Token không hợp lệ hoặc đã hết hạn" : "Thiếu token đăng nhập",
            }, ServerJson.Options) { StatusCode = 401 };
            return Task.CompletedTask;
        }

        var username = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var user = AuthService.GetUser(username);
        if (user is null || !user.Active)
        {
            context.Result = new JsonResult(new { detail = "Token không hợp lệ hoặc đã hết hạn" },
                ServerJson.Options) { StatusCode = 401 };
            return Task.CompletedTask;
        }

        var minRank = _roles.Min(r => AuthService.RoleRank[r]);
        if (!AuthService.RoleRank.TryGetValue(user.Role, out var rank) || rank < minRank)
        {
            context.Result = new JsonResult(new { detail = "Không có quyền thực hiện thao tác này" },
                ServerJson.Options) { StatusCode = 403 };
            return Task.CompletedTask;
        }

        // Database user đã chọn lúc đăng nhập (claim b1_db); không có -> DB mặc định
        var claimDb = principal.FindFirst("b1_db")?.Value;
        var conn = http.RequestServices.GetService<ConnService>();
        var companyDb = string.IsNullOrWhiteSpace(claimDb)
            ? conn?.DefaultCompanyDb() ?? ""
            : claimDb.Trim();
        http.Items["AppUser"] = user with { CompanyDb = companyDb };
        return Task.CompletedTask;
    }
}
