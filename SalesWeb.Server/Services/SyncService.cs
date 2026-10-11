using SalesWeb.Server.Data;
using SalesWeb.Server.Services.B1;

namespace SalesWeb.Server.Services;

/// <summary>Đồng bộ master data B1 -&gt; DB local (chạy nền, ghi log, chống trùng).</summary>
public sealed class SyncService
{
    public static readonly string[] Entities = { "customers", "items", "tax_codes" };
    public static readonly Dictionary<string, string> EntityLabel = new()
    {
        ["customers"] = "Khách hàng",
        ["items"] = "Mặt hàng",
        ["tax_codes"] = "Thuế suất",
    };

    // Chống chạy trùng cùng 1 entity + CompanyDB
    private static readonly HashSet<string> Running = new();
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly ILogger<SyncService>? _logger;

    public SyncService(ILogger<SyncService>? logger = null) => _logger = logger;

    public static bool IsRunning(string entity, string? companyDb = null)
    {
        lock (Running) return Running.Contains(Key(entity, companyDb));
    }

    private static string Key(string entity, string? companyDb)
        => $"{companyDb ?? ""}:{entity}";

    /// <summary>Đồng bộ 1 entity cho 1 CompanyDB. Trả về (số bản ghi, lỗi nếu có).</summary>
    public async Task<(int Total, string Error)> SyncEntityAsync(string entity, IB1Client b1, string? companyDb = null)
    {
        if (!Entities.Contains(entity))
            return (0, $"Entity không hợp lệ: {entity}");

        var key = Key(entity, companyDb);
        await Gate.WaitAsync();
        try
        {
            lock (Running)
            {
                if (Running.Contains(key)) return (0, "Đang đồng bộ, vui lòng đợi");
                Running.Add(key);
            }
        }
        finally { Gate.Release(); }

        var logId = MdStore.LogStart(entity);
        int total = 0;
        string error = "";
        var db = string.IsNullOrWhiteSpace(companyDb) ? "" : companyDb.Trim();
        try
        {
            if (entity == "customers")
            {
                int skip = 0;
                while (true)
                {
                    var rows = await b1.FetchCustomersPageAsync(500, skip);
                    if (rows.Count == 0) break;
                    total += MdStore.UpsertCustomers(db, rows);
                    skip += rows.Count;
                    if (rows.Count < 500) break;
                }
            }
            else if (entity == "items")
            {
                int skip = 0;
                while (true)
                {
                    var rows = await b1.FetchItemsPageAsync(500, skip);
                    if (rows.Count == 0) break;
                    total += MdStore.UpsertItems(db, rows);
                    skip += rows.Count;
                    if (rows.Count < 500) break;
                }
            }
            else // tax_codes
            {
                var rows = await b1.FetchTaxCodesAsync();
                total = MdStore.UpsertTaxCodes(db, rows);
            }
            _logger?.LogInformation("Đồng bộ {Entity} xong: {Total} bản ghi", entity, total);
        }
        catch (B1Exception ex)
        {
            error = $"Lỗi B1: {ex.Message}";
            _logger?.LogWarning("Đồng bộ {Entity} lỗi: {Msg}", entity, ex.Message);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _logger?.LogError(ex, "Đồng bộ {Entity} lỗi không mong đợi", entity);
        }
        finally
        {
            MdStore.LogFinish(logId, total, error);
            await Gate.WaitAsync();
            try { lock (Running) Running.Remove(key); }
            finally { Gate.Release(); }
        }
        return (total, error);
    }

    public async Task SyncManyAsync(IEnumerable<string> entities, IB1Client b1, string? companyDb = null)
    {
        foreach (var e in entities)
            if (Entities.Contains(e))
                await SyncEntityAsync(e, b1, companyDb);
    }
}
