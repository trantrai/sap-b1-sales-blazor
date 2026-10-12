"""App hooks — điểm tích hợp của app với Frappe Framework."""

from . import __version__ as app_version

app_name = "sap_b1_sales"
app_title = "SAP B1 Sales"
app_publisher = "Trần Văn Trải"
app_description = "Quản lý Sales Order tích hợp SAP Business One Service Layer"
app_email = ""
app_license = "MIT"

# Fixtures: export kèm app (mẫu — bật khi cần)
# fixtures = [
#     {"dt": "Custom Field", "filters": [["module", "=", "SAP B1 Sales"]]},
# ]

# DocType events
doc_events = {
    "B1 Sales Order": {
        # "on_submit": "sap_b1_sales.sales.doctype.b1_sales_order.b1_sales_order.push_to_b1",
    }
}

# Scheduler — chạy định kỳ (mặc định tắt, bật khi cần)
# scheduler_events = {
#     "hourly": [
#         "sap_b1_sales.b1_integration.api.sync.enqueue_master_sync",
#     ],
# }

# Website / Desk
app_include_js = "/assets/sap_b1_sales/js/sap_b1_sales.js"
app_include_css = "/assets/sap_b1_sales/css/sap_b1_sales.css"

# Whitelisted API đã expose qua @frappe.whitelist() trong sap_b1_sales.b1_integration.api.*
# Endpoint mẫu: /api/method/sap_b1_sales.b1_integration.api.orders.list_orders
