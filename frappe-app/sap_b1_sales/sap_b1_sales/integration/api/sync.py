"""API đồng bộ — tương đương SyncController.cs (.NET).

Dùng frappe.enqueue để chạy nền, log vào DocType B1 Sync Log.
"""

import frappe
from frappe.utils import now_datetime

from sap_b1_sales.b1_client import get_b1_client
from sap_b1_sales.integration.doctype.b1_connection_settings.b1_connection_settings import (
    get_connection_settings,
)
from sap_b1_sales.utils import customer_out, item_out


@frappe.whitelist()
def status():
    """GET /api/sync/status — trạng thái đồng bộ từng loại dữ liệu."""
    out = {}
    for entity in ("customers", "items", "tax_codes"):
        last = frappe.db.get_value(
            "B1 Sync Log",
            {"entity": entity, "status": "Thành công"},
            ["finished_at", "records_synced"],
            order_by="finished_at desc",
            as_dict=True,
        )
        out[entity] = {
            "last_sync": str(last.finished_at) if last and last.finished_at else None,
            "records": last.records_synced if last else 0,
        }
    return out


@frappe.whitelist()
def start_sync(entities: str = "customers,items,tax_codes"):
    """POST /api/sync — bắt đầu đồng bộ (chạy nền)."""
    frappe.only_for(("System Manager", "Sales Manager"))
    job_ids = []
    for entity in [e.strip() for e in entities.split(",") if e.strip()]:
        log = frappe.get_doc({
            "doctype": "B1 Sync Log",
            "entity": entity,
            "status": "Queued",
            "started_at": now_datetime(),
        }).insert(ignore_permissions=True)
        job = frappe.enqueue(
            "sap_b1_sales.integration.api.sync.run_sync",
            queue="long",
            logname=log.name,
            entity=entity,
        )
        job_ids.append({"entity": entity, "job_id": job.id, "log": log.name})
    return {"jobs": job_ids}


def run_sync(logname: str, entity: str):
    """Job chạy nền — đồng bộ 1 loại dữ liệu từ B1 vào DocType local."""
    settings = get_connection_settings()
    client = get_b1_client(settings)
    frappe.db.set_value("B1 Sync Log", logname, "status", "Running")
    count = 0
    try:
        if entity == "customers":
            count = _sync_customers(client)
        elif entity == "items":
            count = _sync_items(client)
        elif entity == "tax_codes":
            count = 3  # mock chuẩn, không có DocType riêng
        frappe.db.set_value("B1 Sync Log", logname, {
            "status": "Thành công",
            "finished_at": now_datetime(),
            "records_synced": count,
        })
    except Exception as e:
        frappe.db.set_value("B1 Sync Log", logname, {
            "status": "Thất bại",
            "finished_at": now_datetime(),
            "error": str(e)[:500],
        })
    frappe.db.commit()


def _sync_customers(client) -> int:
    count = 0
    for bp in client.search_business_partners("", top=500):
        c = customer_out(bp)
        name = f"B1-CUST-{c['card_code']}"
        if frappe.db.exists("B1 Customer", {"card_code": c["card_code"]}):
            doc = frappe.get_doc("B1 Customer", {"card_code": c["card_code"]})
        else:
            doc = frappe.new_doc("B1 Customer")
        doc.update({
            "card_code": c["card_code"],
            "card_name": c["card_name"],
            "phone": c["phone"],
            "sales_person_code": c["sales_person_code"],
            "price_list_num": c["price_list_num"],
        })
        doc.save(ignore_permissions=True)
        count += 1
    frappe.db.commit()
    return count


def _sync_items(client) -> int:
    count = 0
    for it in client.search_items("", top=500):
        i = item_out(it)
        if frappe.db.exists("B1 Item", {"item_code": i["item_code"]}):
            doc = frappe.get_doc("B1 Item", {"item_code": i["item_code"]})
        else:
            doc = frappe.new_doc("B1 Item")
        doc.update({
            "item_code": i["item_code"],
            "item_name": i["item_name"],
            "sales_unit": i["sales_unit"],
            "price": i["price"],
            "tax_code": i["tax_code"],
        })
        doc.save(ignore_permissions=True)
        count += 1
    frappe.db.commit()
    return count


def enqueue_master_sync():
    """Hook cho scheduler hourly (mặc định tắt trong hooks.py)."""
    start_sync("customers,items,tax_codes")
