"""API audit — tương đương AuditController.cs (.NET).

Đọc từ DocType B1 API Audit Log (thay cho bảng audit SQLite bên .NET).
"""

import frappe


@frappe.whitelist()
def list_logs(page: int = 1, page_size: int = 20, result: str | None = None):
    """GET /api/audit — danh sách log."""
    frappe.only_for(("System Manager", "Sales Manager"))
    filters = {"result": result} if result else {}
    page, page_size = int(page or 1), int(page_size or 20)
    total = frappe.db.count("B1 API Audit Log", filters)
    items = frappe.get_all(
        "B1 API Audit Log",
        filters=filters,
        fields=["name", "timestamp", "username", "action",
                "entity_type", "entity_id", "result", "duration_ms", "error"],
        order_by="timestamp desc",
        start=(page - 1) * page_size,
        page_length=page_size,
    )
    return {"total": total, "items": items}


@frappe.whitelist()
def failed_count():
    """GET /api/audit/failed-count — số log thất bại."""
    frappe.only_for(("System Manager", "Sales Manager"))
    return {"failed": frappe.db.count("B1 API Audit Log", {"result": "Thất bại"})}
