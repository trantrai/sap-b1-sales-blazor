"""API master data — tương đương MasterDataController.cs (.NET)."""

import frappe

from sap_b1_sales.b1_client import get_b1_client
from sap_b1_sales.integration.doctype.b1_connection_settings.b1_connection_settings import (
    get_connection_settings,
)
from sap_b1_sales.utils import customer_out, item_out


def _client():
    return get_b1_client(get_connection_settings())


@frappe.whitelist()
def search_customers(search: str = "", top: int = 20):
    """GET /api/masterdata/customers?search=..."""
    return [customer_out(b) for b in _client().search_business_partners(search, int(top))]


@frappe.whitelist()
def search_items(search: str = "", top: int = 20):
    """GET /api/masterdata/items?search=..."""
    return [item_out(i) for i in _client().search_items(search, int(top))]


@frappe.whitelist()
def list_warehouses():
    """GET /api/masterdata/warehouses"""
    return [
        {"warehouse_code": w.get("WarehouseCode", ""),
         "warehouse_name": w.get("WarehouseName", "")}
        for w in _client().list_warehouses()
    ]


@frappe.whitelist()
def list_payment_terms():
    """GET /api/masterdata/payment-terms (mock chuẩn)."""
    return [
        {"group_number": -1, "name": "Thanh toán ngay"},
        {"group_number": 1, "name": "Công nợ 30 ngày"},
        {"group_number": 2, "name": "Công nợ 60 ngày"},
    ]


@frappe.whitelist()
def list_shipping_types():
    """GET /api/masterdata/shipping-types (mock chuẩn)."""
    return [
        {"code": 1, "name": "Giao hàng tiêu chuẩn"},
        {"code": 2, "name": "Giao hàng nhanh"},
    ]


@frappe.whitelist()
def list_sales_persons():
    """GET /api/masterdata/sales-persons (mock chuẩn)."""
    return [{"code": 1, "name": "Nhân viên KD 01"}]


@frappe.whitelist()
def list_tax_codes():
    """GET /api/masterdata/tax-codes (mock chuẩn)."""
    return [
        {"code": "V10", "name": "VAT 10%", "rate": 10},
        {"code": "V08", "name": "VAT 8%", "rate": 8},
        {"code": "V00", "name": "Không thuế", "rate": 0},
    ]
