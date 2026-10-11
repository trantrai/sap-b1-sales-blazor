namespace SalesWeb.Server.Services.B1;

/// <summary>Giả lập Service Layer trong RAM — dev/test không cần B1 thật.</summary>
public sealed class MockB1Client : IB1Client
{
    private int _seq = 1000;
    private readonly Dictionary<int, Dictionary<string, object?>> _orders = new();
    private readonly string _udf;

    private readonly List<Dictionary<string, object?>> _customers = new()
    {
        new() { ["CardCode"] = "C20000", ["CardName"] = "Công ty TNHH An Phát", ["Phone1"] = "0901111111", ["SalesPersonCode"] = 1, ["PriceListNum"] = 1 },
        new() { ["CardCode"] = "C20001", ["CardName"] = "Cửa hàng Bình Minh", ["Phone1"] = "0902222222", ["SalesPersonCode"] = 2, ["PriceListNum"] = 1 },
        new() { ["CardCode"] = "C20002", ["CardName"] = "Siêu thị Coop Home", ["Phone1"] = "0903333333", ["SalesPersonCode"] = 1, ["PriceListNum"] = 2 },
    };
    private readonly List<Dictionary<string, object?>> _items = new()
    {
        new() { ["ItemCode"] = "A00001", ["ItemName"] = "Bàn làm việc gỗ", ["ItemType"] = "itItems", ["SalesUnit"] = "Cái", ["Price"] = 1500000, ["TaxCode"] = "S1" },
        new() { ["ItemCode"] = "A00002", ["ItemName"] = "Ghế xoay văn phòng", ["ItemType"] = "itItems", ["SalesUnit"] = "Cái", ["Price"] = 850000, ["TaxCode"] = "S1" },
        new() { ["ItemCode"] = "A00003", ["ItemName"] = "Tủ hồ sơ 4 ngăn", ["ItemType"] = "itItems", ["SalesUnit"] = "Cái", ["Price"] = 2300000, ["TaxCode"] = "S1" },
    };
    private readonly List<Dictionary<string, object?>> _warehouses = new()
    {
        new() { ["WarehouseCode"] = "01", ["WarehouseName"] = "Kho chính" },
        new() { ["WarehouseCode"] = "02", ["WarehouseName"] = "Kho chi nhánh" },
    };
    private readonly List<Dictionary<string, object?>> _paymentTerms = new()
    {
        new() { ["GroupNumber"] = 1, ["PaymentTermsGroupName"] = "Thanh toán ngay" },
        new() { ["GroupNumber"] = 2, ["PaymentTermsGroupName"] = "Công nợ 30 ngày" },
        new() { ["GroupNumber"] = 3, ["PaymentTermsGroupName"] = "Công nợ 60 ngày" },
    };
    private readonly List<Dictionary<string, object?>> _shippingTypes = new()
    {
        new() { ["Code"] = 1, ["Name"] = "Giao hàng tiêu chuẩn" },
        new() { ["Code"] = 2, ["Name"] = "Giao hàng nhanh" },
        new() { ["Code"] = 3, ["Name"] = "Khách tự vận chuyển" },
    };
    private readonly List<Dictionary<string, object?>> _salesPersons = new()
    {
        new() { ["SalesEmployeeCode"] = 1, ["SalesEmployeeName"] = "Nguyễn Văn A" },
        new() { ["SalesEmployeeCode"] = 2, ["SalesEmployeeName"] = "Trần Thị B" },
    };
    private readonly List<Dictionary<string, object?>> _taxCodes = new()
    {
        new() { ["Code"] = "S0", ["Name"] = "Không chịu thuế", ["Rate"] = 0 },
        new() { ["Code"] = "S1", ["Name"] = "VAT 8%", ["Rate"] = 8 },
        new() { ["Code"] = "S2", ["Name"] = "VAT 10%", ["Rate"] = 10 },
        new() { ["Code"] = "SV", ["Name"] = "VAT dịch vụ 10%", ["Rate"] = 10 },
    };
    private readonly Dictionary<string, List<Dictionary<string, object?>>> _bpAddresses = new()
    {
        ["C20000"] = new()
        {
            new() { ["AddressName"] = "Trụ sở", ["AddressType"] = "bo_BillTo", ["Street"] = "123 Nguyễn Huệ", ["City"] = "TP.HCM", ["Country"] = "VN" },
            new() { ["AddressName"] = "Kho nhận hàng", ["AddressType"] = "bo_ShipTo", ["Street"] = "45 Võ Văn Kiệt", ["City"] = "TP.HCM", ["Country"] = "VN" },
        },
        ["C20001"] = new()
        {
            new() { ["AddressName"] = "Cửa hàng", ["AddressType"] = "bo_BillTo", ["Street"] = "78 Lê Lợi", ["City"] = "Hà Nội", ["Country"] = "VN" },
        },
    };
    private readonly Dictionary<string, List<Dictionary<string, object?>>> _bpContacts = new()
    {
        ["C20000"] = new()
        {
            new() { ["Name"] = "Anh Tuấn", ["FirstName"] = "Tuấn", ["Phone1"] = "0901111112", ["MobilePhone"] = "0901111112", ["E_Mail"] = "tuan@anphat.vn" },
        },
    };

    public MockB1Client(string externalIdUdf = "U_ExternalID")
    {
        _udf = externalIdUdf;
        SeedOrder();
    }

    public Task LoginAsync() => Task.CompletedTask;
    public Task CloseAsync() => Task.CompletedTask;

    private void SeedOrder()
    {
        _seq++;
        var docEntry = _seq;
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        _orders[docEntry] = new Dictionary<string, object?>
        {
            ["DocEntry"] = docEntry, ["DocNum"] = 5001,
            ["DocDate"] = today, ["DocDueDate"] = today, ["TaxDate"] = today,
            ["CardCode"] = "C20000", ["CardName"] = "Công ty TNHH An Phát",
            ["NumAtCard"] = "PO-2026-001",
            ["DocTotal"] = 3000000, ["DocCurrency"] = "VND", ["DocRate"] = 1.0,
            ["DocumentStatus"] = "bost_Open", ["Cancelled"] = "tNO",
            ["Comments"] = "Đơn mẫu (mock)", ["SalesPersonCode"] = 1,
            ["ContactPersonCode"] = null, ["ShipToCode"] = "Kho nhận hàng", ["PayToCode"] = "Trụ sở",
            ["PaymentGroupCode"] = 2, ["TransportationCode"] = 1,
            ["Project"] = null, ["Series"] = 1, ["DiscountPercent"] = 0,
            ["Address"] = "123 Nguyễn Huệ, TP.HCM",
            ["UpdateDate"] = today, ["UpdateTime"] = "0900",
            [_udf] = "MOCK-001",
            ["_version"] = 1,
            ["DocumentLines"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["LineNum"] = 0, ["ItemCode"] = "A00001", ["ItemDescription"] = "Bàn làm việc gỗ",
                    ["Quantity"] = 2, ["Price"] = 1500000, ["WarehouseCode"] = "01",
                    ["LineStatus"] = "bost_Open", ["TaxCode"] = "V10", ["DiscountPercent"] = 0,
                    ["ShipDate"] = today, ["ProjectCode"] = null,
                    ["CostingCode"] = null, ["UoMCode"] = "Cái",
                },
            },
        };
    }

    private string ETag(Dictionary<string, object?> order)
        => $"W/\"{order["DocEntry"]}-{order["_version"]}\"";

    private Dictionary<string, object?> CheckCustomer(string cardCode)
    {
        var c = _customers.FirstOrDefault(x => B1Json.Str(x, "CardCode") == cardCode);
        if (c is null)
            throw new B1Exception($"B1 lỗi 400: 1320000042 - Business partner '{cardCode}' không tồn tại.");
        return c;
    }

    // ---------------- Orders ----------------
    public Task<Dictionary<string, object?>> CreateOrderAsync(Dictionary<string, object?> payload)
    {
        var customer = CheckCustomer(B1Json.Str(payload, "CardCode"));
        if (!B1Json.DictList(payload, "DocumentLines").Any())
            throw new B1Exception("B1 lỗi 400: Đơn hàng phải có ít nhất 1 dòng hàng.");
        _seq++;
        var docEntry = _seq;
        var lines = new List<object?>();
        double total = 0;
        int i = 0;
        foreach (var ln in B1Json.DictList(payload, "DocumentLines"))
        {
            var qty = B1Json.Num(ln, "Quantity");
            var price = ln.TryGetValue("Price", out var p) && p is not null ? B1Json.Num(ln, "Price")
                : B1Json.Num(ln, "UnitPrice");
            var disc = B1Json.Num(ln, "DiscountPercent");
            var lineTotal = qty * price * (1 - disc / 100);
            total += lineTotal;
            var item = _items.FirstOrDefault(x => B1Json.Str(x, "ItemCode") == B1Json.Str(ln, "ItemCode"));
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            lines.Add(new Dictionary<string, object?>
            {
                ["LineNum"] = i++, ["ItemCode"] = B1Json.Str(ln, "ItemCode"),
                ["ItemDescription"] = item is not null ? B1Json.Str(item, "ItemName") : B1Json.Str(ln, "ItemCode"),
                ["Quantity"] = qty, ["Price"] = price, ["DiscountPercent"] = disc,
                ["WarehouseCode"] = B1Json.Str(ln, "WarehouseCode") != "" ? B1Json.Str(ln, "WarehouseCode") : "01",
                ["TaxCode"] = B1Json.Str(ln, "TaxCode") != "" ? B1Json.Str(ln, "TaxCode") : "V10",
                ["ShipDate"] = B1Json.Str(ln, "ShipDate") != "" ? B1Json.Str(ln, "ShipDate") : today,
                ["ProjectCode"] = ln.TryGetValue("ProjectCode", out var pc) ? pc : null,
                ["CostingCode"] = ln.TryGetValue("CostingCode", out var cc) ? cc : null,
                ["UoMCode"] = ln.TryGetValue("UoMCode", out var uc) ? uc : null,
                ["LineStatus"] = "bost_Open", ["LineTotal"] = lineTotal,
            });
        }
        var headerDisc = B1Json.Num(payload, "DiscountPercent");
        total *= 1 - headerDisc / 100;
        var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
        var order = new Dictionary<string, object?>
        {
            ["DocEntry"] = docEntry, ["DocNum"] = 5000 + (docEntry - 1000),
            ["DocDate"] = B1Json.Str(payload, "DocDate") != "" ? B1Json.Str(payload, "DocDate") : todayStr,
            ["DocDueDate"] = B1Json.Str(payload, "DocDueDate") != "" ? B1Json.Str(payload, "DocDueDate") : todayStr,
            ["TaxDate"] = B1Json.Str(payload, "TaxDate") != "" ? B1Json.Str(payload, "TaxDate")
                : (B1Json.Str(payload, "DocDate") != "" ? B1Json.Str(payload, "DocDate") : todayStr),
            ["CardCode"] = B1Json.Str(payload, "CardCode"), ["CardName"] = B1Json.Str(customer, "CardName"),
            ["NumAtCard"] = payload.TryGetValue("NumAtCard", out var nac) ? nac : null,
            ["DocTotal"] = Math.Round(total, 2),
            ["DocCurrency"] = B1Json.Str(payload, "DocCurrency") != "" ? B1Json.Str(payload, "DocCurrency") : "VND",
            ["DocRate"] = payload.TryGetValue("DocRate", out var dr) && dr is not null ? B1Json.Num(payload, "DocRate") : 1.0,
            ["DocumentStatus"] = "bost_Open", ["Cancelled"] = "tNO",
            ["Comments"] = B1Json.Str(payload, "Comments"),
            ["DiscountPercent"] = headerDisc,
            ["SalesPersonCode"] = payload.TryGetValue("SalesPersonCode", out var spc) && spc is not null ? spc : B1Json.IntN(customer, "SalesPersonCode"),
            ["ContactPersonCode"] = payload.TryGetValue("ContactPersonCode", out var cpc) ? cpc : null,
            ["ShipToCode"] = payload.TryGetValue("ShipToCode", out var stc) ? stc : null,
            ["PayToCode"] = payload.TryGetValue("PayToCode", out var ptc) ? ptc : null,
            ["PaymentGroupCode"] = payload.TryGetValue("PaymentGroupCode", out var pgc) ? pgc : null,
            ["TransportationCode"] = payload.TryGetValue("TransportationCode", out var tc) ? tc : null,
            ["Project"] = payload.TryGetValue("Project", out var prj) ? prj : null,
            ["Series"] = payload.TryGetValue("Series", out var ser) ? ser : null,
            ["UpdateDate"] = todayStr, ["UpdateTime"] = DateTime.Now.ToString("HHmm"),
            [_udf] = payload.TryGetValue(_udf, out var eid) ? eid : null,
            ["_version"] = 1, ["DocumentLines"] = lines,
        };
        _orders[docEntry] = order;
        return Task.FromResult(B1Json.DeepClone(order));
    }

    public Task<(Dictionary<string, object?> Order, string? ETag)> GetOrderAsync(int docEntry)
    {
        if (!_orders.TryGetValue(docEntry, out var order))
            throw new B1Exception($"B1 lỗi 404: Không tìm thấy đơn hàng DocEntry={docEntry}.");
        return Task.FromResult((B1Json.DeepClone(order), (string?)ETag(order)));
    }

    public Task<Dictionary<string, object?>> ListOrdersAsync(int top = 50, int skip = 0,
        string? filter = null, string orderby = "DocEntry desc")
    {
        var orders = _orders.Values.AsEnumerable();
        if (!string.IsNullOrEmpty(filter))
            orders = orders.Where(o => MatchFilter(o, filter));
        var total = orders.Count();
        var page = orders.OrderByDescending(o => Convert.ToInt32(o["DocEntry"]))
            .Skip(skip).Take(top)
            .Select(o => { var c = B1Json.DeepClone(o); c.Remove("DocumentLines"); return c; })
            .Cast<object?>().ToList();
        return Task.FromResult(new Dictionary<string, object?>
        {
            ["odata.count"] = total.ToString(), ["value"] = page,
        });
    }

    private static bool MatchFilter(Dictionary<string, object?> order, string filt)
    {
        bool ok = true;
        if (filt.Contains("DocumentStatus eq 'bost_Open'"))
            ok &= B1Json.Str(order, "DocumentStatus") == "bost_Open" && B1Json.Str(order, "Cancelled") == "tNO";
        if (filt.Contains("DocumentStatus eq 'bost_Close'"))
            ok &= B1Json.Str(order, "DocumentStatus") == "bost_Close";
        if (filt.Contains("Cancelled eq 'tYES'"))
            ok &= B1Json.Str(order, "Cancelled") == "tYES";
        if (filt.Contains("CardCode eq '"))
        {
            var code = filt.Split("CardCode eq '")[1].Split("'")[0];
            ok &= B1Json.Str(order, "CardCode") == code;
        }
        if (filt.Contains("SalesPersonCode eq "))
        {
            var code = filt.Split("SalesPersonCode eq ")[1].Split(" ")[0].Trim(')');
            ok &= (B1Json.IntN(order, "SalesPersonCode")?.ToString() ?? "") == code;
        }
        return ok;
    }

    public Task UpdateOrderAsync(int docEntry, Dictionary<string, object?> payload, string? etag = null)
    {
        if (!_orders.TryGetValue(docEntry, out var order))
            throw new B1Exception($"B1 lỗi 404: Không tìm thấy đơn hàng DocEntry={docEntry}.");
        if (etag is not null && etag != ETag(order))
            throw new B1Exception("B1 lỗi 412: Đơn hàng đã bị người khác sửa (xung đột ETag). Hãy tải lại.");
        if (B1Json.Str(order, "Cancelled") == "tYES")
            throw new B1Exception("B1 lỗi 400: Không thể sửa đơn hàng đã hủy.");
        foreach (var key in new[] { "Comments", "DocDueDate", "TaxDate", "DiscountPercent", "NumAtCard",
            "ContactPersonCode", "ShipToCode", "PayToCode", "PaymentGroupCode",
            "TransportationCode", "Project", "DocCurrency", "DocRate" })
            if (payload.ContainsKey(key)) order[key] = payload[key];
        if (payload.TryGetValue("DocumentLines", out var plObj))
        {
            var byNum = B1Json.DictList(order, "DocumentLines")
                .Where(l => B1Json.IntN(l, "LineNum").HasValue)
                .ToDictionary(l => B1Json.IntN(l, "LineNum")!.Value);
            foreach (var pl in B1Json.DictList(payload, "DocumentLines"))
            {
                var lnNum = B1Json.IntN(pl, "LineNum");
                if (lnNum is null || !byNum.TryGetValue(lnNum.Value, out var ln)) continue;
                foreach (var k in new[] { "Quantity", "WarehouseCode", "TaxCode", "DiscountPercent",
                    "LineStatus", "ShipDate", "ProjectCode", "CostingCode", "UoMCode" })
                    if (pl.ContainsKey(k)) ln[k] = pl[k];
                if (pl.TryGetValue("Price", out var pr) && pr is not null) ln["Price"] = pr;
                else if (pl.TryGetValue("UnitPrice", out var up) && up is not null) ln["Price"] = up;
            }
            order["DocTotal"] = Math.Round(B1Json.DictList(order, "DocumentLines")
                .Where(l => B1Json.Str(l, "LineStatus") == "bost_Open")
                .Sum(l => B1Json.Num(l, "Quantity") * B1Json.Num(l, "Price")
                    * (1 - B1Json.Num(l, "DiscountPercent") / 100)), 2);
        }
        order["_version"] = Convert.ToInt32(order["_version"]) + 1;
        order["UpdateDate"] = DateTime.Today.ToString("yyyy-MM-dd");
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, object?>> CancelOrderAsync(int docEntry)
    {
        if (!_orders.TryGetValue(docEntry, out var order))
            throw new B1Exception($"B1 lỗi 404: Không tìm thấy đơn hàng DocEntry={docEntry}.");
        if (B1Json.Str(order, "Cancelled") == "tYES")
            throw new B1Exception("B1 lỗi 400: Đơn hàng đã được hủy trước đó.");
        order["Cancelled"] = "tYES";
        order["DocumentStatus"] = "bost_Close";
        order["_version"] = Convert.ToInt32(order["_version"]) + 1;
        return Task.FromResult(B1Json.DeepClone(order));
    }

    public Task CloseOrderLinesAsync(int docEntry, List<int>? lineNums)
    {
        if (!_orders.TryGetValue(docEntry, out var order))
            throw new B1Exception($"B1 lỗi 404: Không tìm thấy đơn hàng DocEntry={docEntry}.");
        foreach (var ln in B1Json.DictList(order, "DocumentLines"))
        {
            var lnNum = B1Json.IntN(ln, "LineNum");
            if ((lineNums is null || (lnNum.HasValue && lineNums.Contains(lnNum.Value)))
                && B1Json.Str(ln, "LineStatus") == "bost_Open")
                ln["LineStatus"] = "bost_Close";
        }
        if (B1Json.DictList(order, "DocumentLines").All(l => B1Json.Str(l, "LineStatus") == "bost_Close"))
            order["DocumentStatus"] = "bost_Close";
        order["_version"] = Convert.ToInt32(order["_version"]) + 1;
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, object?>?> FindOrderByExternalIdAsync(string externalId, string? udf = null)
    {
        var key = udf ?? _udf;
        foreach (var o in _orders.Values)
        {
            if (o.TryGetValue(key, out var v) && v?.ToString() == externalId)
            {
                var c = B1Json.DeepClone(o);
                c.Remove("DocumentLines");
                return Task.FromResult((Dictionary<string, object?>?)c);
            }
        }
        return Task.FromResult((Dictionary<string, object?>?)null);
    }

    // ---------------- Master data ----------------
    public Task<List<Dictionary<string, object?>>> SearchBusinessPartnersAsync(string q, int top = 20)
    {
        var ql = q.ToLowerInvariant();
        return Task.FromResult(_customers
            .Where(c => B1Json.Str(c, "CardCode").ToLowerInvariant().Contains(ql)
                     || B1Json.Str(c, "CardName").ToLowerInvariant().Contains(ql))
            .Take(top).ToList());
    }

    public Task<List<Dictionary<string, object?>>> SearchItemsAsync(string q, int top = 20)
    {
        var ql = q.ToLowerInvariant();
        return Task.FromResult(_items
            .Where(i => B1Json.Str(i, "ItemCode").ToLowerInvariant().Contains(ql)
                     || B1Json.Str(i, "ItemName").ToLowerInvariant().Contains(ql))
            .Take(top).ToList());
    }

    public Task<List<Dictionary<string, object?>>> ListWarehousesAsync() => Task.FromResult(_warehouses.ToList());
    public Task<List<Dictionary<string, object?>>> ListPaymentTermsAsync() => Task.FromResult(_paymentTerms.ToList());
    public Task<List<Dictionary<string, object?>>> ListShippingTypesAsync() => Task.FromResult(_shippingTypes.ToList());
    public Task<List<Dictionary<string, object?>>> ListSalesPersonsAsync() => Task.FromResult(_salesPersons.ToList());

    public Task<List<Dictionary<string, object?>>> GetBpAddressesAsync(string cardCode)
        => Task.FromResult(_bpAddresses.TryGetValue(cardCode, out var v) ? v.ToList() : new List<Dictionary<string, object?>>());

    public Task<List<Dictionary<string, object?>>> GetBpContactsAsync(string cardCode)
        => Task.FromResult(_bpContacts.TryGetValue(cardCode, out var v) ? v.ToList() : new List<Dictionary<string, object?>>());

    public Task<List<Dictionary<string, object?>>> FetchCustomersPageAsync(int top = 500, int skip = 0)
        => Task.FromResult(_customers.Skip(skip).Take(top).ToList());

    public Task<List<Dictionary<string, object?>>> FetchItemsPageAsync(int top = 500, int skip = 0)
        => Task.FromResult(_items.Skip(skip).Take(top).ToList());

    public Task<List<Dictionary<string, object?>>> FetchTaxCodesAsync()
        => Task.FromResult(_taxCodes.ToList());
}
