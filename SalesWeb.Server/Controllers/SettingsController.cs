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
    private readonly B1ClientHolder _holder;
    private readonly SyncService _sync;

    public SettingsController(ConnService conn, B1ClientHolder holder, SyncService sync)
    {
        _conn = conn;
        _holder = holder;
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

        // Rebuild B1 client runtime để áp dụng ngay, không cần restart server
        var old = _holder.Current;
        IB1Client next;
        try { next = _conn.BuildClient(); }
        catch (Exception ex) { throw new ApiException(400, $"Không khởi tạo được client B1: {ex.Message}"); }
        _holder.Current = next;
        try { await old.CloseAsync(); } catch { /* bỏ qua */ }

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
    }

    [HttpPut("discount-limits")]
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
}
