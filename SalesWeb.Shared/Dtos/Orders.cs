namespace SalesWeb.Shared.Dtos;

// ---------------- Đầu vào ----------------
public class OrderLineIn
{
    public string ItemCode { get; set; } = "";
    public double Quantity { get; set; }
    public double Price { get; set; }
    public string? Warehouse { get; set; }
    public string? TaxCode { get; set; }
    public double DiscountPercent { get; set; }
    public DateOnly? ShipDate { get; set; }
    public string? ProjectCode { get; set; }
    public string? CostingCode { get; set; }
    public string? UomCode { get; set; }
}

public class OrderCreate
{
    public string? ExternalId { get; set; }
    public string CardCode { get; set; } = "";
    public DateOnly? DocDate { get; set; }
    public DateOnly? DocDueDate { get; set; }
    public DateOnly? TaxDate { get; set; }
    public string? NumAtCard { get; set; }
    public string? DocCurrency { get; set; }
    public double? DocRate { get; set; }
    public int? ContactPersonCode { get; set; }
    public string? ShipToCode { get; set; }
    public string? PayToCode { get; set; }
    public int? PaymentGroupCode { get; set; }
    public int? TransportationCode { get; set; }
    public string? Project { get; set; }
    public int? Series { get; set; }
    public string? Comments { get; set; }
    public double DiscountPercent { get; set; }
    public int? SalesPersonCode { get; set; }
    public List<OrderLineIn> Lines { get; set; } = new();
}

public class OrderLineUpdate
{
    public int LineNum { get; set; }
    public double? Quantity { get; set; }
    public double? Price { get; set; }
    public string? Warehouse { get; set; }
    public double? DiscountPercent { get; set; }
    public DateOnly? ShipDate { get; set; }
}

public class OrderUpdate
{
    public string? Etag { get; set; }
    public string? Comments { get; set; }
    public DateOnly? DocDueDate { get; set; }
    public DateOnly? TaxDate { get; set; }
    public string? NumAtCard { get; set; }
    public int? ContactPersonCode { get; set; }
    public string? ShipToCode { get; set; }
    public string? PayToCode { get; set; }
    public int? PaymentGroupCode { get; set; }
    public int? TransportationCode { get; set; }
    public string? Project { get; set; }
    public double? DiscountPercent { get; set; }
    public List<OrderLineUpdate>? Lines { get; set; }
}

public class CloseLinesIn
{
    public List<int>? LineNums { get; set; }
}

// ---------------- Đầu ra ----------------
public class OrderLineOut
{
    public int LineNum { get; set; }
    public string ItemCode { get; set; } = "";
    public string Description { get; set; } = "";
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double DiscountPercent { get; set; }
    public string Warehouse { get; set; } = "";
    public string TaxCode { get; set; } = "";
    public string ShipDate { get; set; } = "";
    public string ProjectCode { get; set; } = "";
    public string CostingCode { get; set; } = "";
    public string UomCode { get; set; } = "";
    public string LineStatus { get; set; } = "";
    public double? LineTotal { get; set; }
}

public class OrderOut
{
    public int DocEntry { get; set; }
    public int DocNum { get; set; }
    public string DocDate { get; set; } = "";
    public string DocDueDate { get; set; } = "";
    public string TaxDate { get; set; } = "";
    public string CardCode { get; set; } = "";
    public string CardName { get; set; } = "";
    public string NumAtCard { get; set; } = "";
    public double DocTotal { get; set; }
    public string Currency { get; set; } = "";
    public double DocRate { get; set; } = 1;
    public string Status { get; set; } = "open"; // open | closed | cancelled
    public string Comments { get; set; } = "";
    public int? SalesPersonCode { get; set; }
    public int? ContactPersonCode { get; set; }
    public string ShipToCode { get; set; } = "";
    public string PayToCode { get; set; } = "";
    public int? PaymentGroupCode { get; set; }
    public int? TransportationCode { get; set; }
    public string Project { get; set; } = "";
    public int? Series { get; set; }
    public double DiscountPercent { get; set; }
    public string Address { get; set; } = "";
    public string? ExternalId { get; set; }
    public List<OrderLineOut> Lines { get; set; } = new();
}

public class PagedOrders
{
    public int Total { get; set; }
    public List<OrderOut> Items { get; set; } = new();
}

/// <summary>Thống kê dashboard (A1).</summary>
public class DashboardStats
{
    public int TotalOrders { get; set; }
    public double TotalRevenue { get; set; }
    public int OpenOrders { get; set; }
    public int ClosedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public List<DailyRevenue> RevenueByDay { get; set; } = new();
    public List<TopCustomer> TopCustomers { get; set; } = new();
    public List<TopItem> TopItems { get; set; } = new();
    /// <summary>Ghi chú phạm vi số liệu (fix #5): ví dụ "Số liệu trong 1.000 đơn gần nhất".</summary>
    public string ScopeNote { get; set; } = "";
}

public class DailyRevenue
{
    public string Date { get; set; } = "";
    public double Revenue { get; set; }
    public int Orders { get; set; }
}

public class TopCustomer
{
    public string CardCode { get; set; } = "";
    public string CardName { get; set; } = "";
    public double Revenue { get; set; }
    public int Orders { get; set; }
}

public class TopItem
{
    public string ItemCode { get; set; } = "";
    public string ItemName { get; set; } = "";
    public double Quantity { get; set; }
    public double Revenue { get; set; }
}
