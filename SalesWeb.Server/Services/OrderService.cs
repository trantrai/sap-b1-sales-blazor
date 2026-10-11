using System.Diagnostics;
using SalesWeb.Server.Data;
using SalesWeb.Server.Middleware;
using SalesWeb.Server.Services.B1;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Server.Services;

/// <summary>Nghiệp vụ Sales Orders: map web &lt;-&gt; Service Layer, idempotency, normalize.</summary>
public sealed class OrderService
{
    private readonly IB1Client _b1;
    private readonly string _udf;
    public string Username { get; set; } = "";
    /// <summary>Đặt khi chạy retry: id bản ghi audit gốc để liên kết.</summary>
    public long? RetryOf { get; set; }

    public OrderService(IB1Client b1, string externalIdUdf, string username = "")
    {
        _b1 = b1;
        _udf = externalIdUdf;
        Username = username;
    }

    /// <summary>Dịch lỗi Service Layer sang HTTP + tiếng Việt thân thiện.</summary>
    public static ApiException B1ErrorToHttp(B1Exception exc)
    {
        var msg = exc.Message;
        if (msg.Contains("412"))
            return new ApiException(409, "Đơn hàng đã bị người khác sửa. Vui lòng tải lại rồi thử tiếp.");
        if (msg.Contains("404"))
            return new ApiException(404, "Không tìm thấy đơn hàng.");
        if (msg.Contains("401") || msg.Contains("Login"))
            return new ApiException(502, "Không kết nối được SAP B1 (sai thông tin đăng nhập?).");
        var detail = msg.Replace("B1 lỗi 400: ", "").Replace("B1 lỗi ", "");
        return new ApiException(400, $"Lỗi từ SAP B1: {detail}");
    }

    public static Dictionary<string, object?> ToB1CreatePayload(OrderCreate order, string udf)
    {
        var lines = new List<object?>();
        foreach (var ln in order.Lines)
        {
            var b1Line = new Dictionary<string, object?>
            {
                ["ItemCode"] = ln.ItemCode,
                ["Quantity"] = ln.Quantity,
                ["Price"] = ln.Price, // tên field chuẩn Service Layer cho đơn giá
                ["DiscountPercent"] = ln.DiscountPercent,
            };
            if (!string.IsNullOrEmpty(ln.Warehouse)) b1Line["WarehouseCode"] = ln.Warehouse;
            if (!string.IsNullOrEmpty(ln.TaxCode)) b1Line["TaxCode"] = ln.TaxCode;
            if (ln.ShipDate.HasValue) b1Line["ShipDate"] = ln.ShipDate.Value.ToString("yyyy-MM-dd");
            if (!string.IsNullOrEmpty(ln.ProjectCode)) b1Line["ProjectCode"] = ln.ProjectCode;
            if (!string.IsNullOrEmpty(ln.CostingCode)) b1Line["CostingCode"] = ln.CostingCode;
            if (!string.IsNullOrEmpty(ln.UomCode)) b1Line["UoMCode"] = ln.UomCode;
            lines.Add(b1Line);
        }
        var docDate = (order.DocDate ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd");
        var payload = new Dictionary<string, object?>
        {
            ["CardCode"] = order.CardCode,
            ["DocDate"] = docDate,
            ["TaxDate"] = order.TaxDate?.ToString("yyyy-MM-dd") ?? docDate,
            ["DocumentLines"] = lines,
            ["DiscountPercent"] = order.DiscountPercent,
        };
        if (order.DocDueDate.HasValue) payload["DocDueDate"] = order.DocDueDate.Value.ToString("yyyy-MM-dd");
        if (!string.IsNullOrEmpty(order.NumAtCard)) payload["NumAtCard"] = order.NumAtCard;
        if (!string.IsNullOrEmpty(order.DocCurrency)) payload["DocCurrency"] = order.DocCurrency;
        if (order.DocRate.HasValue) payload["DocRate"] = order.DocRate.Value;
        if (order.ContactPersonCode.HasValue) payload["ContactPersonCode"] = order.ContactPersonCode.Value;
        if (!string.IsNullOrEmpty(order.ShipToCode)) payload["ShipToCode"] = order.ShipToCode;
        if (!string.IsNullOrEmpty(order.PayToCode)) payload["PayToCode"] = order.PayToCode;
        if (order.PaymentGroupCode.HasValue) payload["PaymentGroupCode"] = order.PaymentGroupCode.Value;
        if (order.TransportationCode.HasValue) payload["TransportationCode"] = order.TransportationCode.Value;
        if (!string.IsNullOrEmpty(order.Project)) payload["Project"] = order.Project;
        if (order.Series.HasValue) payload["Series"] = order.Series.Value;
        if (!string.IsNullOrEmpty(order.Comments)) payload["Comments"] = order.Comments;
        if (order.SalesPersonCode.HasValue) payload["SalesPersonCode"] = order.SalesPersonCode.Value;
        if (!string.IsNullOrEmpty(order.ExternalId)) payload[udf] = order.ExternalId;
        return payload;
    }

    public static Dictionary<string, object?> ToB1UpdatePayload(OrderUpdate order)
    {
        var payload = new Dictionary<string, object?>();
        if (order.Comments is not null) payload["Comments"] = order.Comments;
        if (order.DocDueDate.HasValue) payload["DocDueDate"] = order.DocDueDate.Value.ToString("yyyy-MM-dd");
        if (order.TaxDate.HasValue) payload["TaxDate"] = order.TaxDate.Value.ToString("yyyy-MM-dd");
        if (order.NumAtCard is not null) payload["NumAtCard"] = order.NumAtCard;
        if (order.ContactPersonCode.HasValue) payload["ContactPersonCode"] = order.ContactPersonCode.Value;
        if (order.ShipToCode is not null) payload["ShipToCode"] = order.ShipToCode;
        if (order.PayToCode is not null) payload["PayToCode"] = order.PayToCode;
        if (order.PaymentGroupCode.HasValue) payload["PaymentGroupCode"] = order.PaymentGroupCode.Value;
        if (order.TransportationCode.HasValue) payload["TransportationCode"] = order.TransportationCode.Value;
        if (order.Project is not null) payload["Project"] = order.Project;
        if (order.DiscountPercent.HasValue) payload["DiscountPercent"] = order.DiscountPercent.Value;
        if (order.Lines is { Count: > 0 })
        {
            var b1Lines = new List<object?>();
            foreach (var ln in order.Lines)
            {
                var b1Ln = new Dictionary<string, object?> { ["LineNum"] = ln.LineNum };
                if (ln.Quantity.HasValue) b1Ln["Quantity"] = ln.Quantity.Value;
                if (ln.Price.HasValue) b1Ln["Price"] = ln.Price.Value;
                if (ln.Warehouse is not null) b1Ln["WarehouseCode"] = ln.Warehouse;
                if (ln.DiscountPercent.HasValue) b1Ln["DiscountPercent"] = ln.DiscountPercent.Value;
                if (ln.ShipDate.HasValue) b1Ln["ShipDate"] = ln.ShipDate.Value.ToString("yyyy-MM-dd");
                b1Lines.Add(b1Ln);
            }
            payload["DocumentLines"] = b1Lines;
        }
        return payload;
    }

    public static OrderOut NormalizeOrder(Dictionary<string, object?> raw, string udf)
    {
        var cancelled = B1Json.Str(raw, "Cancelled") == "tYES";
        var docStatus = B1Json.Str(raw, "DocumentStatus");
        if (docStatus == "") docStatus = "bost_Open";
        var status = cancelled ? "cancelled" : (docStatus == "bost_Close" ? "closed" : "open");
        var lines = B1Json.DictList(raw, "DocumentLines").Select(ln => new OrderLineOut
        {
            LineNum = B1Json.IntN(ln, "LineNum") ?? 0,
            ItemCode = B1Json.Str(ln, "ItemCode"),
            Description = B1Json.Str(ln, "ItemDescription"),
            Quantity = B1Json.Num(ln, "Quantity"),
            Price = B1Json.Num(ln, "Price", B1Json.Num(ln, "UnitPrice")),
            DiscountPercent = B1Json.Num(ln, "DiscountPercent"),
            Warehouse = B1Json.Str(ln, "WarehouseCode"),
            TaxCode = B1Json.Str(ln, "TaxCode"),
            ShipDate = B1Json.Str(ln, "ShipDate"),
            ProjectCode = B1Json.Str(ln, "ProjectCode"),
            CostingCode = B1Json.Str(ln, "CostingCode"),
            UomCode = B1Json.Str(ln, "UoMCode"),
            LineStatus = B1Json.Str(ln, "LineStatus"),
            LineTotal = ln.TryGetValue("LineTotal", out var lt) && lt is not null
                ? B1Json.Num(ln, "LineTotal") : (double?)null,
        }).ToList();
        return new OrderOut
        {
            DocEntry = B1Json.IntN(raw, "DocEntry") ?? 0,
            DocNum = B1Json.IntN(raw, "DocNum") ?? 0,
            DocDate = B1Json.Str(raw, "DocDate"),
            DocDueDate = B1Json.Str(raw, "DocDueDate"),
            TaxDate = B1Json.Str(raw, "TaxDate"),
            CardCode = B1Json.Str(raw, "CardCode"),
            CardName = B1Json.Str(raw, "CardName"),
            NumAtCard = B1Json.Str(raw, "NumAtCard"),
            DocTotal = B1Json.Num(raw, "DocTotal"),
            Currency = B1Json.Str(raw, "DocCurrency"),
            DocRate = B1Json.Num(raw, "DocRate", 1),
            Status = status,
            Comments = B1Json.Str(raw, "Comments"),
            SalesPersonCode = B1Json.IntN(raw, "SalesPersonCode"),
            ContactPersonCode = B1Json.IntN(raw, "ContactPersonCode"),
            ShipToCode = B1Json.Str(raw, "ShipToCode"),
            PayToCode = B1Json.Str(raw, "PayToCode"),
            PaymentGroupCode = B1Json.IntN(raw, "PaymentGroupCode"),
            TransportationCode = B1Json.IntN(raw, "TransportationCode"),
            Project = B1Json.Str(raw, "Project"),
            Series = B1Json.IntN(raw, "Series"),
            DiscountPercent = B1Json.Num(raw, "DiscountPercent"),
            Address = B1Json.Str(raw, "Address"),
            ExternalId = raw.TryGetValue(udf, out var eid) ? eid?.ToString() : null,
            Lines = lines,
        };
    }

    public static string? BuildODataFilter(string status, string? cardCode, int? salesPersonCode,
        DateOnly? dateFrom, DateOnly? dateTo)
    {
        var parts = new List<string>();
        if (status == "open") parts.Add("DocumentStatus eq 'bost_Open' and Cancelled eq 'tNO'");
        else if (status == "closed") parts.Add("DocumentStatus eq 'bost_Close' and Cancelled eq 'tNO'");
        else if (status == "cancelled") parts.Add("Cancelled eq 'tYES'");
        if (!string.IsNullOrEmpty(cardCode))
            parts.Add($"CardCode eq '{cardCode.Replace("'", "''")}'");
        if (salesPersonCode.HasValue)
            parts.Add($"SalesPersonCode eq {salesPersonCode.Value}");
        if (dateFrom.HasValue)
            parts.Add($"DocDate ge datetime'{dateFrom.Value:yyyy-MM-dd}T00:00:00'");
        if (dateTo.HasValue)
            parts.Add($"DocDate le datetime'{dateTo.Value:yyyy-MM-dd}T23:59:59'");
        return parts.Count > 0 ? string.Join(" and ", parts) : null;
    }

    private async Task<T> AuditedAsync<T>(string operation, string entityKey,
        Dictionary<string, object?> requestData, Func<Task<(T Retval, object? RespObj, string Note)>> doFn)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var (retval, respObj, note) = await doFn();
            sw.Stop();
            var key = entityKey;
            if (string.IsNullOrEmpty(key) && respObj is OrderOut oo) key = oo.DocEntry.ToString();
            SlAuditStore.Log(operation, key, Username, requestData, true, respObj, "",
                null, (int)sw.ElapsedMilliseconds, RetryOf, note);
            return retval;
        }
        catch (B1Exception ex)
        {
            sw.Stop();
            SlAuditStore.Log(operation, entityKey, Username, requestData, false, null,
                ex.Message, null, (int)sw.ElapsedMilliseconds, RetryOf, "");
            throw B1ErrorToHttp(ex);
        }
    }

    private static Dictionary<string, object?> ToRequestDict<T>(T dto)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(dto, ServerJson.Options);
        var d = new Dictionary<string, object?>();
        foreach (var kv in System.Text.Json.JsonDocument.Parse(json).RootElement.EnumerateObject())
            d[kv.Name] = B1Json.ToObject(kv.Value);
        return d;
    }

    /// <summary>Tạo đơn — idempotent theo external_id. Trả về (đơn, created_mới?).</summary>
    public Task<(OrderOut Order, bool Created)> CreateAsync(OrderCreate order)
    {
        var req = ToRequestDict(order);
        return AuditedAsync<(OrderOut, bool)>("order.create", order.ExternalId ?? "", req,
            async () =>
            {
                if (!string.IsNullOrEmpty(order.ExternalId))
                {
                    var existed = await _b1.FindOrderByExternalIdAsync(order.ExternalId, _udf);
                    if (existed is not null)
                    {
                        var (full, _) = await _b1.GetOrderAsync(
                            Convert.ToInt32(existed["DocEntry"]));
                        var out_ = NormalizeOrder(full, _udf);
                        return ((out_, false), (object)out_, "idempotent: đơn đã tồn tại trong B1");
                    }
                }
                var raw = await _b1.CreateOrderAsync(ToB1CreatePayload(order, _udf));
                var created = NormalizeOrder(raw, _udf);
                return ((created, true), (object)created, "");
            });
    }

    public async Task<(OrderOut Order, string? ETag)> GetAsync(int docEntry)
    {
        try
        {
            var (raw, etag) = await _b1.GetOrderAsync(docEntry);
            return (NormalizeOrder(raw, _udf), etag);
        }
        catch (B1Exception ex) { throw B1ErrorToHttp(ex); }
    }

    public async Task<PagedOrders> ListAsync(string status, string? cardCode, int? salesPersonCode,
        DateOnly? dateFrom, DateOnly? dateTo, int top, int skip)
    {
        try
        {
            var data = await _b1.ListOrdersAsync(top, skip,
                BuildODataFilter(status, cardCode, salesPersonCode, dateFrom, dateTo));
            var total = data.TryGetValue("odata.count", out var c) && c is not null
                ? Convert.ToInt32(c.ToString())
                : B1Json.DictList(data, "value").Count;
            return new PagedOrders
            {
                Total = total,
                Items = B1Json.DictList(data, "value")
                    .Select(o => NormalizeOrder(o, _udf)).ToList(),
            };
        }
        catch (B1Exception ex) { throw B1ErrorToHttp(ex); }
    }

    /// <summary>Thống kê dashboard (A1): lấy tối đa 1000 đơn gần nhất, tổng hợp trong bộ nhớ.</summary>
    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var paged = await ListAsync("", null, null, null, null, 1000, 0);
        var orders = paged.Items;
        var stats = new DashboardStats
        {
            TotalOrders = paged.Total,
            TotalRevenue = orders.Where(o => o.Status != "cancelled").Sum(o => o.DocTotal),
            OpenOrders = orders.Count(o => o.Status == "open"),
            ClosedOrders = orders.Count(o => o.Status == "closed"),
            CancelledOrders = orders.Count(o => o.Status == "cancelled"),
        };
        // Doanh thu 30 ngày gần nhất
        var today = DateOnly.FromDateTime(DateTime.Today);
        var byDay = orders
            .Where(o => o.Status != "cancelled" && DateOnly.TryParse(o.DocDate, out _))
            .GroupBy(o => DateOnly.Parse(o.DocDate))
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(o => o.DocTotal), Orders: g.Count()));
        for (int i = 29; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            byDay.TryGetValue(d, out var v);
            stats.RevenueByDay.Add(new DailyRevenue
            {
                Date = d.ToString("dd/MM"),
                Revenue = v.Revenue,
                Orders = v.Orders,
            });
        }
        // Top 5 khách hàng
        stats.TopCustomers = orders
            .Where(o => o.Status != "cancelled")
            .GroupBy(o => new { o.CardCode, o.CardName })
            .Select(g => new TopCustomer
            {
                CardCode = g.Key.CardCode,
                CardName = g.Key.CardName,
                Revenue = g.Sum(o => o.DocTotal),
                Orders = g.Count(),
            })
            .OrderByDescending(t => t.Revenue).Take(5).ToList();
        // Top 5 hàng hoá (cần chi tiết dòng — lấy từ orders đã có lines)
        stats.TopItems = orders
            .Where(o => o.Status != "cancelled")
            .SelectMany(o => o.Lines)
            .GroupBy(l => new { l.ItemCode, Name = l.Description })
            .Select(g => new TopItem
            {
                ItemCode = g.Key.ItemCode,
                ItemName = g.Key.Name,
                Quantity = g.Sum(l => l.Quantity),
                Revenue = g.Sum(l => (l.LineTotal ?? l.Quantity * l.Price)),
            })
            .OrderByDescending(t => t.Revenue).Take(5).ToList();
        return stats;
    }

    public Task<OrderOut> UpdateAsync(int docEntry, OrderUpdate order)
    {
        var req = ToRequestDict(order);
        return AuditedAsync<OrderOut>("order.update", docEntry.ToString(), req,
            async () =>
            {
                await _b1.UpdateOrderAsync(docEntry, ToB1UpdatePayload(order), order.Etag);
                var (raw, _) = await _b1.GetOrderAsync(docEntry);
                var out_ = NormalizeOrder(raw, _udf);
                return (out_, (object)out_, "");
            });
    }

    public Task<OrderOut> CancelAsync(int docEntry)
    {
        return AuditedAsync<OrderOut>("order.cancel", docEntry.ToString(),
            new Dictionary<string, object?>(),
            async () =>
            {
                var raw = await _b1.CancelOrderAsync(docEntry);
                var out_ = NormalizeOrder(raw, _udf);
                return (out_, (object)out_, "");
            });
    }

    public Task<OrderOut> CloseLinesAsync(int docEntry, List<int>? lineNums)
    {
        var req = new Dictionary<string, object?> { ["line_nums"] = lineNums };
        return AuditedAsync<OrderOut>("order.close", docEntry.ToString(), req,
            async () =>
            {
                await _b1.CloseOrderLinesAsync(docEntry, lineNums);
                var (raw, _) = await _b1.GetOrderAsync(docEntry);
                var out_ = NormalizeOrder(raw, _udf);
                return (out_, (object)out_, "");
            });
    }
}
