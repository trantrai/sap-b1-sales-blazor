# SAP B1 Sales — Frappe App

App quản lý **Sales Order** tích hợp **SAP Business One Service Layer**, xây dựng trên
[Frappe Framework](https://frappeframework.com).

Đây là bản thử nghiệm song song (branch `frappe-framework`) của project
[sap-b1-sales-blazor](https://github.com/trantrai/sap-b1-sales-blazor)
(bản chính: .NET 9 API + React + Tailwind).

## Tính năng

- DocTypes: **B1 Sales Order** (+ dòng hàng), **B1 Customer**, **B1 Item**
- Kết nối SAP B1 Service Layer (chế độ thật + mock), quản lý session tự động
- REST API (`@frappe.whitelist`) tương thích payload với frontend React hiện tại
- Đồng bộ master data (khách hàng / mặt hàng / thuế suất) chạy nền bằng `frappe.enqueue`
- Nhật ký gọi Service Layer + cơ chế thử lại (retry)
- Cài đặt kết nối lưu trong Single DocType **B1 Connection Settings**

## Cài đặt nhanh

Xem chi tiết trong [FRAPPE_SETUP.md](../FRAPPE_SETUP.md).

```bash
bench get-app sap_b1_sales /đường/dẫn/tới/frappe-app/sap_b1_sales
bench --site site1.local install-app sap_b1_sales
bench --site site1.local migrate
```

## License

MIT
