using System.Net;
using System.Text;
using System.Text.Json;

namespace SalesWeb.Server.Services.B1;

/// <summary>Client gọi SAP Business One Service Layer (REST/OData):
/// login cookie B1SESSION, tự login lại khi 401, ETag/If-Match.</summary>
public sealed class RealB1Client : IB1Client, IAsyncDisposable
{
    private const string OrderSelect =
        "DocEntry,DocNum,DocDate,DocDueDate,TaxDate,CardCode,CardName,NumAtCard," +
        "DocTotal,DocCurrency,DocRate,DocumentStatus,Cancelled,Comments," +
        "SalesPersonCode,ContactPersonCode,ShipToCode,PayToCode,PaymentGroupCode," +
        "TransportationCode,Project,Series,DiscountPercent,Address," +
        "UpdateDate,UpdateTime,DocumentLines";
    private const string OrderListSelect =
        "DocEntry,DocNum,DocDate,DocDueDate,CardCode,CardName,NumAtCard," +
        "DocTotal,DocCurrency,DocumentStatus,Cancelled,SalesPersonCode";

    private readonly HttpClient _http;
    private readonly string _companyDb;
    private readonly string _username;
    private readonly string _password;
    private readonly string _externalIdUdf;
    private bool _loggedIn;
    private bool _disposed;

    public RealB1Client(string baseUrl, string companyDb, string username, string password,
        bool verifySsl = true, double timeoutSeconds = 30, string externalIdUdf = "U_ExternalID")
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseProxy = false, // B1 thường nằm trong mạng nội bộ, bỏ qua proxy môi trường
        };
        if (!verifySsl)
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 1)),
        };
        _companyDb = companyDb;
        _username = username;
        _password = password;
        _externalIdUdf = externalIdUdf;
    }

    public async ValueTask DisposeAsync() => await CloseAsync();

    public Task CloseAsync()
    {
        if (!_disposed) { _disposed = true; _http.Dispose(); }
        return Task.CompletedTask;
    }

    public async Task LoginAsync()
    {
        using var resp = await _http.PostAsJsonAsync("Login", new
        {
            CompanyDB = _companyDb,
            UserName = _username,
            Password = _password,
        });
        if (resp.StatusCode != HttpStatusCode.OK)
        {
            var text = await resp.Content.ReadAsStringAsync();
            throw new B1Exception($"Login B1 thất bại ({(int)resp.StatusCode}): {text[..Math.Min(500, text.Length)]}");
        }
        _loggedIn = true;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path,
        Dictionary<string, string>? query = null, object? json = null,
        Dictionary<string, string>? headers = null)
    {
        using var req = new HttpRequestMessage(method, BuildUri(path, query));
        if (json is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(json),
                Encoding.UTF8, "application/json");
        if (headers is not null)
            foreach (var (k, v) in headers)
                req.Headers.TryAddWithoutValidation(k, v);
        return await _http.SendAsync(req);
    }

    private static string BuildUri(string path, Dictionary<string, string>? query)
    {
        if (query is null || query.Count == 0) return path;
        var qs = string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"{path}?{qs}";
    }

    private async Task<HttpResponseMessage> RequestAsync(HttpMethod method, string path,
        Dictionary<string, string>? query = null, object? json = null,
        Dictionary<string, string>? headers = null)
    {
        if (!_loggedIn) await LoginAsync();
        var resp = await SendAsync(method, path, query, json, headers);
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            // session hết hạn -> login lại, retry 1 lần
            await LoginAsync();
            resp.Dispose();
            resp = await SendAsync(method, path, query, json, headers);
        }
        return resp;
    }

    private static async Task<Dictionary<string, object?>> RaiseForB1Async(HttpResponseMessage resp)
    {
        var text = await resp.Content.ReadAsStringAsync();
        if ((int)resp.StatusCode == 412)
            throw new B1Exception("B1 lỗi 412: Đơn hàng đã bị người khác sửa (xung đột ETag). Hãy tải lại.");
        if ((int)resp.StatusCode >= 400)
        {
            string? err = null;
            try
            {
                using var doc = JsonDocument.Parse(text);
                err = doc.RootElement.GetProperty("error").GetProperty("message")
                    .GetProperty("value").GetString();
            }
            catch { /* giữ text gốc */ }
            throw new B1Exception($"B1 lỗi {(int)resp.StatusCode}: {err ?? text[..Math.Min(500, text.Length)]}");
        }
        if (string.IsNullOrWhiteSpace(text)) return new Dictionary<string, object?>();
        return B1Json.ToDict(JsonDocument.Parse(text).RootElement);
    }

    // ---------------- Sales Orders ----------------
    public async Task<Dictionary<string, object?>> CreateOrderAsync(Dictionary<string, object?> payload)
    {
        using var resp = await RequestAsync(HttpMethod.Post, "Orders", json: payload);
        return await RaiseForB1Async(resp);
    }

    public async Task<(Dictionary<string, object?> Order, string? ETag)> GetOrderAsync(int docEntry)
    {
        using var resp = await RequestAsync(HttpMethod.Get, $"Orders({docEntry})",
            new Dictionary<string, string> { ["$select"] = OrderSelect });
        var data = await RaiseForB1Async(resp);
        return (data, resp.Headers.ETag?.ToString());
    }

    public async Task<Dictionary<string, object?>> ListOrdersAsync(int top = 50, int skip = 0,
        string? filter = null, string orderby = "DocEntry desc")
    {
        var q = new Dictionary<string, string>
        {
            ["$select"] = OrderListSelect,
            ["$top"] = Math.Min(top, 200).ToString(),
            ["$skip"] = skip.ToString(),
            ["$orderby"] = orderby,
            ["$count"] = "true",
        };
        if (!string.IsNullOrEmpty(filter)) q["$filter"] = filter;
        using var resp = await RequestAsync(HttpMethod.Get, "Orders", q);
        return await RaiseForB1Async(resp);
    }

    public async Task UpdateOrderAsync(int docEntry, Dictionary<string, object?> payload, string? etag = null)
    {
        var headers = etag is not null
            ? new Dictionary<string, string> { ["If-Match"] = etag } : null;
        using var resp = await RequestAsync(new HttpMethod("PATCH"), $"Orders({docEntry})",
            json: payload, headers: headers);
        await RaiseForB1Async(resp);
    }

    public async Task<Dictionary<string, object?>> CancelOrderAsync(int docEntry)
    {
        using var resp = await RequestAsync(HttpMethod.Post, $"Orders({docEntry})/Cancel");
        return await RaiseForB1Async(resp);
    }

    public async Task CloseOrderLinesAsync(int docEntry, List<int>? lineNums)
    {
        var (order, _) = await GetOrderAsync(docEntry);
        var lines = new List<Dictionary<string, object?>>();
        foreach (var ln in B1Json.DictList(order, "DocumentLines"))
        {
            var lnNum = B1Json.IntN(ln, "LineNum");
            if (lineNums is not null && (lnNum is null || !lineNums.Contains(lnNum.Value)))
                continue;
            if (B1Json.Str(ln, "LineStatus") == "bost_Open")
                lines.Add(new Dictionary<string, object?>
                {
                    ["LineNum"] = lnNum ?? 0, ["LineStatus"] = "bost_Close",
                });
        }
        if (lines.Count == 0) return;
        await UpdateOrderAsync(docEntry, new Dictionary<string, object?> { ["DocumentLines"] = lines });
    }

    public async Task<Dictionary<string, object?>?> FindOrderByExternalIdAsync(string externalId, string? udf = null)
    {
        var filt = $"{udf ?? _externalIdUdf} eq '{B1Json.EscapeOData(externalId)}'";
        using var resp = await RequestAsync(HttpMethod.Get, "Orders",
            new Dictionary<string, string>
            {
                ["$filter"] = filt, ["$select"] = OrderListSelect, ["$top"] = "1",
            });
        var data = await RaiseForB1Async(resp);
        var values = B1Json.DictList(data, "value");
        return values.Count > 0 ? values[0] : null;
    }

    // ---------------- Master data ----------------
    public async Task<List<Dictionary<string, object?>>> SearchBusinessPartnersAsync(string q, int top = 20)
    {
        var qq = B1Json.EscapeOData(q);
        var filt = $"CardType eq 'cCustomer' and (startswith(CardCode,'{qq}') or contains(CardName,'{qq}'))";
        using var resp = await RequestAsync(HttpMethod.Get, "BusinessPartners",
            new Dictionary<string, string>
            {
                ["$filter"] = filt,
                ["$select"] = "CardCode,CardName,Phone1,SalesPersonCode,PriceListNum",
                ["$top"] = top.ToString(),
            });
        return B1Json.DictList(await RaiseForB1Async(resp), "value");
    }

    public async Task<List<Dictionary<string, object?>>> SearchItemsAsync(string q, int top = 20)
    {
        var qq = B1Json.EscapeOData(q);
        var filt = $"Valid eq 'tYES' and (startswith(ItemCode,'{qq}') or contains(ItemName,'{qq}'))";
        using var resp = await RequestAsync(HttpMethod.Get, "Items",
            new Dictionary<string, string>
            {
                ["$filter"] = filt,
                ["$select"] = "ItemCode,ItemName,ItemType,SalesUnit,Price",
                ["$top"] = top.ToString(),
            });
        return B1Json.DictList(await RaiseForB1Async(resp), "value");
    }

    public async Task<List<Dictionary<string, object?>>> ListWarehousesAsync()
    {
        using var resp = await RequestAsync(HttpMethod.Get, "Warehouses",
            new Dictionary<string, string> { ["$select"] = "WarehouseCode,WarehouseName", ["$top"] = "100" });
        return B1Json.DictList(await RaiseForB1Async(resp), "value");
    }

    public async Task<List<Dictionary<string, object?>>> ListPaymentTermsAsync()
    {
        using var resp = await RequestAsync(HttpMethod.Get, "PaymentTermsTypes",
            new Dictionary<string, string> { ["$select"] = "GroupNumber,PaymentTermsGroupName", ["$top"] = "100" });
        return B1Json.DictList(await RaiseForB1Async(resp), "value");
    }

    public async Task<List<Dictionary<string, object?>>> ListShippingTypesAsync()
    {
        using var resp = await RequestAsync(HttpMethod.Get, "ShippingTypes",
            new Dictionary<string, string> { ["$select"] = "Code,Name", ["$top"] = "100" });
        return B1Json.DictList(await RaiseForB1Async(resp), "value");
    }

    public async Task<List<Dictionary<string, object?>>> ListSalesPersonsAsync()
    {
        using var resp = await RequestAsync(HttpMethod.Get, "SalesPersons",
            new Dictionary<string, string>
            {
                ["$select"] = "SalesEmployeeCode,SalesEmployeeName",
                ["$filter"] = "Active eq 'tYES'", ["$top"] = "200",
            });
        return B1Json.DictList(await RaiseForB1Async(resp), "value");
    }

    public async Task<List<Dictionary<string, object?>>> GetBpAddressesAsync(string cardCode)
    {
        using var resp = await RequestAsync(HttpMethod.Get,
            $"BusinessPartners('{B1Json.EscapeOData(cardCode)}')",
            new Dictionary<string, string> { ["$select"] = "CardCode,BPAddresses" });
        return B1Json.DictList(await RaiseForB1Async(resp), "BPAddresses");
    }

    public async Task<List<Dictionary<string, object?>>> GetBpContactsAsync(string cardCode)
    {
        using var resp = await RequestAsync(HttpMethod.Get,
            $"BusinessPartners('{B1Json.EscapeOData(cardCode)}')",
            new Dictionary<string, string> { ["$select"] = "CardCode,ContactEmployees" });
        return B1Json.DictList(await RaiseForB1Async(resp), "ContactEmployees");
    }

    public async Task<List<Dictionary<string, object?>>> FetchCustomersPageAsync(int top = 500, int skip = 0)
    {
        using var resp = await RequestAsync(HttpMethod.Get, "BusinessPartners",
            new Dictionary<string, string>
            {
                ["$filter"] = "CardType eq 'cCustomer'",
                ["$select"] = "CardCode,CardName,Phone1,SalesPersonCode,PriceListNum,Currency,UpdateDate",
                ["$orderby"] = "CardCode",
                ["$top"] = top.ToString(), ["$skip"] = skip.ToString(),
            });
        return B1Json.DictList(await RaiseForB1Async(resp), "value");
    }

    public async Task<List<Dictionary<string, object?>>> FetchItemsPageAsync(int top = 500, int skip = 0)
    {
        using var resp = await RequestAsync(HttpMethod.Get, "Items",
            new Dictionary<string, string>
            {
                ["$filter"] = "Valid eq 'tYES' and SalesItem eq 'tYES'",
                ["$select"] = "ItemCode,ItemName,ItemType,SalesUnit,Price,UpdateDate",
                ["$orderby"] = "ItemCode",
                ["$top"] = top.ToString(), ["$skip"] = skip.ToString(),
            });
        return B1Json.DictList(await RaiseForB1Async(resp), "value");
    }

    public async Task<List<Dictionary<string, object?>>> FetchTaxCodesAsync()
    {
        using var resp = await RequestAsync(HttpMethod.Get, "SalesTaxCodes",
            new Dictionary<string, string>
            {
                ["$filter"] = "ValidForAR eq 'tYES'",
                ["$select"] = "Code,Name,Rate",
                ["$orderby"] = "Code",
                ["$top"] = "500",
            });
        return B1Json.DictList(await RaiseForB1Async(resp), "value");
    }
}
