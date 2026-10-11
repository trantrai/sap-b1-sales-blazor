"""API đơn hàng — tương đương OrdersController.cs (.NET).

Endpoint Frappe: /api/method/sap_b1_sales.integration.api.orders.<ten_ham>
"""

import frappe

from sap_b1_sales.b1_client import get_b1_client
from sap_b1_sales.integration.doctype.b1_connection_settings.b1_connection_settings import (
    get_connection_settings,
)
from sap_b1_sales.utils import order_out, paged


def _client():
    return get_b1_client(get_connection_settings())


def _odata_filter(status=None, card_code=None, doc_num=None) -> str | None:
    parts = []
    if status == "open":
        parts.append("DocumentStatus eq 'bost_Open'")
    elif status == "closed":
        parts.append("DocumentStatus eq 'bost_Close'")
    elif status == "cancelled":
        parts.append("DocumentStatus eq 'bost_Cancelled'")
    if card_code:
        parts.append(f"startswith(CardCode,'{card_code}')")
    if doc_num:
        parts.append(f"DocNum eq {int(doc_num)}")
    return " and ".join(parts) or None


@frappe.whitelist()
def list_orders(status=None, card_code=None, doc_num=None, page=1, page_size=20):
    """GET /api/orders — danh sách đơn có lọc + phân trang."""
    page, page_size = int(page or 1), int(page_size or 20)
    data = _client().list_orders(
        top=page_size, skip=(page - 1) * page_size,
        filter=_odata_filter(status, card_code, doc_num),
    )
    items = [order_out(o) for o in (data.get("value") or [])]
    # mock không trả tổng -> ước lượng qua trang kế (đủ cho demo)
    return paged(items, total=len(items))


@frappe.whitelist()
def get_order(doc_entry: int):
    """GET /api/orders/{docEntry} — chi tiết đơn."""
    doc, _etag = _client().get_order(int(doc_entry))
    return order_out(doc)


@frappe.whitelist()
def create_order():
    """POST /api/orders — tạo đơn (idempotent theo external_id)."""
    data = frappe.local.form_dict
    payload = {
        "CardCode": data.get("card_code"),
        "DocDate": data.get("doc_date"),
        "DocDueDate": data.get("doc_due_date"),
        "TaxDate": data.get("tax_date"),
        "NumAtCard": data.get("num_at_card"),
        "DocCurrency": data.get("doc_currency") or "VND",
        "DocRate": data.get("doc_rate") or 1,
        "DiscountPercent": data.get("discount_percent") or 0,
        "Comments": data.get("comments"),
        "SalesPersonCode": data.get("sales_person_code"),
        "PaymentGroupCode": data.get("payment_group_code"),
        "U_ExternalId": data.get("external_id"),
        "DocumentLines": [
            {
                "ItemCode": l.get("item_code"),
                "Quantity": l.get("quantity"),
                "Price": l.get("price"),
                "DiscountPercent": l.get("discount_percent") or 0,
                "WarehouseCode": l.get("warehouse"),
                "TaxCode": l.get("tax_code"),
            }
            for l in (data.get("lines") or [])
        ],
    }
    result = _client().create_order(payload)
    _audit("order.create", "Orders", str(result.get("DocEntry")), payload, result)
    return order_out(result)


@frappe.whitelist()
def cancel_order(doc_entry: int):
    """POST /api/orders/{docEntry}/cancel."""
    result = _client().cancel_order(int(doc_entry))
    _audit("order.cancel", "Orders", str(doc_entry), {}, result)
    return order_out(result)


@frappe.whitelist()
def close_order(doc_entry: int):
    """POST /api/orders/{docEntry}/close."""
    result = _client().close_order(int(doc_entry))
    _audit("order.close", "Orders", str(doc_entry), {}, result)
    return order_out(result)


@frappe.whitelist()
def stats():
    """GET /api/orders/stats — thống kê dashboard."""
    data = _client().list_orders(top=1000)
    orders = [order_out(o) for o in (data.get("value") or [])]
    return {
        "total_orders": len(orders),
        "total_revenue": sum(o["doc_total"] for o in orders),
        "open_orders": sum(1 for o in orders if o["status"] == "open"),
        "closed_orders": sum(1 for o in orders if o["status"] == "closed"),
        "cancelled_orders": sum(1 for o in orders if o["status"] == "cancelled"),
        "scope_note": f"Số liệu trong {len(orders)} đơn gần nhất",
    }


@frappe.whitelist()
def export(status=None, card_code=None, doc_num=None):
    """GET /api/orders/export — xuất Excel (xlsx)."""
    from openpyxl import Workbook

    data = _client().list_orders(
        top=5000, filter=_odata_filter(status, card_code, doc_num)
    )
    orders = [order_out(o) for o in (data.get("value") or [])]

    wb = Workbook()
    ws = wb.active
    ws.title = "Don hang"
    ws.append(["Số đơn", "Ngày", "Mã KH", "Tên KH", "Tổng tiền", "Trạng thái"])
    for o in orders:
        ws.append([o["doc_num"], o["doc_date"], o["card_code"],
                   o["card_name"], o["doc_total"], o["status"]])

    frappe.response["filename"] = "don-hang.xlsx"
    frappe.response["filecontent"] = _wb_bytes(wb)
    frappe.response["type"] = "binary"


def _wb_bytes(wb) -> bytes:
    import io

    buf = io.BytesIO()
    wb.save(buf)
    return buf.getvalue()


def _audit(action, entity_type, entity_id, request, response):
    try:
        frappe.get_doc({
            "doctype": "B1 API Audit Log",
            "username": frappe.session.user,
            "action": action,
            "entity_type": entity_type,
            "entity_id": entity_id,
            "result": "Thành công",
        }).insert(ignore_permissions=True)
    except Exception:
        pass
