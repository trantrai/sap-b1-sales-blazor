"""Single DocType B1 Connection Settings — cấu hình kết nối SAP B1."""

import frappe
from frappe.model.document import Document


class B1ConnectionSettings(Document):
    pass


def get_connection_settings() -> dict:
    """Đọc settings, trả về dict dùng cho b1_client.get_b1_client()."""
    doc = frappe.get_single("B1 Connection Settings")
    return {
        "b1_mock": bool(doc.b1_mock),
        "b1_service_layer_url": doc.b1_service_layer_url or "",
        "b1_company_db": doc.b1_company_db or "",
        "b1_username": doc.b1_username or "",
        "b1_password": doc.get_password("b1_password"),
        "b1_verify_ssl": bool(doc.b1_verify_ssl),
        "b1_timeout": doc.b1_timeout or 30,
        "databases": [
            {"code": d.db_code, "name": d.db_name} for d in (doc.databases or [])
        ],
        "discount_sales": doc.discount_sales or 10,
        "discount_manager": doc.discount_manager or 20,
    }
