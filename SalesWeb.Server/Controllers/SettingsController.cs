using Microsoft.AspNetCore.Mvc;
using SalesWeb.Server.Data;
using SalesWeb.Server.Middleware;
using SalesWeb.Server.Services;
using SalesWeb.Server.Services.B1;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Server.Controllers;

/// <summary>API cài đặt kết nối SAP B1 Service Layer (chỉ admin).</summary>
[Route("api/settings")]
public sealed class SettingsController : ApiBaseController
{
    private readonly ConnService _conn;
    private readonly B1ClientPool _pool;
    private readonly SyncService _sync;

    public SettingsController(ConnService conn, B1ClientPool pool, SyncService sync)
    {
        _conn = conn;
        _pool = pool;
        _sync = sync;
    }

    [HttpGet("connection")]
    [RequireRole("admin")]
    public ActionResult<ConnSettingsOut> GetConnection()
        => Ok(_conn.GetPublic());

    [HttpPut("connection")]
    [RequireRole("admin")]
    public async Task<ActionResult<ConnSaveOut>> SaveConnection([FromBody] ConnSettingsIn body)
    {
        ConnSettingsOut saved;
        try { saved = _conn.Save(body); }
        catch (ArgumentException ex) { throw new ApiException(400, ex.Message); }

        // Rebuild B1 client runtime để áp dụng ngay: xóa pool, client mới sẽ build lại
        // theo đúng CompanyDB từng user đã chọn lúc đăng nhập
        await _pool.ClearAsync();
        var next = _pool.GetClient(null);

        var eff = _conn.GetEffective();
        string? hint = null;
        if (eff.B1Mock)
        {
            // Nạp lại master data mock vào DB local cho tra cứu nhanh
            await _sync.SyncManyAsync(new[] { "customers", "items", "tax_codes" }, next);
        }
        else
        {
            hint = "Đã chuyển sang B1 thật — hãy vào 'Đồng bộ dữ liệu' để nạp master data.";
        }

        AuditLog.LogAction(CurrentUser.Username, "settings.connection", "Settings", "b1_connection",
            new Dictionary<string, object?>
            {
                ["b1_mock"] = eff.B1Mock, ["url"] = eff.B1ServiceLayerUrl,
            }, ClientIp);
        return Ok(new ConnSaveOut
        {
            Ok = true,
            Mode = eff.B1Mock ? "mock" : "live",
            Hint = hint,
        });
    }

    [HttpPost("connection/test")]
    [RequireRole("admin")]
    public async Task<ActionResult<ConnTestOut>> TestConnection([FromBody] ConnTestIn body)
    {
        var (ok, message) = await _conn.TestConnectionAsync(body);
        return Ok(new ConnTestOut { Ok = ok, Message = message });
    }

    [HttpGet("discount-limits")]
    [RequireRole("admin")]
    public ActionResult<DiscountLimitsOut> GetDiscountLimits()
    {
        var l = _conn.GetDiscountLimits();
        return Ok(new DiscountLimitsOut { Sales = l.Sales, Manager = l.Manager });
    }    [HttpPut("discount-limits")]
    [RequireRole("admin")]
    public ActionResult<DiscountLimitsOut> SaveDiscountLimits([FromBody] DiscountLimitsIn body)
    {
        try
        {
            var l = _conn.SaveDiscountLimits(body.Sales, body.Manager);
            AuditLog.LogAction(CurrentUser.Username, "settings.discount_limits", "Settings",
                "discount_limits",
                new Dictionary<string, object?> { ["sales"] = l.Sales, ["manager"] = l.Manager },
                ClientIp);
            return Ok(new DiscountLimitsOut { Sales = l.Sales, Manager = l.Manager });
        }
        catch (ArgumentException ex) { throw new ApiException(400, ex.Message); }
    }

    [HttpGet("databases")]
    [RequireRole("admin")]
    public ActionResult<DbListOut> GetDatabases()
        => Ok(new DbListOut { Databases = _conn.GetDatabases(), Default = _conn.DefaultCompanyDb() });

    [HttpPut("databases")]
    [RequireRole("admin")]
    public ActionResult<DbListOut> SaveDatabases([FromBody] List<DbEntry> body)
    {
        try
        {
            var saved = _conn.SaveDatabases(body);
            // Database mặc định phải nằm trong danh sách đã khai báo
            var dflt = _conn.DefaultCompanyDb();
            if (!saved.Any(d => d.Code.Equals(dflt, StringComparison.OrdinalIgnoreCase)))
                throw new ApiException(400,
                    $"Database mặc định '{dflt}' không có trong danh sách — hãy cập nhật CompanyDB trước.");
            AuditLog.LogAction(CurrentUser.Username, "settings.databases", "Settings",
                "databases",
                new Dictionary<string, object?> { ["count"] = saved.Count }, ClientIp);
            return Ok(new DbListOut { Databases = saved, Default = _conn.DefaultCompanyDb() });
        }
        catch (ArgumentException ex) { throw new ApiException(400, ex.Message); }
    }
}
