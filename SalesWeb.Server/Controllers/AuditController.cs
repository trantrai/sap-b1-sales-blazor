using Microsoft.AspNetCore.Mvc;
using SalesWeb.Server.Data;
using SalesWeb.Server.Services;
using SalesWeb.Server.Services.B1;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Server.Controllers;

/// <summary>API giám sát tích hợp Service Layer + retry.</summary>
[Route("api/audit")]
public sealed class AuditController : ApiBaseController
{
    private readonly IB1Client _b1;
    private readonly AppConfig _cfg;

    public AuditController(IB1Client b1, AppConfig cfg)
    {
        _b1 = b1;
        _cfg = cfg;
    }

    private static AuditOut ToOut(Dictionary<string, object?> r) => new()
    {
        Id = Convert.ToInt32(r["id"]),
        CreatedAt = r["created_at"]?.ToString() ?? "",
        Operation = r["operation"]?.ToString() ?? "",
        EntityKey = r["entity_key"]?.ToString() ?? "",
        Username = r["username"]?.ToString() ?? "",
        Success = Convert.ToInt32(r["success"]) == 1,
        Error = r["error"]?.ToString() ?? "",
        DurationMs = Convert.ToInt32(r["duration_ms"]),
        RetryCount = Convert.ToInt32(r["retry_count"]),
        RetriedFrom = r["retried_from"] is null ? null : Convert.ToInt32(r["retried_from"]),
        Note = r["note"]?.ToString() ?? "",
        RetriedOk = Convert.ToInt32(r["resolved"]) == 1,
    };

    [HttpGet("")]
    [RequireRole("manager")]
    public ActionResult<PagedAudit> List(
        [FromQuery] string operation = "",
        [FromQuery] string status = "",
        [FromQuery] string q = "",
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        var (rows, total) = SlAuditStore.ListAudit(operation, status, q,
            Math.Min(limit, 500), offset);
        return Ok(new PagedAudit
        {
            Total = total,
            Items = rows.Select(ToOut).ToList(),
        });
    }

    [HttpGet("failed-count")]
    [RequireRole("manager")]
    public ActionResult<FailedCountOut> FailedCount()
        => Ok(new FailedCountOut { Failed = SlAuditStore.CountFailed() });

    [HttpGet("{id:long}")]
    [RequireRole("manager")]
    public ActionResult<AuditDetailOut> Get(long id)
    {
        var r = SlAuditStore.Get(id)
            ?? throw new Middleware.ApiException(404, "Không tìm thấy bản ghi audit.");
        var out_ = ToOut(r);
        return Ok(new AuditDetailOut
        {
            Id = out_.Id, CreatedAt = out_.CreatedAt, Operation = out_.Operation,
            EntityKey = out_.EntityKey, Username = out_.Username, Success = out_.Success,
            Error = out_.Error, DurationMs = out_.DurationMs, RetryCount = out_.RetryCount,
            RetriedFrom = out_.RetriedFrom, Note = out_.Note, RetriedOk = out_.RetriedOk,
            RequestJson = System.Text.Json.JsonDocument.Parse(r["request_json"]?.ToString() ?? "{}").RootElement,
            ResponseJson = System.Text.Json.JsonDocument.Parse(string.IsNullOrEmpty(r["response_json"]?.ToString()) ? "{}" : r["response_json"]!.ToString()!).RootElement,
        });
    }

    [HttpPost("{id:long}/retry")]
    [RequireRole("manager")]
    public async Task<ActionResult<RetryOut>> RetryOne(long id)
        => Ok(await new SlRetryService(_cfg).RetryAuditAsync(id, _b1, CurrentUser.Username));

    [HttpPost("retry-all")]
    [RequireRole("manager")]
    public async Task<ActionResult<RetryAllOut>> RetryAll()
        => Ok(await new SlRetryService(_cfg).RetryAllFailedAsync(_b1, CurrentUser.Username));
}
