// Client script — cảnh báo khi chiết khấu vượt ngưỡng cho phép
frappe.ui.form.on("B1 Sales Order", {
    refresh(frm) {
        if (frm.doc.doc_entry) {
            frm.add_custom_button("Xem trên B1", () => {
                frappe.msgprint(`DocEntry: ${frm.doc.doc_entry} — DocNum: ${frm.doc.doc_num}`);
            });
        }
    },
    discount_percent(frm) {
        const limit = 20; // lấy từ B1 Connection Settings (discount_manager)
        if ((frm.doc.discount_percent || 0) > limit) {
            frappe.msgprint({
                title: "Cảnh báo",
                message: `Chiết khấu ${frm.doc.discount_percent}% vượt ngưỡng ${limit}% cho vai trò của bạn.`,
                indicator: "orange",
            });
        }
    },
});
