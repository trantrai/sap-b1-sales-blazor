# Hướng dẫn cài đặt & chạy — SAP B1 Sales (Frappe Framework)

> Branch thử nghiệm song song: `frappe-framework`
> Bản chính vẫn ở `main` (.NET 9 API + React + Tailwind), branch này **không ảnh hưởng** bản chính.

App Frappe `sap_b1_sales` tái hiện backend quản lý Sales Order tích hợp SAP B1
trên [Frappe Framework](https://frappeframework.com) (Python + MariaDB/PostgreSQL).

---

## 1. Yêu cầu hệ thống

| Thành phần | Phiên bản gợi ý |
|---|---|
| Python | 3.10+ |
| Node.js | 18+ (cho bench build assets) |
| MariaDB 10.6+ **hoặc** PostgreSQL 14+ | |
| Redis | 6+ |
| Git | |

---

## 2. Cài đặt Frappe Bench

```bash
# Cài bench CLI
pip install frappe-bench

# Khởi tạo bench (tải frappe framework)
bench init frappe-bench --frappe-branch version-15
cd frappe-bench
```

Tạo site mới:

```bash
bench new-site site1.local --db-root-password <mat-khau-root-db>
# Khi hỏi, đặt mật khẩu Administrator cho site
```

---

## 3. Cài app sap_b1_sales

Từ thư mục `frappe-bench`:

```bash
# Cách A: từ đường dẫn local (branch frappe-framework đã checkout)
bench get-app sap_b1_sales /home/<user>/workspace/sap-b1-sales-blazor/frappe-app/sap_b1_sales

# Cách B: từ git URL + branch
bench get-app sap_b1_sales https://github.com/trantrai/sap-b1-sales-blazor --branch frappe-framework
# (sau đó chép thư mục frappe-app/sap_b1_sales vào apps/ nếu get-app không tự nhận diện)

bench --site site1.local install-app sap_b1_sales
bench --site site1.local migrate
```

> **Lưu ý:** repo chứa cả code .NET/React ở root; app Frappe nằm trong
> `frappe-app/sap_b1_sales/`. Khi dùng `get-app` với URL git, hãy trỏ thẳng
> vào thư mục app như Cách A.

---

## 4. Chạy thử

```bash
# Terminal 1: chạy bench (web + worker + redis + socketio)
bench start

# Mở trình duyệt: http://site1.local:8000  (hoặc http://localhost:8000)
# Đăng nhập: Administrator / <mật khẩu đã đặt>
```

Sau khi đăng nhập, vào **B1 Connection Settings** (module *B1 Integration*):

- Mặc định `Demo (dữ liệu giả)` = **bật** → dùng ngay không cần SAP B1 thật.
- Khai báo thêm CompanyDB trong bảng *Databases* nếu cần.
- Nhấn **Kiểm tra kết nối** để test.

Các DocType chính (module *Sales* / *B1 Integration*):

| DocType | Mô tả |
|---|---|
| B1 Sales Order | Đơn hàng (+ child table B1 Sales Order Item) |
| B1 Customer | Khách hàng (đồng bộ từ OCRD) |
| B1 Item | Mặt hàng (đồng bộ từ OITM) |
| B1 Connection Settings | Cấu hình Service Layer (Single) |
| B1 Sync Log | Lịch sử đồng bộ master data |
| B1 API Audit Log | Nhật ký gọi Service Layer |

---

## 5. REST API cho frontend React

Các hàm `@frappe.whitelist()` expose tại:

```
/api/method/sap_b1_sales.integration.api.<module>.<ten_ham>
```

Bảng map với API .NET cũ (`/api/...`):

| .NET cũ | Frappe mới |
|---|---|
| `POST /api/auth/login` | `POST /api/method/sap_b1_sales.integration.api.auth.login` |
| `GET /api/auth/databases` | `GET /api/method/sap_b1_sales.integration.api.auth.get_databases` |
| `GET /api/orders?...` | `GET /api/method/sap_b1_sales.integration.api.orders.list_orders` |
| `GET /api/orders/stats` | `GET /api/method/sap_b1_sales.integration.api.orders.stats` |
| `GET /api/orders/export` | `GET /api/method/sap_b1_sales.integration.api.orders.export` |
| `GET /api/orders/{id}` | `GET /api/method/sap_b1_sales.integration.api.orders.get_order` |
| `POST /api/orders` | `POST /api/method/sap_b1_sales.integration.api.orders.create_order` |
| `POST /api/orders/{id}/cancel` | `POST /api/method/sap_b1_sales.integration.api.orders.cancel_order` |
| `POST /api/orders/{id}/close` | `POST /api/method/sap_b1_sales.integration.api.orders.close_order` |
| `GET /api/masterdata/customers` | `GET /api/method/sap_b1_sales.integration.api.master_data.search_customers` |
| `GET /api/masterdata/items` | `GET /api/method/sap_b1_sales.integration.api.master_data.search_items` |
| `GET /api/masterdata/warehouses` | `GET /api/method/sap_b1_sales.integration.api.master_data.list_warehouses` |
| `GET /api/sync/status` | `GET /api/method/sap_b1_sales.integration.api.sync.status` |
| `POST /api/sync` | `POST /api/method/sap_b1_sales.integration.api.sync.start_sync` |
| `GET /api/audit` | `GET /api/method/sap_b1_sales.integration.api.audit.list_logs` |
| `GET /api/settings/connection` | `GET /api/method/sap_b1_sales.integration.api.settings.get_connection` |
| `PUT /api/settings/connection` | `POST /api/method/sap_b1_sales.integration.api.settings.save_connection` |

### Xác thực từ React

Frappe dùng **token** thay vì JWT Bearer:

1. Gọi `auth.login` → nhận `access_token = "api_key:api_secret"`.
2. Các request tiếp theo gửi header:
   `Authorization: token api_key:api_secret`
3. Sửa `frontend/src/api/client.js` (2 dòng):
   ```js
   // thay: config.headers.Authorization = `Bearer ${token}`;
   config.headers.Authorization = `token ${token}`;
   ```
4. `VITE_API_BASE` trỏ tới site Frappe, ví dụ `http://site1.local:8000`.

> Ngoài ra Frappe còn có REST chuẩn `/api/resource/<DocType>` cho CRUD trực tiếp
> các DocType (B1 Sales Order, B1 Customer, B1 Item...).

---

## 6. Chạy test

```bash
bench --site site1.local run-tests --app sap_b1_sales
```

---

## 7. Cấu trúc thư mục

```
frappe-app/
├── FRAPPE_SETUP.md          # file này
└── sap_b1_sales/            # app Frappe
    ├── pyproject.toml
    ├── requirements.txt
    ├── README.md
    └── sap_b1_sales/
        ├── hooks.py         # app hooks
        ├── modules.txt      # Sales, B1 Integration
        ├── b1_client.py     # client Service Layer (thật + mock)
        ├── utils.py         # map payload B1 -> snake_case
        ├── config/desktop.py
        ├── public/js|css/
        ├── sales/doctype/
        │   ├── b1_sales_order/       # đơn hàng (+ .py/.js/test)
        │   ├── b1_sales_order_item/  # dòng hàng (child table)
        │   ├── b1_customer/
        │   └── b1_item/
        └── integration/
            ├── doctype/
            │   ├── b1_connection_settings/  # Single
            │   ├── b1_company_db/           # child table
            │   ├── b1_sync_log/
            │   └── b1_api_audit_log/
            └── api/
                ├── auth.py | orders.py | master_data.py
                └── sync.py | audit.py | settings.py
```

---

## 8. Ghi chú & giới hạn (bản scaffold 0.1.0)

- Chưa có refresh-token như bản .NET (Frappe token không hết hạn; thu hồi bằng
  cách reset API key của user).
- Export Excel dùng `openpyxl` (thay ClosedXML).
- Phân quyền chi tiết (Sales User / Sales Manager) mới ở mức DocType
  permissions cơ bản — cần tinh chỉnh thêm theo ma trận RBAC.
- Chưa cấu hình scheduler hourly (đã để sẵn hook, mặc định tắt).
- Chế độ multi-CompanyDB: chọn DB lúc login được validate với danh sách khai báo;
  client B1 thật dùng CompanyDB mặc định trong settings (cần mở rộng pool theo DB
  như bản .NET nếu dùng nhiều DB song song).
