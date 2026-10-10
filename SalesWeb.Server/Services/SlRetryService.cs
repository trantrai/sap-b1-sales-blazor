using System.Text.Json;
using SalesWeb.Server.Data;
using SalesWeb.Server.Middleware;
using SalesWeb.Server.Services.B1;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Server.Services;

/// <summary>Thử lại các thao tác B1 thất bại từ bản ghi audit — kèm kiểm tra idempotency.</summary>
public sealed class SlRetryService
{
    private static readonly HashSet<string> Retryable = new()
        { "order.create", "order.update", "order.cancel", "order.close" };

    private readonly AppConfig _cfg;
    public SlRetryService(AppConfig cfg) => _cfg = cfg;

    private static Dictionary<string, object?> ParseReq(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            var d = new Dictionary<string, object?>();
            foreach (var kv in doc.RootElement.EnumerateObject())
                d[kv.Name] = B1Json.ToObject(kv.Value);
            return d;
        }
        catch { throw new ApiException(400, "Dữ liệu request đã lưu bị hỏng, không thể thử lại."); }
    }

    private static T Bind<T>(Dictionary<string, object?> req) where T : new()
    {
        // request_json đã lưu ở dạng snake_case -> bind trực tiếp
        var json = JsonSerializer.Serialize(req, ServerJson.Options);
        return JsonSerializer.Deserialize<T>(json, ServerJson.Options) ?? new T();
    }

    public async Task<RetryOut> RetryAuditAsync(long auditId, IB1Client b1, string username)
    {
        var rec = SlAuditStore.Get(auditId)
            ?? throw new ApiException(404, "Không tìm thấy bản ghi audit.");
        var success = Convert.ToInt32(rec["success"]) == 1;
        if (success) throw new ApiException(400, "Thao tác đã thành công, không cần thử lại.");
        var op = rec["operation"]?.ToString() ?? "";
        if (!Retryable.Contains(op))
            throw new ApiException(400, $"Thao tác {op} không hỗ trợ thử lại tự động.");

        var req = ParseReq(rec["request_json"]?.ToString() ?? "{}");
        var svc = new OrderService(b1, _cfg.ExternalIdUdf, username) { RetryOf = auditId };
        string key;
        Dictionary<string, object?> detail;
        try
        {
            if (op == "order.create")
            {
                var (out_, created) = await svc.CreateAsync(Bind<OrderCreate>(req));
                key = out_.DocEntry.ToString();
                detail = new Dictionary<string, object?>
                {
                    ["doc_entry"] = out_.DocEntry, ["doc_num"] = out_.DocNum,
                    ["idempotent_hit"] = !created,
                };
            }
            else if (op == "order.update")
            {
                var docEntry = Convert.ToInt32(rec["entity_key"]);
                var (_, etag) = await svc.GetAsync(docEntry); // ETag mới nhất, tránh 412
                var upd = Bind<OrderUpdate>(req);
                upd.Etag = etag;
                var out_ = await svc.UpdateAsync(docEntry, upd);
                key = docEntry.ToString();
                detail = new Dictionary<string, object?> { ["doc_entry"] = docEntry };
            }
            else if (op == "order.cancel")
            {
                var docEntry = Convert.ToInt32(rec["entity_key"]);
                var (current, _) = await svc.GetAsync(docEntry);
                if (current.Status == "cancelled")
                {
                    // Lần đầu thực ra đã huỷ thành công (mất response) -> ghi nhận
                    SlAuditStore.Log("order.cancel", docEntry.ToString(), username, req, true,
                        new Dictionary<string, object?> { ["status"] = "cancelled" }, "",
                        null, 0, auditId, "đơn đã được huỷ trước đó — không gọi lại B1");
                    key = docEntry.ToString();
                    detail = new Dictionary<string, object?> { ["already_cancelled"] = true };
                }
                else
                {
                    var out_ = await svc.CancelAsync(docEntry);
                    key = docEntry.ToString();
                    detail = new Dictionary<string, object?> { ["doc_entry"] = out_.DocEntry };
                }
            }
            else // order.close
            {
                var docEntry = Convert.ToInt32(rec["entity_key"]);
                List<int>? lineNums = null;
                if (req.TryGetValue("line_nums", out var ln) && ln is not null)
                {
                    if (ln is List<object?> lo)
                        lineNums = lo.Select(x => Convert.ToInt32(x)).ToList();
                    else if (ln is System.Text.Json.JsonElement je && je.ValueKind == JsonValueKind.Array)
                        lineNums = je.EnumerateArray().Select(x => x.GetInt32()).ToList();
                }
                var out_ = await svc.CloseLinesAsync(docEntry, lineNums);
                key = docEntry.ToString();
                detail = new Dictionary<string, object?> { ["doc_entry"] = out_.DocEntry };
            }
        }
        finally
        {
            SlAuditStore.BumpRetryCount(auditId);
        }

        return new RetryOut { Ok = true, Operation = op, EntityKey = key, Detail = detail };
    }

    public async Task<RetryAllOut> RetryAllFailedAsync(IB1Client b1, string username, int limit = 200)
    {
        var ids = SlAuditStore.FailedIds(limit);
        var ok = new List<Dictionary<string, object?>>();
        var fail = new List<Dictionary<string, object?>>();
        foreach (var aid in ids)
        {
            try
            {
                var r = await RetryAuditAsync(aid, b1, username);
                ok.Add(new Dictionary<string, object?>
                {
                    ["id"] = aid, ["ok"] = r.Ok, ["operation"] = r.Operation,
                    ["entity_key"] = r.EntityKey, ["detail"] = r.Detail,
                });
            }
            catch (ApiException ex)
            {
                fail.Add(new Dictionary<string, object?> { ["id"] = aid, ["error"] = ex.Message });
            }
            catch (Exception ex)
            {
                fail.Add(new Dictionary<string, object?> { ["id"] = aid, ["error"] = ex.Message });
            }
        }
        return new RetryAllOut { Retried = ids.Count, Ok = ok, Fail = fail };
    }
}
