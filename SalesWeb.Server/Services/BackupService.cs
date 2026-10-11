using SalesWeb.Server.Data;

namespace SalesWeb.Server.Services;

/// <summary>
/// Backup SQLite tự động (B8): chạy VACUUM INTO mỗi 24h,
/// giữ tối đa 7 bản gần nhất trong {DataDir}/backups.
/// </summary>
public sealed class BackupService : BackgroundService
{
    private readonly AppConfig _cfg;
    private readonly ILogger<BackupService> _log;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private const int KeepCount = 7;

    public BackupService(AppConfig cfg, ILogger<BackupService> log)
    {
        _cfg = cfg;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Backup 1 lần khi khởi động (trễ 1 phút để DB init xong)
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { DoBackup(); }
            catch (Exception ex) { _log.LogError(ex, "Backup SQLite thất bại"); }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    public void DoBackup()
    {
        var dir = Path.Combine(_cfg.DataDir, "backups");
        Directory.CreateDirectory(dir);
        var name = $"backup-{DateTime.Now:yyyyMMdd-HHmmss}.db";
        var dest = Path.Combine(dir, name);
        // VACUUM INTO: backup nhất quán ngay cả khi đang có kết nối mở
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "VACUUM INTO $p";
        cmd.Parameters.AddWithValue("$p", dest);
        cmd.ExecuteNonQuery();
        _log.LogInformation("Đã backup SQLite → {File}", dest);
        // Xoá bản cũ, chỉ giữ KeepCount bản mới nhất
        var old = new DirectoryInfo(dir).GetFiles("backup-*.db")
            .OrderByDescending(f => f.Name).Skip(KeepCount).ToList();
        foreach (var f in old)
        {
            try { f.Delete(); } catch { /* bỏ qua */ }
        }
    }
}
