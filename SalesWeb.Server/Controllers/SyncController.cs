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
    private readonly B1ClientPool _pool;
    private readonly SyncService _sync;
    private readonly ILogger<SyncController> _logger;

    public SyncController(B1ClientPool pool, SyncService sync, ILogger<SyncController> logger)
    {
        _pool = pool;
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
        // M1: status theo CompanyDB của user hiện tại
        var companyDb = CurrentUser.CompanyDb ?? "";
        var rows = MdStore.SyncStatus(companyDb);
        var tables = rows.Select(r => new SyncTableOut
        {
            Table = r["entity"]?.ToString() ?? "",
            Count = Convert.ToInt32(r["count"]),
            LastSync = r["last_finished_at"]?.ToString(),
            LastRecords = Convert.ToInt32(r["last_records"]),
        }).ToList();
        var running = rows.Any(r => SyncService.IsRunning(r["entity"]?.ToString() ?? "", companyDb));
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
        // Fix #4: snapshot CompanyDB trước khi chạy nền — không truyền scoped proxy vào Task.Run
        // Fix #9: tạo job có ID để theo dõi
        var companyDb = CurrentUser.CompanyDb;
        var username = CurrentUser.Username;
        var jobId = Guid.NewGuid().ToString("N")[..12];
        var cts = new CancellationTokenSource();
        SyncService.TrackJob(jobId, new SyncJobInfo
        {
            JobId = jobId,
            CompanyDb = companyDb ?? "",
            StartedBy = username,
            StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Entities = entities,
            Cancellation = cts,
        });
        // P1-3: không truyền token vào Task.Run để delegate không bị skip mà không finalize.
        // Cancellation được kiểm tra trong vòng lặp; đảm bảo FinishJob luôn được gọi.
        _ = Task.Run(async () =>
        {
            var total = 0;
            string error = "";
            try
            {
                var b1 = _pool.GetClient(companyDb);
                foreach (var e in entities)
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        error = "Đã huỷ bởi người dùng";
                        break;
                    }
                    var (t, err) = await _sync.SyncEntityAsync(e, b1, companyDb);
                    total += t;
                    if (!string.IsNullOrEmpty(err)) error = err;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Đồng bộ nền lỗi");
                error = ex.Message;
            }
            finally
            {
                // P1-3: luôn finalize — job không bao giờ kẹt ở running
                SyncService.FinishJob(jobId, total, error);
                cts.Dispose();
            }
        });
        return Ok(new SyncStartOut
        {
            Started = true,
            JobId = jobId,
            Message = $"Đã bắt đầu đồng bộ: {string.Join(", ", entities.Select(e => SyncService.EntityLabel[e]))}",
        });
    }

    /// <summary>Trạng thái job đồng bộ (fix #9, M2: chỉ xem job của đúng CompanyDB).</summary>
    [HttpGet("job/{jobId}")]
    [RequireRole("viewer")]
    public ActionResult<SyncJobOut> JobStatus(string jobId)
    {
        var job = SyncService.GetJob(jobId);
        // M2: 404 thay vì 403 để không lộ sự tồn tại của job thuộc DB khác
        if (job is null || job.CompanyDb != (CurrentUser.CompanyDb ?? "")) return NotFound();
        return Ok(new SyncJobOut
        {
            JobId = job.JobId,
            Status = job.FinishedAt is null ? "running" : (string.IsNullOrEmpty(job.Error) ? "done" : "error"),
            CompanyDb = job.CompanyDb,
            StartedBy = job.StartedBy,
            StartedAt = job.StartedAt,
            FinishedAt = job.FinishedAt,
            TotalRecords = job.TotalRecords,
            Error = job.Error,
            Entities = job.Entities,
        });
    }
}
