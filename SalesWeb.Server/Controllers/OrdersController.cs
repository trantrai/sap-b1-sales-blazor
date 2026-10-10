using Microsoft.AspNetCore.Mvc;
using SalesWeb.Server.Data;
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

    public OrdersController(IB1Client b1, AppConfig cfg)
    {
        _b1 = b1;
        _cfg = cfg;
    }

    private OrderService Svc(string username)
        => new(_b1, _cfg.ExternalIdUdf, username);

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
