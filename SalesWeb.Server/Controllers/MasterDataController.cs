using Microsoft.AspNetCore.Mvc;
using SalesWeb.Server.Services;
using SalesWeb.Server.Services.B1;
using SalesWeb.Shared.Dtos;

namespace SalesWeb.Server.Controllers;

/// <summary>Tra cứu master data cho autocomplete (KH, hàng hoá, kho...).</summary>
[Route("api/masterdata")]
public sealed class MasterDataController : ApiBaseController
{
    private readonly IB1Client _b1;
    public MasterDataController(IB1Client b1) => _b1 = b1;
    private MasterDataService Svc() => new(_b1);

    [HttpGet("customers")]
    [RequireRole("viewer")]
    public async Task<ActionResult<List<CustomerOut>>> SearchCustomers([FromQuery] string q)
        => Ok(await Svc().SearchCustomersAsync(q));

    [HttpGet("customers/all")]
    [RequireRole("viewer")]
    public async Task<ActionResult<List<CustomerOut>>> ListAllCustomers()
        => Ok(await Svc().ListAllCustomersAsync());

    [HttpGet("items")]
    [RequireRole("viewer")]
    public async Task<ActionResult<List<ItemOut>>> SearchItems([FromQuery] string q)
        => Ok(await Svc().SearchItemsAsync(q));

    [HttpGet("items/all")]
    [RequireRole("viewer")]
    public async Task<ActionResult<List<ItemOut>>> ListAllItems()
        => Ok(await Svc().ListAllItemsAsync());

    [HttpGet("warehouses")]
    [RequireRole("viewer")]
    public async Task<ActionResult<List<WarehouseOut>>> Warehouses()
        => Ok(await Svc().ListWarehousesAsync());

    [HttpGet("payment-terms")]
    [RequireRole("viewer")]
    public async Task<ActionResult<List<PaymentTermOut>>> PaymentTerms()
        => Ok(await Svc().ListPaymentTermsAsync());

    [HttpGet("shipping-types")]
    [RequireRole("viewer")]
    public async Task<ActionResult<List<ShippingTypeOut>>> ShippingTypes()
        => Ok(await Svc().ListShippingTypesAsync());

    [HttpGet("sales-persons")]
    [RequireRole("viewer")]
    public async Task<ActionResult<List<SalesPersonOut>>> SalesPersons()
        => Ok(await Svc().ListSalesPersonsAsync());

    [HttpGet("tax-codes")]
    [RequireRole("viewer")]
    public async Task<ActionResult<List<TaxCodeOut>>> TaxCodes()
        => Ok(await Svc().ListTaxCodesAsync());

    [HttpGet("customers/{cardCode}/addresses")]
    [RequireRole("viewer")]
    public async Task<ActionResult<List<BpAddressOut>>> Addresses(string cardCode)
        => Ok(await Svc().GetBpAddressesAsync(cardCode));

    [HttpGet("customers/{cardCode}/contacts")]
    [RequireRole("viewer")]
    public async Task<ActionResult<List<BpContactOut>>> Contacts(string cardCode)
        => Ok(await Svc().GetBpContactsAsync(cardCode));
}
