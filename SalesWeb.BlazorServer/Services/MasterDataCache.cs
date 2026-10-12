using SalesWeb.Shared.Dtos;

namespace SalesWeb.BlazorServer.Services;

/// <summary>
/// Cache master data (khách hàng, hàng hoá) phía client.
/// - Tải toàn bộ danh sách 1 lần (lazy, khi autocomplete dùng lần đầu hoặc preload sau login).
/// - Autocomplete chỉ tìm khi nhập tối thiểu 2 ký tự, lọc hoàn toàn trên bộ nhớ — không gọi API mỗi lần gõ.
/// - Gọi <see cref="Invalidate"/> sau khi đồng bộ master data để tải lại.
/// </summary>
public sealed class MasterDataCache
{
    private const int MinChars = 2;
    private const int MaxResults = 20;

    private readonly ApiClient _api;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<CustomerOut>? _customers;
    private List<ItemOut>? _items;

    public MasterDataCache(ApiClient api) => _api = api;

    public bool IsLoaded => _customers is not null && _items is not null;

    /// <summary>Tìm khách hàng theo mã/tên. Trả về rỗng nếu chưa nhập đủ 2 ký tự.</summary>
    public async Task<List<CustomerOut>> SearchCustomersAsync(string? q)
    {
        var t = (q ?? "").Trim();
        if (t.Length < MinChars) return new();
        var all = await GetCustomersAsync();
        return all
            .Where(c => c.CardCode.Contains(t, StringComparison.OrdinalIgnoreCase)
                     || (c.CardName?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(MaxResults)
            .ToList();
    }

    /// <summary>Tìm hàng hoá theo mã/tên. Trả về rỗng nếu chưa nhập đủ 2 ký tự.</summary>
    public async Task<List<ItemOut>> SearchItemsAsync(string? q)
    {
        var t = (q ?? "").Trim();
        if (t.Length < MinChars) return new();
        var all = await GetItemsAsync();
        return all
            .Where(i => i.ItemCode.Contains(t, StringComparison.OrdinalIgnoreCase)
                     || (i.ItemName?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(MaxResults)
            .ToList();
    }

    /// <summary>Tải trước cả hai danh sách (gọi nền sau login để autocomplete phản hồi ngay).</summary>
    public async Task PreloadAsync()
    {
        await GetCustomersAsync();
        await GetItemsAsync();
    }

    /// <summary>Xoá cache — gọi sau khi đồng bộ master data xong.</summary>
    public void Invalidate()
    {
        _customers = null;
        _items = null;
    }

    private async Task<List<CustomerOut>> GetCustomersAsync()
    {
        if (_customers is not null) return _customers;
        await _lock.WaitAsync();
        try
        {
            _customers ??= await _api.ListAllCustomersAsync() ?? new();
            return _customers;
        }
        finally { _lock.Release(); }
    }

    private async Task<List<ItemOut>> GetItemsAsync()
    {
        if (_items is not null) return _items;
        await _lock.WaitAsync();
        try
        {
            _items ??= await _api.ListAllItemsAsync() ?? new();
            return _items;
        }
        finally { _lock.Release(); }
    }
}
