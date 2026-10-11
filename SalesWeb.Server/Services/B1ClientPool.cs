using System.Collections.Concurrent;
using SalesWeb.Server.Services.B1;

namespace SalesWeb.Server.Services;

/// <summary>
/// Pool B1 client theo từng CompanyDB. Mỗi database có client riêng (mock: dữ liệu
/// trong RAM tách biệt từng DB; live: session Service Layer riêng từng CompanyDB).
/// Database user chọn lúc đăng nhập được lưu trong JWT claim "b1_db".
/// </summary>
public sealed class B1ClientPool
{
    private readonly ConnService _conn;
    private readonly ConcurrentDictionary<string, IB1Client> _clients = new();

    public B1ClientPool(ConnService conn) => _conn = conn;

    public string DefaultDb => _conn.DefaultCompanyDb();

    /// <summary>Lấy client cho CompanyDB (rỗng = database mặc định của hệ thống).</summary>
    public IB1Client GetClient(string? companyDb)
    {
        var db = string.IsNullOrWhiteSpace(companyDb) ? DefaultDb : companyDb.Trim();
        // Chỉ cho phép DB đã khai báo; DB lạ -> về mặc định (tránh session rác)
        var allowed = _conn.GetDatabases().Select(d => d.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!allowed.Contains(db))
            db = DefaultDb;
        return _clients.GetOrAdd(db, static (key, self) => self._conn.BuildClient(key), this);
    }

    /// <summary>Xóa toàn bộ client (gọi khi lưu cài đặt kết nối) để áp dụng cấu hình mới.</summary>
    public async Task ClearAsync()
    {
        var all = _clients.Values.ToList();
        _clients.Clear();
        foreach (var c in all)
        {
            try { await c.CloseAsync(); } catch { /* bỏ qua */ }
        }
    }
}

/// <summary>
/// Proxy IB1Client theo request: điều hướng mọi cuộc gọi tới client của đúng
/// CompanyDB mà user đã chọn lúc đăng nhập. Controllers giữ nguyên DI IB1Client.
/// </summary>
public sealed class B1ClientProxy : IB1Client
{
    private readonly IHttpContextAccessor _http;
    private readonly B1ClientPool _pool;

    public B1ClientProxy(IHttpContextAccessor http, B1ClientPool pool)
    {
        _http = http;
        _pool = pool;
    }

    private IB1Client Current
    {
        get
        {
            var db = (_http.HttpContext?.Items["AppUser"] as AppUser)?.CompanyDb;
            return _pool.GetClient(db);
        }
    }

    public Task LoginAsync() => Current.LoginAsync();
    public Task CloseAsync() => Current.CloseAsync();
    public Task<Dictionary<string, object?>> CreateOrderAsync(Dictionary<string, object?> payload)
        => Current.CreateOrderAsync(payload);
    public Task<(Dictionary<string, object?> Order, string? ETag)> GetOrderAsync(int docEntry)
        => Current.GetOrderAsync(docEntry);
    public Task<Dictionary<string, object?>> ListOrdersAsync(int top = 50, int skip = 0, string? filter = null, string orderby = "DocEntry desc")
        => Current.ListOrdersAsync(top, skip, filter, orderby);
    public Task UpdateOrderAsync(int docEntry, Dictionary<string, object?> payload, string? etag = null)
        => Current.UpdateOrderAsync(docEntry, payload, etag);
    public Task<Dictionary<string, object?>> CancelOrderAsync(int docEntry)
        => Current.CancelOrderAsync(docEntry);
    public Task CloseOrderLinesAsync(int docEntry, List<int>? lineNums)
        => Current.CloseOrderLinesAsync(docEntry, lineNums);
    public Task<Dictionary<string, object?>?> FindOrderByExternalIdAsync(string externalId, string? udf = null)
        => Current.FindOrderByExternalIdAsync(externalId, udf);
    public Task<List<Dictionary<string, object?>>> SearchBusinessPartnersAsync(string q, int top = 20)
        => Current.SearchBusinessPartnersAsync(q, top);
    public Task<List<Dictionary<string, object?>>> SearchItemsAsync(string q, int top = 20)
        => Current.SearchItemsAsync(q, top);
    public Task<List<Dictionary<string, object?>>> ListWarehousesAsync()
        => Current.ListWarehousesAsync();
    public Task<List<Dictionary<string, object?>>> ListPaymentTermsAsync()
        => Current.ListPaymentTermsAsync();
    public Task<List<Dictionary<string, object?>>> ListShippingTypesAsync()
        => Current.ListShippingTypesAsync();
    public Task<List<Dictionary<string, object?>>> ListSalesPersonsAsync()
        => Current.ListSalesPersonsAsync();
    public Task<List<Dictionary<string, object?>>> GetBpAddressesAsync(string cardCode)
        => Current.GetBpAddressesAsync(cardCode);
    public Task<List<Dictionary<string, object?>>> GetBpContactsAsync(string cardCode)
        => Current.GetBpContactsAsync(cardCode);
    public Task<List<Dictionary<string, object?>>> FetchCustomersPageAsync(int top = 500, int skip = 0)
        => Current.FetchCustomersPageAsync(top, skip);
    public Task<List<Dictionary<string, object?>>> FetchItemsPageAsync(int top = 500, int skip = 0)
        => Current.FetchItemsPageAsync(top, skip);
    public Task<List<Dictionary<string, object?>>> FetchTaxCodesAsync()
        => Current.FetchTaxCodesAsync();
}
