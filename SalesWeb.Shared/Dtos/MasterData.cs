namespace SalesWeb.Shared.Dtos;

public class CustomerOut
{
    public string CardCode { get; set; } = "";
    public string CardName { get; set; } = "";
    public string Phone { get; set; } = "";
    public int? SalesPersonCode { get; set; }
    public int? PriceListNum { get; set; }
}

public class ItemOut
{
    public string ItemCode { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string SalesUnit { get; set; } = "";
    public double Price { get; set; }
    public string? TaxCode { get; set; }
}

public class WarehouseOut
{
    public string WarehouseCode { get; set; } = "";
    public string WarehouseName { get; set; } = "";
}

public class PaymentTermOut
{
    public int GroupNumber { get; set; }
    public string Name { get; set; } = "";
}

public class ShippingTypeOut
{
    public int Code { get; set; }
    public string Name { get; set; } = "";
}

public class SalesPersonOut
{
    public int Code { get; set; }
    public string Name { get; set; } = "";
}

public class BpAddressOut
{
    public string AddressName { get; set; } = "";
    public string AddressType { get; set; } = "";
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
}

public class BpContactOut
{
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string Email { get; set; } = "";
}

public class TaxCodeOut
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public double Rate { get; set; }
}
