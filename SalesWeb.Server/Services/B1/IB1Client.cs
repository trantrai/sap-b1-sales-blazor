namespace SalesWeb.Server.Services.B1;

/// <summary>Interface client SAP Business One Service Layer — dùng chung cho Real và Mock.</summary>
public interface IB1Client
{
    Task LoginAsync();
    Task CloseAsync();

    // ----- Sales Orders -----
    Task<Dictionary<string, object?>> CreateOrderAsync(Dictionary<string, object?> payload);
    Task<(Dictionary<string, object?> Order, string? ETag)> GetOrderAsync(int docEntry);
    Task<Dictionary<string, object?>> ListOrdersAsync(int top = 50, int skip = 0, string? filter = null, string orderby = "DocEntry desc");
    Task UpdateOrderAsync(int docEntry, Dictionary<string, object?> payload, string? etag = null);
    Task<Dictionary<string, object?>> CancelOrderAsync(int docEntry);
    Task CloseOrderLinesAsync(int docEntry, List<int>? lineNums);
    Task<Dictionary<string, object?>?> FindOrderByExternalIdAsync(string externalId, string? udf = null);

    // ----- Master data -----
    Task<List<Dictionary<string, object?>>> SearchBusinessPartnersAsync(string q, int top = 20);
    Task<List<Dictionary<string, object?>>> SearchItemsAsync(string q, int top = 20);
    Task<List<Dictionary<string, object?>>> ListWarehousesAsync();
    Task<List<Dictionary<string, object?>>> ListPaymentTermsAsync();
    Task<List<Dictionary<string, object?>>> ListShippingTypesAsync();
    Task<List<Dictionary<string, object?>>> ListSalesPersonsAsync();
    Task<List<Dictionary<string, object?>>> GetBpAddressesAsync(string cardCode);
    Task<List<Dictionary<string, object?>>> GetBpContactsAsync(string cardCode);

    // ----- Đồng bộ master data (phân trang) -----
    Task<List<Dictionary<string, object?>>> FetchCustomersPageAsync(int top = 500, int skip = 0);
    Task<List<Dictionary<string, object?>>> FetchItemsPageAsync(int top = 500, int skip = 0);
    Task<List<Dictionary<string, object?>>> FetchTaxCodesAsync();
}
