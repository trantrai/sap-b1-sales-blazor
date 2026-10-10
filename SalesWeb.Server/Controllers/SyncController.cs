using Microsoft.AspNetCore.Mvc;
using SalesWeb.Server.Data;
using SalesWeb.Server.Services;
using SalesWeb.Server.Services.B1;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Server.Controllers;

/// <summary>API đồng bộ master data B1 -&gt; DB local.</summary>
[Route("api/sync")]
public sealed class SyncController : ApiBaseController
{
    private readonly IB1Client _b1;
    private readonly SyncService _sync;
    private readonly ILogger<SyncController> _logger;

    public SyncController(IB1Client b1, SyncService sync, ILogger<SyncController> logger)
    {
        _b1 = b1;
        _sync = sync;
        _logger = logger;
    }

    public sealed class SyncTriggerIn
    {
        public List<string>? Entities { get; set; }
    }

    [HttpGet("status")]
    [RequireRole("viewer")]
    public ActionResult<SyncStatusOut> Status()
    {
        var rows = MdStore.SyncStatus();
        var tables = rows.Select(r => new SyncTableOut
        {
            Table = r["entity"]?.ToString() ?? "",
            Count = Convert.ToInt32(r["count"]),
            LastSync = r["last_finished_at"]?.ToString(),
            LastRecords = Convert.ToInt32(r["last_records"]),
        }).ToList();
        var running = rows.Any(r => SyncService.IsRunning(r["entity"]?.ToString() ?? ""));
        var latest = rows
            .OrderByDescending(r => r["last_started_at"]?.ToString())
            .FirstOrDefault();
        return Ok(new SyncStatusOut
        {
            Running = running,
            StartedAt = latest?["last_started_at"]?.ToString(),
            FinishedAt = latest?["last_finished_at"]?.ToString(),
            Error = latest?["last_error"]?.ToString(),
            Tables = tables,
        });
    }

    [HttpPost("")]
    [RequireRole("manager")]
    public ActionResult<SyncStartOut> Trigger([FromBody] SyncTriggerIn? body)
    {
        var entities = (body?.Entities ?? new List<string>())
            .Where(e => SyncService.Entities.Contains(e)).ToList();
        if (entities.Count == 0) entities = SyncService.Entities.ToList();
        var b1 = _b1;
        _ = Task.Run(async () =>
        {
            try { await _sync.SyncManyAsync(entities, b1); }
            catch (Exception ex) { _logger.LogError(ex, "Đồng bộ nền lỗi"); }
        });
        return Ok(new SyncStartOut
        {
            Started = true,
            Message = $"Đã bắt đầu đồng bộ: {string.Join(", ", entities.Select(e => SyncService.EntityLabel[e]))}",
        });
    }
}
