using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SalesWeb.Server.Data;
using SalesWeb.Server.Middleware;
using SalesWeb.Server.Services;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Server.Controllers;

/// <summary>Đăng nhập web + quản trị tài khoản.</summary>
[Route("api/auth")]
public sealed class AuthController : ApiBaseController
{
    private readonly AuthService _auth;
    private readonly ConnService _conn;

    public AuthController(AuthService auth, ConnService conn)
    {
        _auth = auth;
        _conn = conn;
    }

    /// <summary>Danh sách database được khai báo (public — dùng ở màn Login).</summary>
    [HttpGet("databases")]
    public ActionResult<DbListOut> Databases()
    {
        var dbs = _conn.GetDatabases();
        return Ok(new DbListOut { Databases = dbs, Default = _conn.DefaultCompanyDb() });
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public ActionResult<TokenOut> Login([FromBody] LoginIn body)
    {
        var user = AuthService.Authenticate(body.Username, body.Password);
        if (user is null)
            throw new ApiException(401, "Sai tên đăng nhập hoặc mật khẩu");
        var companyDb = (body.CompanyDb ?? "").Trim();
        if (string.IsNullOrEmpty(companyDb))
            companyDb = _conn.DefaultCompanyDb();
        else if (!_conn.GetDatabases().Any(d =>
                     d.Code.Equals(companyDb, StringComparison.OrdinalIgnoreCase)))
            throw new ApiException(400, $"Database '{companyDb}' chưa được khai báo trong hệ thống.");
        AuditLog.LogAction(user.Username, "auth.login", "User", user.Username,
            new Dictionary<string, object?> { ["company_db"] = companyDb }, ClientIp);
        var cfg = HttpContext.RequestServices.GetRequiredService<AppConfig>();
        return Ok(new TokenOut
        {
            AccessToken = _auth.CreateToken(user.Username, user.Role, companyDb),
            TokenType = "bearer",
            RefreshToken = _auth.IssueRefreshToken(user.Username, companyDb),
            ExpiresIn = cfg.JwtExpireMinutes * 60,
        });
    }

    /// <summary>Đổi refresh token lấy cặp token mới (B5).</summary>
    [HttpPost("refresh")]
    public ActionResult<TokenOut> Refresh([FromBody] RefreshIn body)
    {
        var result = _auth.ConsumeRefreshToken(body.RefreshToken);
        if (result is null)
            throw new ApiException(401, "Refresh token không hợp lệ hoặc đã hết hạn");
        var (username, companyDb) = result.Value;
        var user = AuthService.GetUser(username);
        if (user is null || !user.Active)
            throw new ApiException(401, "Tài khoản đã bị vô hiệu hoá");
        var cfg = HttpContext.RequestServices.GetRequiredService<AppConfig>();
        AuditLog.LogAction(username, "auth.refresh", "User", username, null, ClientIp);
        return Ok(new TokenOut
        {
            AccessToken = _auth.CreateToken(username, user.Role, companyDb),
            TokenType = "bearer",
            RefreshToken = _auth.IssueRefreshToken(username, companyDb),
            ExpiresIn = cfg.JwtExpireMinutes * 60,
        });
    }

    /// <summary>Đăng xuất — thu hồi refresh token (B5).</summary>
    [HttpPost("logout")]
    public ActionResult Logout([FromBody] RefreshIn body)
    {
        if (!string.IsNullOrWhiteSpace(body.RefreshToken))
            _auth.RevokeRefreshToken(body.RefreshToken);
        return Ok(new { ok = true });
    }

    [HttpGet("me")]
    [RequireRole("viewer")]
    public ActionResult<UserOut> Me() => Ok(AuthService.ToUserOut(CurrentUser));

    [HttpGet("users")]
    [RequireRole("admin")]
    public ActionResult<List<UserOut>> ListUsers()
        => Ok(AuthService.ListUsers().Select(AuthService.ToUserOut).ToList());

    [HttpPost("users")]
    [RequireRole("admin")]
    public ActionResult<UserOut> CreateUser([FromBody] UserCreate body)
    {
        if (AuthService.GetUser(body.Username) is not null)
            throw new ApiException(400, "Tên đăng nhập đã tồn tại");
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO users (username, password_hash, full_name, role, sales_person_code)
            VALUES ($u, $p, $f, $r, $s)
            """;
        cmd.Parameters.AddWithValue("$u", body.Username);
        cmd.Parameters.AddWithValue("$p", AuthService.HashPassword(body.Password));
        cmd.Parameters.AddWithValue("$f", (object?)body.FullName ?? "");
        cmd.Parameters.AddWithValue("$r", string.IsNullOrEmpty(body.Role) ? "viewer" : body.Role);
        cmd.Parameters.AddWithValue("$s", (object?)body.SalesPersonCode ?? DBNull.Value);
        cmd.ExecuteNonQuery();
        AuditLog.LogAction(CurrentUser.Username, "user.create", "User", body.Username, null, ClientIp);
        return StatusCode(201, AuthService.ToUserOut(AuthService.GetUser(body.Username)!));
    }

    [HttpGet("roles")]
    [RequireRole("admin")]
    public ActionResult<RoleMatrix> RoleMatrix() => Ok(AuthService.GetRoleMatrix());

    [HttpPatch("users/{username}")]
    [RequireRole("admin")]
    public ActionResult<UserOut> UpdateUser(string username, [FromBody] UserUpdate body)
    {
        var target = AuthService.GetUser(username)
            ?? throw new ApiException(404, "Không tìm thấy người dùng");
        var admin = CurrentUser;
        if (username == admin.Username)
        {
            if (body.Active == false)
                throw new ApiException(400, "Không thể khoá chính tài khoản của mình");
            if (body.Role is not null && body.Role != target.Role)
                throw new ApiException(400, "Không thể tự đổi vai trò của mình");
        }
        var sets = new List<string>();
        var prms = new List<(string, object)>();
        var changed = new Dictionary<string, object?>();
        if (body.FullName is not null) { sets.Add("full_name = $f"); prms.Add(("$f", (object)body.FullName)); changed["full_name"] = body.FullName; }
        if (body.Role is not null) { sets.Add("role = $r"); prms.Add(("$r", (object)body.Role)); changed["role"] = body.Role; }
        if (body.SalesPersonCode.HasValue) { sets.Add("sales_person_code = $s"); prms.Add(("$s", (object)body.SalesPersonCode.Value)); changed["sales_person_code"] = body.SalesPersonCode.Value; }
        if (body.Active.HasValue) { sets.Add("active = $a"); prms.Add(("$a", (object)(body.Active.Value ? 1 : 0))); changed["active"] = body.Active.Value; }
        if (!string.IsNullOrEmpty(body.Password)) { sets.Add("password_hash = $p"); prms.Add(("$p", (object)AuthService.HashPassword(body.Password))); }
        if (sets.Count == 0)
            throw new ApiException(400, "Không có gì để cập nhật");

        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE users SET {string.Join(", ", sets)} WHERE username = $u";
        foreach (var (k, v) in prms) cmd.Parameters.AddWithValue(k, v);
        cmd.Parameters.AddWithValue("$u", username);
        cmd.ExecuteNonQuery();

        AuditLog.LogAction(admin.Username, "user.update", "User", username, changed, ClientIp);
        return Ok(AuthService.ToUserOut(AuthService.GetUser(username)!));
    }

    [HttpGet("audit")]
    [RequireRole("manager")]
    public ActionResult<List<Dictionary<string, object?>>> Audit([FromQuery] int limit = 100)
        => Ok(AuditLog.RecentLogs(Math.Min(limit, 500)));
}
