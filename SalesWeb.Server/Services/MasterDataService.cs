using System.Collections.Concurrent;
using SalesWeb.Server.Data;
using SalesWeb.Server.Middleware;
using SalesWeb.Server.Services.B1;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Server.Services;

/// <summary>Tra cứu master data — ưu tiên DB local, fallback B1 khi DB trống.</summary>
public sealed class MasterDataService
{
    private readonly IB1Client _b1;
    private readonly string _companyDb;
    private static readonly ConcurrentDictionary<string, (DateTime At, object Value)> Cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public MasterDataService(IB1Client b1, string? companyDb = null)
    {
        _b1 = b1;
        _companyDb = string.IsNullOrWhiteSpace(companyDb) ? "" : companyDb.Trim();
    }

    private string CacheKey(string key) => $"{_companyDb}:{key}";

    private static T? CacheGet<T>(string key) where T : class
    {
        if (Cache.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.At < Ttl)
            return (T)hit.Value;
        return null;
    }

    private static void CacheSet(string key, object value)
        => Cache[key] = (DateTime.UtcNow, value);

    /// <summary>P2-4: xoá cache theo CompanyDB (khi đổi settings/database).</summary>
    public static void InvalidateCompany(string companyDb)
    {
        var prefix = (string.IsNullOrWhiteSpace(companyDb) ? "" : companyDb.Trim()) + ":";
        foreach (var k in Cache.Keys)
            if (k.StartsWith(prefix, StringComparison.Ordinal))
                Cache.TryRemove(k, out _);
    }

    /// <summary>P2-4: xoá toàn bộ cache (khi đổi connection settings).</summary>
    public static void InvalidateAll() => Cache.Clear();

    public async Task<List<CustomerOut>> ListAllCustomersAsync()
    {
        var key = CacheKey("cust:__all__");
        var hit = CacheGet<List<CustomerOut>>(key);
        if (hit is not null) return hit;
        List<CustomerOut> out_;
        if (MdStore.Count("customers", _companyDb) > 0)
        {
            out_ = MdStore.SearchCustomers(_companyDb, "", top: 5000).Select(r => new CustomerOut
            {
                CardCode = B1Json.Str(r, "card_code"),
                CardName = B1Json.Str(r, "card_name"),
                Phone = B1Json.Str(r, "phone"),
                SalesPersonCode = B1Json.IntN(r, "sales_person_code"),
                PriceListNum = B1Json.IntN(r, "price_list_num"),
            }).ToList();
        }
        else
        {
            // Chưa đồng bộ: fallback sang search rỗng qua B1 (nếu B1 hỗ trợ)
            out_ = await SearchCustomersAsync("");
        }
        CacheSet(key, out_);
        return out_;
    }

    public async Task<List<CustomerOut>> SearchCustomersAsync(string q)
    {
        var key = CacheKey($"cust:{q.ToLowerInvariant()}");
        var hit = CacheGet<List<CustomerOut>>(key);
        if (hit is not null) return hit;
        List<CustomerOut> out_;
        if (MdStore.Count("customers", _companyDb) > 0)
        {
            out_ = MdStore.SearchCustomers(_companyDb, q).Select(r => new CustomerOut
            {
                CardCode = B1Json.Str(r, "card_code"),
                CardName = B1Json.Str(r, "card_name"),
                Phone = B1Json.Str(r, "phone"),
                SalesPersonCode = B1Json.IntN(r, "sales_person_code"),
                PriceListNum = B1Json.IntN(r, "price_list_num"),
            }).ToList();
        }
        else
        {
            List<Dictionary<string, object?>> rows;
            try { rows = await _b1.SearchBusinessPartnersAsync(q); }
            catch (B1Exception ex) { throw new ApiException(502, $"Lỗi tra cứu khách hàng từ B1: {ex.Message}"); }
            out_ = rows.Select(r => new CustomerOut
            {
                CardCode = B1Json.Str(r, "CardCode"),
                CardName = B1Json.Str(r, "CardName"),
                Phone = B1Json.Str(r, "Phone1"),
                SalesPersonCode = B1Json.IntN(r, "SalesPersonCode"),
                PriceListNum = B1Json.IntN(r, "PriceListNum"),
            }).ToList();
        }
        CacheSet(key, out_);
        return out_;
    }

    public async Task<List<ItemOut>> SearchItemsAsync(string q)
    {
        var key = CacheKey($"item:{q.ToLowerInvariant()}");
        var hit = CacheGet<List<ItemOut>>(key);
        if (hit is not null) return hit;
        List<ItemOut> out_;
        if (MdStore.Count("items", _companyDb) > 0)
        {
            out_ = MdStore.SearchItems(_companyDb, q).Select(r => new ItemOut
            {
                ItemCode = B1Json.Str(r, "item_code"),
                ItemName = B1Json.Str(r, "item_name"),
                SalesUnit = B1Json.Str(r, "sales_unit"),
                Price = B1Json.Num(r, "price"),
                TaxCode = B1Json.Str(r, "tax_code"),
            }).ToList();
        }
        else
        {
            List<Dictionary<string, object?>> rows;
            try { rows = await _b1.SearchItemsAsync(q); }
            catch (B1Exception ex) { throw new ApiException(502, $"Lỗi tra cứu hàng hoá từ B1: {ex.Message}"); }
            out_ = rows.Select(r => new ItemOut
            {
                ItemCode = B1Json.Str(r, "ItemCode"),
                ItemName = B1Json.Str(r, "ItemName"),
                SalesUnit = B1Json.Str(r, "SalesUnit"),
                Price = B1Json.Num(r, "Price"),
                TaxCode = B1Json.Str(r, "TaxCode"),
            }).ToList();
        }
        CacheSet(key, out_);
        return out_;
    }

    public async Task<List<ItemOut>> ListAllItemsAsync()
    {
        var key = CacheKey("item:__all__");
        var hit = CacheGet<List<ItemOut>>(key);
        if (hit is not null) return hit;
        List<ItemOut> out_;
        if (MdStore.Count("items", _companyDb) > 0)
        {
            out_ = MdStore.SearchItems(_companyDb, "", top: 5000).Select(r => new ItemOut
            {
                ItemCode = B1Json.Str(r, "item_code"),
                ItemName = B1Json.Str(r, "item_name"),
                SalesUnit = B1Json.Str(r, "sales_unit"),
                Price = B1Json.Num(r, "price"),
                TaxCode = B1Json.Str(r, "tax_code"),
            }).ToList();
        }
        else
        {
            out_ = await SearchItemsAsync("");
        }
        CacheSet(key, out_);
        return out_;
    }

    /// <summary>Thuế suất — luôn đọc từ DB local (đồng bộ 1 lần, ít thay đổi).</summary>
    public Task<List<TaxCodeOut>> ListTaxCodesAsync()
        => Task.FromResult(MdStore.ListTaxCodes(_companyDb).Select(r => new TaxCodeOut
        {
            Code = B1Json.Str(r, "code"),
            Name = B1Json.Str(r, "name"),
            Rate = B1Json.Num(r, "rate"),
        }).ToList());

    public async Task<List<WarehouseOut>> ListWarehousesAsync()
    {
        var hit = CacheGet<List<WarehouseOut>>(CacheKey("warehouses"));
        if (hit is not null) return hit;
        List<Dictionary<string, object?>> rows;
        try { rows = await _b1.ListWarehousesAsync(); }
        catch (B1Exception ex) { throw new ApiException(502, $"Lỗi tra cứu kho từ B1: {ex.Message}"); }
        var out_ = rows.Select(r => new WarehouseOut
        {
            WarehouseCode = B1Json.Str(r, "WarehouseCode"),
            WarehouseName = B1Json.Str(r, "WarehouseName"),
        }).ToList();
        CacheSet(CacheKey("warehouses"), out_);
        return out_;
    }

    public async Task<List<PaymentTermOut>> ListPaymentTermsAsync()
    {
        var hit = CacheGet<List<PaymentTermOut>>(CacheKey("payment_terms"));
        if (hit is not null) return hit;
        List<Dictionary<string, object?>> rows;
        try { rows = await _b1.ListPaymentTermsAsync(); }
        catch (B1Exception ex) { throw new ApiException(502, $"Lỗi tra cứu điều khoản thanh toán từ B1: {ex.Message}"); }
        var out_ = rows.Select(r => new PaymentTermOut
        {
            GroupNumber = B1Json.IntN(r, "GroupNumber") ?? 0,
            Name = B1Json.Str(r, "PaymentTermsGroupName"),
        }).ToList();
        CacheSet(CacheKey("payment_terms"), out_);
        return out_;
    }

    public async Task<List<ShippingTypeOut>> ListShippingTypesAsync()
    {
        var hit = CacheGet<List<ShippingTypeOut>>(CacheKey("shipping_types"));
        if (hit is not null) return hit;
        List<Dictionary<string, object?>> rows;
        try { rows = await _b1.ListShippingTypesAsync(); }
        catch (B1Exception ex) { throw new ApiException(502, $"Lỗi tra cứu hình thức vận chuyển từ B1: {ex.Message}"); }
        var out_ = rows.Select(r => new ShippingTypeOut
        {
            Code = B1Json.IntN(r, "Code") ?? 0,
            Name = B1Json.Str(r, "Name"),
        }).ToList();
        CacheSet(CacheKey("shipping_types"), out_);
        return out_;
    }

    public async Task<List<SalesPersonOut>> ListSalesPersonsAsync()
    {
        var hit = CacheGet<List<SalesPersonOut>>(CacheKey("sales_persons"));
        if (hit is not null) return hit;
        List<Dictionary<string, object?>> rows;
        try { rows = await _b1.ListSalesPersonsAsync(); }
        catch (B1Exception ex) { throw new ApiException(502, $"Lỗi tra cứu nhân viên kinh doanh từ B1: {ex.Message}"); }
        var out_ = rows.Select(r => new SalesPersonOut
        {
            Code = B1Json.IntN(r, "SalesEmployeeCode") ?? 0,
            Name = B1Json.Str(r, "SalesEmployeeName"),
        }).ToList();
        CacheSet(CacheKey("sales_persons"), out_);
        return out_;
    }

    public async Task<List<BpAddressOut>> GetBpAddressesAsync(string cardCode)
    {
        List<Dictionary<string, object?>> rows;
        try { rows = await _b1.GetBpAddressesAsync(cardCode); }
        catch (B1Exception ex) { throw new ApiException(502, $"Lỗi tra cứu địa chỉ KH từ B1: {ex.Message}"); }
        return rows.Select(r => new BpAddressOut
        {
            AddressName = B1Json.Str(r, "AddressName"),
            AddressType = B1Json.Str(r, "AddressType"),
            Street = B1Json.Str(r, "Street"),
            City = B1Json.Str(r, "City"),
            Country = B1Json.Str(r, "Country"),
        }).ToList();
    }

    public async Task<List<BpContactOut>> GetBpContactsAsync(string cardCode)
    {
        List<Dictionary<string, object?>> rows;
        try { rows = await _b1.GetBpContactsAsync(cardCode); }
        catch (B1Exception ex) { throw new ApiException(502, $"Lỗi tra cứu người liên hệ từ B1: {ex.Message}"); }
        return rows.Select(r => new BpContactOut
        {
            Name = B1Json.Str(r, "Name") != "" ? B1Json.Str(r, "Name")
                : string.Join(" ", new[] { B1Json.Str(r, "FirstName"), B1Json.Str(r, "LastName") }
                    .Where(x => x != "")),
            Phone = B1Json.Str(r, "Phone1"),
            Mobile = B1Json.Str(r, "MobilePhone"),
            Email = B1Json.Str(r, "E_Mail"),
        }).ToList();
    }
}
