using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SalesWeb.Shared;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Client.Services;

/// <summary>Lỗi API — mang theo HTTP status và message (đọc từ trường "detail").</summary>
public class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

/// <summary>
/// Wrapper HttpClient: tự gắn Authorization Bearer, serialize snake_case,
/// ném ApiException khi HTTP lỗi. Client được host cùng origin với Server
/// nên gọi API bằng URL tương đối "api/...".
/// </summary>
public class ApiClient(HttpClient http, TokenStore tokens)
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url,
        object? body = null, bool auth = true)
    {
        var req = new HttpRequestMessage(method, url);
        if (auth)
        {
            var token = await tokens.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (body is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(body, JsonOpts),
                Encoding.UTF8, "application/json");
        return await http.SendAsync(req);
    }

    private static string ReadDetail(string text, int status)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("detail", out var d))
                {
                    return d.ValueKind == JsonValueKind.String
                        ? d.GetString() ?? ""
                        : d.GetRawText();
                }
            }
            catch { /* không phải JSON */ }
            return text;
        }
        return $"Lỗi {status}";
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string url,
        object? body = null, bool auth = true)
    {
        using var res = await SendRawAsync(method, url, body, auth);
        // B5: 401 → thử refresh token 1 lần rồi gọi lại
        if (res.StatusCode == HttpStatusCode.Unauthorized && auth && await TryRefreshAsync())
        {
            using var retry = await SendRawAsync(method, url, body, auth);
            var retryText = await retry.Content.ReadAsStringAsync();
            if (!retry.IsSuccessStatusCode)
                throw new ApiException((int)retry.StatusCode, ReadDetail(retryText, (int)retry.StatusCode));
            if (string.IsNullOrWhiteSpace(retryText)) return default;
            return JsonSerializer.Deserialize<T>(retryText, JsonOpts);
        }
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new ApiException((int)res.StatusCode, ReadDetail(text, (int)res.StatusCode));
        if (string.IsNullOrWhiteSpace(text)) return default;
        return JsonSerializer.Deserialize<T>(text, JsonOpts);
    }

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _refreshFailed;

    /// <summary>Đổi refresh token lấy access token mới. Trả về false nếu không refresh được.</summary>
    private async Task<bool> TryRefreshAsync()
    {
        if (_refreshFailed) return false;
        await _refreshLock.WaitAsync();
        try
        {
            if (_refreshFailed) return false;
            var refresh = await tokens.GetRefreshTokenAsync();
            if (string.IsNullOrEmpty(refresh)) { _refreshFailed = true; return false; }
            var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new RefreshIn { RefreshToken = refresh }, JsonOpts),
                    Encoding.UTF8, "application/json"),
            };
            using var res = await http.SendAsync(req);
            if (!res.IsSuccessStatusCode) { _refreshFailed = true; return false; }
            var text = await res.Content.ReadAsStringAsync();
            var tok = JsonSerializer.Deserialize<TokenOut>(text, JsonOpts);
            if (tok is null || string.IsNullOrEmpty(tok.AccessToken)) { _refreshFailed = true; return false; }
            await tokens.SetTokenAsync(tok.AccessToken);
            await tokens.SetRefreshTokenAsync(tok.RefreshToken);
            return true;
        }
        catch { _refreshFailed = true; return false; }
        finally { _refreshLock.Release(); }
    }

    /// <summary>Đăng xuất: thu hồi refresh token phía server + xoá token local.</summary>
    public async Task LogoutAsync()
    {
        try
        {
            var refresh = await tokens.GetRefreshTokenAsync();
            if (!string.IsNullOrEmpty(refresh))
                await SendNoContentAsync(HttpMethod.Post, "api/auth/logout",
                    new RefreshIn { RefreshToken = refresh }, auth: false);
        }
        catch { /* bỏ qua */ }
        await tokens.ClearAsync();
        _refreshFailed = false;
    }

    private async Task SendNoContentAsync(HttpMethod method, string url,
        object? body = null, bool auth = true)
    {
        using var res = await SendRawAsync(method, url, body, auth);
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new ApiException((int)res.StatusCode, ReadDetail(text, (int)res.StatusCode));
    }

    private static string Q(string url, Dictionary<string, string?> p)
    {
        var qs = string.Join("&", p
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}"));
        return string.IsNullOrEmpty(qs) ? url : $"{url}?{qs}";
    }

    // ---------------- Auth ----------------
    public Task<TokenOut?> LoginAsync(string username, string password, string companyDb = "")
        => SendAsync<TokenOut>(HttpMethod.Post, "api/auth/login",
            new LoginIn { Username = username, Password = password, CompanyDb = companyDb }, auth: false);

    public Task<DbListOut?> GetDatabasesAsync()
        => SendAsync<DbListOut>(HttpMethod.Get, "api/auth/databases", auth: false);

    public Task<UserOut?> GetMeAsync() => SendAsync<UserOut>(HttpMethod.Get, "api/auth/me");

    public Task<List<UserOut>?> ListUsersAsync()
        => SendAsync<List<UserOut>>(HttpMethod.Get, "api/auth/users");

    public Task<UserOut?> CreateUserAsync(UserCreate u)
        => SendAsync<UserOut>(HttpMethod.Post, "api/auth/users", u);

    public Task<UserOut?> UpdateUserAsync(string username, UserUpdate u)
        => SendAsync<UserOut>(HttpMethod.Patch, $"api/auth/users/{Uri.EscapeDataString(username)}", u);

    public Task<RoleMatrix?> GetRoleMatrixAsync()
        => SendAsync<RoleMatrix>(HttpMethod.Get, "api/auth/roles");

    // ---------------- Orders ----------------
    public Task<PagedOrders?> ListOrdersAsync(string? status, string? cardCode, int top, int skip)
        => SendAsync<PagedOrders>(HttpMethod.Get, Q("api/orders", new Dictionary<string, string?>
        {
            ["status"] = status, ["card_code"] = cardCode,
            ["top"] = top.ToString(), ["skip"] = skip.ToString(),
        }));

    public async Task<(OrderOut? Order, string? ETag)> GetOrderAsync(int docEntry)
    {
        // Fix #7: dùng SendAsyncWithResponse để có refresh-on-401 như các API khác
        using var res = await SendWithRefreshAsync(HttpMethod.Get, $"api/orders/{docEntry}");
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new ApiException((int)res.StatusCode, ReadDetail(text, (int)res.StatusCode));
        var etag = res.Headers.ETag?.Tag?.Trim('"')
            ?? (res.Headers.TryGetValues("ETag", out var vs) ? vs.FirstOrDefault()?.Trim('"') : null);
        return (JsonSerializer.Deserialize<OrderOut>(text, JsonOpts), etag);
    }

    /// <summary>Gửi request có tự refresh token khi 401 — dùng cho các API cần đọc response thô.</summary>
    private async Task<HttpResponseMessage> SendWithRefreshAsync(HttpMethod method, string url,
        object? body = null, bool auth = true)
    {
        var res = await SendRawAsync(method, url, body, auth);
        if (res.StatusCode == HttpStatusCode.Unauthorized && auth && await TryRefreshAsync())
        {
            res.Dispose();
            res = await SendRawAsync(method, url, body, auth);
        }
        return res;
    }

    public Task<OrderOut?> CreateOrderAsync(OrderCreate o)
        => SendAsync<OrderOut>(HttpMethod.Post, "api/orders", o);

    public Task<OrderOut?> UpdateOrderAsync(int docEntry, OrderUpdate u)
        => SendAsync<OrderOut>(HttpMethod.Patch, $"api/orders/{docEntry}", u);

    public Task<OrderOut?> CancelOrderAsync(int docEntry)
        => SendAsync<OrderOut>(HttpMethod.Post, $"api/orders/{docEntry}/cancel");

    public Task<OrderOut?> CloseOrderAsync(int docEntry, List<int>? lineNums)
        => SendAsync<OrderOut>(HttpMethod.Post, $"api/orders/{docEntry}/close",
            new CloseLinesIn { LineNums = lineNums });

    // ---------------- Master data ----------------
    public Task<List<CustomerOut>?> SearchCustomersAsync(string q)
        => SendAsync<List<CustomerOut>>(HttpMethod.Get,
            $"api/masterdata/customers?q={Uri.EscapeDataString(q)}");

    public Task<List<ItemOut>?> SearchItemsAsync(string q)
        => SendAsync<List<ItemOut>>(HttpMethod.Get,
            $"api/masterdata/items?q={Uri.EscapeDataString(q)}");

    public Task<List<CustomerOut>?> ListAllCustomersAsync()
        => SendAsync<List<CustomerOut>>(HttpMethod.Get, "api/masterdata/customers/all");

    public Task<List<ItemOut>?> ListAllItemsAsync()
        => SendAsync<List<ItemOut>>(HttpMethod.Get, "api/masterdata/items/all");

    public Task<List<WarehouseOut>?> ListWarehousesAsync()
        => SendAsync<List<WarehouseOut>>(HttpMethod.Get, "api/masterdata/warehouses");

    public Task<List<PaymentTermOut>?> ListPaymentTermsAsync()
        => SendAsync<List<PaymentTermOut>>(HttpMethod.Get, "api/masterdata/payment-terms");

    public Task<List<ShippingTypeOut>?> ListShippingTypesAsync()
        => SendAsync<List<ShippingTypeOut>>(HttpMethod.Get, "api/masterdata/shipping-types");

    public Task<List<SalesPersonOut>?> ListSalesPersonsAsync()
        => SendAsync<List<SalesPersonOut>>(HttpMethod.Get, "api/masterdata/sales-persons");

    public Task<List<TaxCodeOut>?> ListTaxCodesAsync()
        => SendAsync<List<TaxCodeOut>>(HttpMethod.Get, "api/masterdata/tax-codes");

    public Task<List<BpAddressOut>?> GetCustomerAddressesAsync(string cardCode)
        => SendAsync<List<BpAddressOut>>(HttpMethod.Get,
            $"api/masterdata/customers/{Uri.EscapeDataString(cardCode)}/addresses");

    public Task<List<BpContactOut>?> GetCustomerContactsAsync(string cardCode)
        => SendAsync<List<BpContactOut>>(HttpMethod.Get,
            $"api/masterdata/customers/{Uri.EscapeDataString(cardCode)}/contacts");

    // ---------------- Sync ----------------
    public Task<List<SyncEntityStatus>?> GetSyncStatusAsync()
        => SendAsync<List<SyncEntityStatus>>(HttpMethod.Get, "api/sync/status");

    public Task<SyncStartOut?> TriggerSyncAsync(List<string> entities)
        => SendAsync<SyncStartOut>(HttpMethod.Post, "api/sync", new { entities });

    // ---------------- Audit ----------------
    public Task<PagedAudit?> ListAuditAsync(string? operation, string? status, string? q, int limit = 100)
        => SendAsync<PagedAudit>(HttpMethod.Get, Q("api/audit", new Dictionary<string, string?>
        {
            ["operation"] = operation, ["status"] = status, ["q"] = q,
            ["limit"] = limit.ToString(),
        }));

    /// <summary>Nhật ký thao tác người dùng (B6) — ai làm gì, khi nào.</summary>
    public Task<List<Dictionary<string, object>>?> ListUserAuditAsync(int limit = 200)
        => SendAsync<List<Dictionary<string, object>>>(HttpMethod.Get,
            $"api/auth/audit?limit={limit}");

    /// <summary>Thống kê dashboard (A1).</summary>
    public Task<DashboardStats?> GetDashboardStatsAsync()
        => SendAsync<DashboardStats>(HttpMethod.Get, "api/orders/stats");

    /// <summary>Tải file Excel danh sách đơn (A2) — trả về bytes.</summary>
    public async Task<byte[]?> ExportOrdersExcelAsync(string? status, string? cardCode)
    {
        var token = await tokens.GetTokenAsync();
        var req = new HttpRequestMessage(HttpMethod.Get,
            Q("api/orders/export", new Dictionary<string, string?>
            {
                ["status"] = status, ["card_code"] = cardCode,
            }));
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await http.SendAsync(req);
        if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized && await TryRefreshAsync())
            return await ExportOrdersExcelAsync(status, cardCode);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsByteArrayAsync();
    }

    public Task<AuditDetailOut?> GetAuditDetailAsync(int id)
        => SendAsync<AuditDetailOut>(HttpMethod.Get, $"api/audit/{id}");

    public Task<RetryOut?> RetryAuditAsync(int id)
        => SendAsync<RetryOut>(HttpMethod.Post, $"api/audit/{id}/retry");

    public Task<RetryAllOut?> RetryAllAuditAsync()
        => SendAsync<RetryAllOut>(HttpMethod.Post, "api/audit/retry-all");

    public Task<FailedCountOut?> GetFailedCountAsync()
        => SendAsync<FailedCountOut>(HttpMethod.Get, "api/audit/failed-count");

    // ---------------- Settings ----------------
    public Task<ConnSettingsOut?> GetConnSettingsAsync()
        => SendAsync<ConnSettingsOut>(HttpMethod.Get, "api/settings/connection");

    public Task<ConnSaveOut?> SaveConnSettingsAsync(ConnSettingsIn s)
        => SendAsync<ConnSaveOut>(HttpMethod.Put, "api/settings/connection", s);

    public Task<ConnTestOut?> TestConnSettingsAsync(ConnTestIn t)
        => SendAsync<ConnTestOut>(HttpMethod.Post, "api/settings/connection/test", t);

    public Task<DiscountLimitsOut?> GetDiscountLimitsAsync()
        => SendAsync<DiscountLimitsOut>(HttpMethod.Get, "api/settings/discount-limits");

    public Task<DiscountLimitsOut?> SaveDiscountLimitsAsync(DiscountLimitsIn l)
        => SendAsync<DiscountLimitsOut>(HttpMethod.Put, "api/settings/discount-limits", l);

    public Task<DbListOut?> GetDbListAsync()
        => SendAsync<DbListOut>(HttpMethod.Get, "api/settings/databases");

    public Task<DbListOut?> SaveDbListAsync(List<DbEntry> dbs)
        => SendAsync<DbListOut>(HttpMethod.Put, "api/settings/databases", dbs);

    public Task<HealthOut?> GetHealthAsync()
        => SendAsync<HealthOut>(HttpMethod.Get, "health", auth: false);
}
