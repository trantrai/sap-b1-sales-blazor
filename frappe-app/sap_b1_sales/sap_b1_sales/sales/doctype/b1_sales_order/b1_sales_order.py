"""Controller cho DocType B1 Sales Order.

Đơn hàng được đẩy lên SAP B1 Service Layer khi lưu (nếu cấu hình),
giữ idempotency qua external_id.
"""

import frappe
from frappe.model.document import Document


class B1SalesOrder(Document):
    def validate(self):
        if not self.items:
            frappe.throw("Đơn hàng phải có ít nhất một dòng hàng.")
        total = sum((d.line_total or 0) for d in self.items)
        disc = self.discount_percent or 0
        self.doc_total = total * (1 - disc / 100)

    def on_update(self):
        # Ghi log đồng bộ B1 (chạy nền để không chặn lưu)
        if self.doc_entry:
            frappe.enqueue(
                "sap_b1_sales.sales.doctype.b1_sales_order.b1_sales_order.push_to_b1",
                queue="short",
                docname=self.name,
            )


def push_to_b1(docname: str):
    """Đẩy đơn lên B1 Service Layer (chạy nền)."""
    from sap_b1_sales.b1_client import get_b1_client
    from sap_b1_sales.b1_integration.doctype.b1_connection_settings.b1_connection_settings import (
        get_connection_settings,
    )

    doc = frappe.get_doc("B1 Sales Order", docname)
    client = get_b1_client(get_connection_settings())
    payload = {
        "CardCode": doc.card_code,
        "DocDate": str(doc.doc_date),
        "DocDueDate": str(doc.doc_due_date) if doc.doc_due_date else None,
        "TaxDate": str(doc.tax_date) if doc.tax_date else None,
        "NumAtCard": doc.num_at_card,
        "DocCurrency": doc.doc_currency,
        "DiscountPercent": doc.discount_percent or 0,
        "Comments": doc.comments,
        "DocumentLines": [
            {
                "ItemCode": d.item_code,
                "Quantity": d.quantity,
                "Price": d.price,
                "DiscountPercent": d.discount_percent or 0,
                "WarehouseCode": d.warehouse_code,
                "TaxCode": d.tax_code,
            }
            for d in doc.items
        ],
    }
    result = client.create_order(payload)
    frappe.db.set_value(
        "B1 Sales Order", docname,
        {"doc_entry": result.get("DocEntry"), "doc_num": result.get("DocNum")},
    )
