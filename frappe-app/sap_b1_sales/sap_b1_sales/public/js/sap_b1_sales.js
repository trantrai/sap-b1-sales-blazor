// SAP B1 Sales — custom JS toàn app (nạp qua hooks.py app_include_js)
frappe.provide("sap_b1_sales");

sap_b1_sales.open_b1_dashboard = function () {
    frappe.set_route("List", "B1 Sales Order");
};
