using Microsoft.AspNetCore.Mvc;
using SalesWeb.Server.Data;
using SalesWeb.Server.Middleware;
using SalesWeb.Server.Services;
using SalesWeb.Server.Services.B1;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Server.Controllers;

/// <summary>API quản lý Sales Orders.</summary>
[Route("api/orders")]
public sealed class OrdersController : ApiBaseController
{
    private readonly IB1Client _b1;
    private readonly AppConfig _cfg;
    private readonly ConnService _conn;

    public OrdersController(IB1Client b1, AppConfig cfg, ConnService conn)
    {
        _b1 = b1;
        _cfg = cfg;
        _conn = conn;
    }

    private OrderService Svc(string username)
        => new(_b1, _cfg.ExternalIdUdf, username);

    /// <summary>
    /// Kiểm tra chiết khấu: 0–100%; ngưỡng theo vai trò cấu hình trong Settings
    /// (mặc định sales ≤ 10%, manager ≤ 20%, admin không giới hạn).
    /// </summary>
    private void ValidateDiscounts(double headerDisc, IEnumerable<double> lineDiscs)
    {
        var all = lineDiscs.Prepend(headerDisc).ToList();
        if (all.Any(d => d < 0 || d > 100))
            throw new ApiException(400, "Chiết khấu phải nằm trong khoảng 0–100%.");
        var role = CurrentUser.Role;
        var limits = _conn.GetDiscountLimits();
        double limit = role switch { "admin" => 100, "manager" => limits.Manager, _ => limits.Sales };
        var over = all.FirstOrDefault(d => d > limit);
        if (over > limit)
            throw new ApiException(403,
                $"Vai trò '{role}' chỉ được áp chiết khấu tối đa {FmtLimit(limit)}% (đang nhập {FmtLimit(over)}%).");
    }

    private static string FmtLimit(double n)
        => n.ToString("N2", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');

    /// <summary>Thống kê dashboard (A1).</summary>
    [HttpGet("stats")]
    [RequireRole("viewer")]
    public async Task<ActionResult<DashboardStats>> Stats()
        => Ok(await Svc(CurrentUser.Username).GetDashboardStatsAsync());

    /// <summary>Xuất Excel danh sách đơn hàng (A2).</summary>
    [HttpGet("export")]
    [RequireRole("viewer")]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] string status = "",
        [FromQuery] string? card_code = null,
        [FromQuery] DateOnly? date_from = null,
        [FromQuery] DateOnly? date_to = null)
    {
        var user = CurrentUser;
        if (user.Role == "sales") { /* sales chỉ xuất đơn của mình — lọc theo sales_person_code */ }
        var paged = await Svc(user.Username).ListAsync(status, card_code,
            user.Role == "sales" ? user.SalesPersonCode : null,
            date_from, date_to, 1000, 0);
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("DonHang");
        var headers = new[] { "Số đơn", "Ngày", "Mã KH", "Tên KH", "Tổng tiền", "Trạng thái", "Ghi chú" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#1E3A5F");
            ws.Cell(1, i + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
        }
        int row = 2;
        foreach (var o in paged.Items)
        {
            ws.Cell(row, 1).Value = o.DocNum;
            ws.Cell(row, 2).Value = o.DocDate;
            ws.Cell(row, 3).Value = o.CardCode;
            ws.Cell(row, 4).Value = o.CardName;
            ws.Cell(row, 5).Value = o.DocTotal;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 6).Value = o.Status switch
            {
                "open" => "Đang mở", "closed" => "Đã đóng",
                "cancelled" => "Đã huỷ", _ => o.Status,
            };
            ws.Cell(row, 7).Value = o.Comments;
            row++;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        AuditLog.LogAction(user.Username, "order.export", "Orders", $"excel:{paged.Items.Count}",
            new Dictionary<string, object?> { ["status"] = status, ["count"] = paged.Items.Count }, ClientIp);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"donhang-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpGet("")]
    [RequireRole("viewer")]
    public async Task<ActionResult<PagedOrders>> List(
        [FromQuery] string status = "open",
        [FromQuery] string? card_code = null,
        [FromQuery] int? sales_person_code = null,
        [FromQuery] DateOnly? date_from = null,
        [FromQuery] DateOnly? date_to = null,
        [FromQuery] int top = 50,
        [FromQuery] int skip = 0)
    {
        var user = CurrentUser;
        // NVKD thường chỉ xem đơn của mình
        if (user.Role == "sales" && user.SalesPersonCode.HasValue && sales_person_code is null)
            sales_person_code = user.SalesPersonCode;
        top = Math.Min(top, 200);
        return Ok(await Svc(user.Username).ListAsync(
            status, card_code, sales_person_code, date_from, date_to, top, skip));
    }

    [HttpGet("{docEntry:int}")]
    [RequireRole("viewer")]
    public async Task<ActionResult<OrderOut>> Get(int docEntry)
    {
        var (order, etag) = await Svc(CurrentUser.Username).GetAsync(docEntry);
        if (!string.IsNullOrEmpty(etag))
            Response.Headers["ETag"] = etag;
        return Ok(order);
    }

    [HttpPost("")]
    [RequireRole("sales")]
    public async Task<ActionResult<OrderOut>> Create([FromBody] OrderCreate body)
    {
        var user = CurrentUser;
        // Mặc định gán NVKD tạo đơn
        if (body.SalesPersonCode is null && user.SalesPersonCode.HasValue)
            body.SalesPersonCode = user.SalesPersonCode;
        ValidateDiscounts(body.DiscountPercent, body.Lines.Select(l => l.DiscountPercent));
        var (order, created) = await Svc(user.Username).CreateAsync(body);
        AuditLog.LogAction(user.Username, "order.create", "Orders", order.DocEntry.ToString(),
            new Dictionary<string, object?>
            {
                ["doc_num"] = order.DocNum, ["card_code"] = order.CardCode,
                ["total"] = order.DocTotal, ["idempotent_hit"] = !created,
            }, ClientIp);
        return StatusCode(201, order);
    }

    [HttpPatch("{docEntry:int}")]
    [RequireRole("sales")]
    public async Task<ActionResult<OrderOut>> Update(int docEntry, [FromBody] OrderUpdate body)
    {
        if (body.DiscountPercent.HasValue || body.Lines?.Any(l => l.DiscountPercent.HasValue) == true)
            ValidateDiscounts(body.DiscountPercent ?? 0,
                (body.Lines ?? new()).Select(l => l.DiscountPercent ?? 0));
        var order = await Svc(CurrentUser.Username).UpdateAsync(docEntry, body);
        var detail = new Dictionary<string, object?>();
        if (body.Comments is not null) detail["comments"] = body.Comments;
        if (body.DocDueDate.HasValue) detail["doc_due_date"] = body.DocDueDate.Value.ToString("yyyy-MM-dd");
        if (body.NumAtCard is not null) detail["num_at_card"] = body.NumAtCard;
        AuditLog.LogAction(CurrentUser.Username, "order.update", "Orders", docEntry.ToString(),
            detail, ClientIp);
        return Ok(order);
    }

    [HttpPost("{docEntry:int}/cancel")]
    [RequireRole("manager")]
    public async Task<ActionResult<OrderOut>> Cancel(int docEntry)
    {
        var order = await Svc(CurrentUser.Username).CancelAsync(docEntry);
        AuditLog.LogAction(CurrentUser.Username, "order.cancel", "Orders", docEntry.ToString(),
            new Dictionary<string, object?> { ["doc_num"] = order.DocNum }, ClientIp);
        return Ok(order);
    }

    [HttpPost("{docEntry:int}/close")]
    [RequireRole("manager")]
    public async Task<ActionResult<OrderOut>> Close(int docEntry, [FromBody] CloseLinesIn body)
    {
        var order = await Svc(CurrentUser.Username).CloseLinesAsync(docEntry, body.LineNums);
        AuditLog.LogAction(CurrentUser.Username, "order.close", "Orders", docEntry.ToString(),
            new Dictionary<string, object?> { ["line_nums"] = body.LineNums }, ClientIp);
        return Ok(order);
    }
}
