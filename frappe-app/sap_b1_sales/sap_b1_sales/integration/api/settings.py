"""API cài đặt — tương đương SettingsController.cs (.NET).

Đọc/ghi Single DocType B1 Connection Settings.
"""

import frappe

from sap_b1_sales.b1_client import B1Exception, get_b1_client
from sap_b1_sales.integration.doctype.b1_connection_settings.b1_connection_settings import (
    get_connection_settings,
)


@frappe.whitelist()
def get_connection():
    """GET /api/settings/connection."""
    frappe.only_for("System Manager")
    s = get_connection_settings()
    return {
        "b1_mock": s["b1_mock"],
        "b1_service_layer_url": s["b1_service_layer_url"],
        "b1_company_db": s["b1_company_db"],
        "b1_username": s["b1_username"],
        "b1_password": "********" if s["b1_password"] else "",
        "b1_verify_ssl": s["b1_verify_ssl"],
        "b1_timeout": s["b1_timeout"],
        "from_db": True,
    }


@frappe.whitelist()
def save_connection():
    """PUT /api/settings/connection."""
    frappe.only_for("System Manager")
    data = frappe.local.form_dict
    doc = frappe.get_single("B1 Connection Settings")
    doc.b1_mock = int(bool(data.get("b1_mock", 1)))
    doc.b1_service_layer_url = data.get("b1_service_layer_url") or ""
    doc.b1_company_db = data.get("b1_company_db") or ""
    doc.b1_username = data.get("b1_username") or ""
    if data.get("b1_password") and data.get("b1_password") != "********":
        doc.b1_password = data.get("b1_password")
    doc.b1_verify_ssl = int(bool(data.get("b1_verify_ssl", 1)))
    doc.b1_timeout = float(data.get("b1_timeout") or 30)
    doc.save(ignore_permissions=True)
    frappe.db.commit()
    return {"ok": True, "mode": "mock" if doc.b1_mock else "live"}


@frappe.whitelist()
def test_connection():
    """POST /api/settings/connection/test — kiểm tra kết nối B1."""
    frappe.only_for("System Manager")
    data = frappe.local.form_dict
    settings = get_connection_settings()
    # Cho phép test với thông số tạm thời từ form
    for k in ("b1_mock", "b1_service_layer_url", "b1_company_db",
              "b1_username", "b1_password", "b1_verify_ssl", "b1_timeout"):
        if k in data and data[k] not in (None, ""):
            settings[k] = data[k]
    try:
        client = get_b1_client(settings)
        client.login()
        client.logout()
        return {"ok": True, "message": "Kết nối thành công."}
    except B1Exception as e:
        return {"ok": False, "message": str(e)}


@frappe.whitelist()
def get_discount_limits():
    """GET /api/settings/discount-limits."""
    s = get_connection_settings()
    return {"sales": s["discount_sales"], "manager": s["discount_manager"]}


@frappe.whitelist()
def save_discount_limits():
    """PUT /api/settings/discount-limits."""
    frappe.only_for("System Manager")
    data = frappe.local.form_dict
    doc = frappe.get_single("B1 Connection Settings")
    doc.discount_sales = float(data.get("sales") or 10)
    doc.discount_manager = float(data.get("manager") or 20)
    doc.save(ignore_permissions=True)
    frappe.db.commit()
    return {"ok": True}


@frappe.whitelist()
def get_databases():
    """GET /api/settings/databases — danh sách database khai báo."""
    frappe.only_for("System Manager")
    return {"databases": get_connection_settings()["databases"]}


@frappe.whitelist()
def save_databases():
    """PUT /api/settings/databases — lưu danh sách database."""
    frappe.only_for("System Manager")
    data = frappe.local.form_dict
    doc = frappe.get_single("B1 Connection Settings")
    doc.set("databases", [])
    for d in (data.get("databases") or []):
        doc.append("databases", {
            "db_code": d.get("code"), "db_name": d.get("name")})
    doc.save(ignore_permissions=True)
    frappe.db.commit()
    return {"ok": True}
