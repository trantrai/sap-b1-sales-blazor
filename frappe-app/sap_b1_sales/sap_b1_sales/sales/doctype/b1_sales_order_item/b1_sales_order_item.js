// Client script — tự tính thành tiền khi đổi số lượng/đơn giá/chiết khấu
frappe.ui.form.on("B1 Sales Order Item", {
    quantity(frm, cdt, cdn) { calc_line_total(cdt, cdn); },
    price(frm, cdt, cdn) { calc_line_total(cdt, cdn); },
    discount_percent(frm, cdt, cdn) { calc_line_total(cdt, cdn); },
});

function calc_line_total(cdt, cdn) {
    const row = locals[cdt][cdn];
    const qty = row.quantity || 0;
    const price = row.price || 0;
    const disc = row.discount_percent || 0;
    frappe.model.set_value(cdt, cdn, "line_total", qty * price * (1 - disc / 100));
}
