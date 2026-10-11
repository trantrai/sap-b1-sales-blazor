"""Helpers dùng chung cho API — map payload B1 sang snake_case như DTO .NET."""

from __future__ import annotations

from typing import Any


def _status_of(doc: dict[str, Any]) -> str:
    s = str(doc.get("DocumentStatus") or "")
    return {"bost_Open": "open", "bost_Close": "closed",
            "bost_Cancelled": "cancelled"}.get(s, "open")


def order_line_out(line: dict[str, Any]) -> dict[str, Any]:
    qty = line.get("Quantity") or 0
    price = line.get("Price") or 0
    return {
        "line_num": line.get("LineNum", 0),
        "item_code": line.get("ItemCode", ""),
        "description": line.get("ItemDescription", ""),
        "quantity": qty,
        "price": price,
        "discount_percent": line.get("DiscountPercent") or 0,
        "warehouse": line.get("WarehouseCode", ""),
        "tax_code": line.get("TaxCode", ""),
        "ship_date": str(line.get("ShipDate") or ""),
        "project_code": line.get("ProjectCode", ""),
        "costing_code": line.get("CostingCode", ""),
        "uom_code": line.get("UoMCode", ""),
        "line_status": line.get("LineStatus", ""),
        "line_total": qty * price,
    }


def order_out(doc: dict[str, Any]) -> dict[str, Any]:
    """Map dict B1 thô -> OrderOut (snake_case) như backend .NET."""
    return {
        "doc_entry": doc.get("DocEntry", 0),
        "doc_num": doc.get("DocNum", 0),
        "doc_date": str(doc.get("DocDate") or ""),
        "doc_due_date": str(doc.get("DocDueDate") or ""),
        "tax_date": str(doc.get("TaxDate") or ""),
        "card_code": doc.get("CardCode", ""),
        "card_name": doc.get("CardName", ""),
        "num_at_card": doc.get("NumAtCard", ""),
        "doc_total": doc.get("DocTotal") or 0,
        "currency": doc.get("DocCurrency", ""),
        "doc_rate": doc.get("DocRate") or 1,
        "status": _status_of(doc),
        "comments": doc.get("Comments", ""),
        "sales_person_code": doc.get("SalesPersonCode"),
        "contact_person_code": doc.get("ContactPersonCode"),
        "ship_to_code": doc.get("ShipToCode", ""),
        "pay_to_code": doc.get("PayToCode", ""),
        "payment_group_code": doc.get("PaymentGroupCode"),
        "transportation_code": doc.get("TransportationCode"),
        "project": doc.get("Project", ""),
        "series": doc.get("Series"),
        "discount_percent": doc.get("DiscountPercent") or 0,
        "address": doc.get("Address", ""),
        "external_id": doc.get("U_ExternalId"),
        "lines": [order_line_out(l) for l in (doc.get("DocumentLines") or [])],
    }


def customer_out(bp: dict[str, Any]) -> dict[str, Any]:
    return {
        "card_code": bp.get("CardCode", ""),
        "card_name": bp.get("CardName", ""),
        "phone": bp.get("Phone", ""),
        "sales_person_code": bp.get("SalesPersonCode"),
        "price_list_num": bp.get("PriceListNum"),
    }


def item_out(item: dict[str, Any]) -> dict[str, Any]:
    return {
        "item_code": item.get("ItemCode", ""),
        "item_name": item.get("ItemName", ""),
        "sales_unit": item.get("SalesUnit", ""),
        "price": item.get("Price") or 0,
        "tax_code": item.get("TaxCode"),
    }


def paged(items: list, total: int) -> dict[str, Any]:
    return {"total": total, "items": items}
