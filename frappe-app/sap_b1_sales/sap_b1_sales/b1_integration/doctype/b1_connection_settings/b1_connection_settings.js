// Client script B1 Connection Settings — nút kiểm tra kết nối
frappe.ui.form.on("B1 Connection Settings", {
    refresh(frm) {
        frm.add_custom_button("Kiểm tra kết nối", () => {
            frappe.call({
                method: "sap_b1_sales.integration.api.settings.test_connection",
                callback(r) {
                    const ok = r.message && r.message.ok;
                    frappe.msgprint({
                        title: ok ? "Thành công" : "Thất bại",
                        message: (r.message && r.message.message) || "",
                        indicator: ok ? "green" : "red",
                    });
                },
            });
        });
    },
});
