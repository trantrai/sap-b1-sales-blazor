# API PAYLOAD CHI TIẾT
## Website quản lý Sales Order SAP B1 HANA — Backend .NET 9 API
**Phiên bản:** **2.0 — 11/10/2026** (cập nhật theo code thực tế) | **Base URL:** `https://<host>/api` | **Auth:** `Authorization: Bearer <JWT>`

Chuẩn chung (thực tế):
- Response thành công: JSON, `Content-Type: application/json` (snake_case).
- Response lỗi: `{ "detail": "<thông điệp tiếng Việt>" }`, kèm HTTP status tương ứng.
- Phân trang list đơn: `top` (số bản ghi), `skip` (bỏ qua). Response: `{ "items": [...], "total": n }`.

> **Lịch sử:** v1.0 — thiết kế ban đầu. v2.0 — khớp implementation: login kèm
> `company_db`, endpoint đa database, chiết khấu + ngưỡng, đường dẫn `/api/masterdata/*`.

---

## 1. Xác thực

### GET /api/auth/databases (public)
Danh sách database cho màn Login.

**Response 200**
```json
{
  "databases": [
    { "code": "SBODEMO_VN", "name": "Demo VN" },
    { "code": "TESTDB", "name": "Test nội bộ" }
  ],
  "default": "SBODEMO_VN"
}
```

### POST /api/auth/login
Đăng nhập, trả JWT (claims: `sub`, `role`, `b1_db`).

**Request**
```json
{
  "username": "sales01",
  "password": "••••••••",
  "company_db": "SBODEMO_VN"
}
```
`company_db` rỗng = dùng DB mặc định hệ thống; DB không thuộc danh sách khai báo → `400`.

**Response 200**
```json
{ "access_token": "eyJhbGciOiJIUzI1NiIs...", "token_type": "bearer" }
```

**Lỗi:** `401` — `{ "detail": "Sai tên đăng nhập hoặc mật khẩu" }`;
`400` — `{ "detail": "Database 'NODB' chưa được khai báo trong hệ thống." }`

### GET /api/auth/me
**Response 200**
```json
{
  "username": "sales01", "full_name": "Nguyễn Văn A", "role": "sales",
  "sales_person_code": 1, "active": true, "company_db": "SBODEMO_VN"
}
```

---

## 2. Sales Order

### GET /api/orders — Danh sách đơn hàng
**Quyền:** sales+

**Query params:** `status` (open/closed/cancelled), `card_code`, `top`, `skip`.

**Response 200**
```json
{
  "items": [
    {
      "doc_entry": 1002, "doc_num": 5002,
      "card_code": "C20000", "card_name": "Công ty TNHH An Phát",
      "doc_date": "2026-10-11", "doc_total": 106875000.0, "status": "open"
    }
  ],
  "total": 1
}
```

### GET /api/orders/{docEntry} — Chi tiết đơn
**Quyền:** sales+ (kèm header `ETag` để sửa an toàn)

**Response 200**
```json
{
  "doc_entry": 1002, "doc_num": 5002,
  "card_code": "C20000", "card_name": "Công ty TNHH An Phát",
  "doc_date": "2026-10-11", "doc_due_date": "2026-10-11",
  "discount_percent": 5.0,
  "doc_total": 106875000.0, "status": "open", "comments": "",
  "lines": [
    {
      "line_num": 0, "item_code": "A00001", "item_name": "Máy in Laser X100",
      "quantity": 5.0, "price": 25000000.0, "discount_percent": 10.0,
      "warehouse": "01", "line_total": 112500000.0
    }
  ]
}
```

**Lỗi:** `404` — `{ "detail": "Không tìm thấy đơn hàng." }`

### POST /api/orders — Tạo đơn hàng (idempotent)
**Quyền:** sales+

**Request**
```json
{
  "external_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "card_code": "C20000",
  "doc_date": "2026-10-11",
  "doc_due_date": "2026-10-18",
  "comments": "Giao hàng giờ hành chính",
  "discount_percent": 5.0,
  "lines": [
    { "item_code": "A00001", "quantity": 5, "price": 25000000.0, "discount_percent": 10.0, "warehouse": "01" }
  ]
}
```

**Response 201** — trả về đơn vừa tạo (cấu trúc như GET chi tiết).

**Idempotency:** nếu `external_id` đã tồn tại → trả đơn đã tạo trước đó, không gọi lại B1.

**Validation chiết khấu:**
- CK dòng/header phải 0–100 → ngược lại `400 { "detail": "Chiết khấu phải nằm trong khoảng 0–100%." }`
- Ngưỡng theo vai trò (Settings; mặc định sales ≤ 10%, manager ≤ 20%, admin không giới hạn)
  → vượt ngưỡng `403 { "detail": "Vai trò 'sales' chỉ được áp chiết khấu tối đa 10%…" }`

**Payload gửi sang B1 Service Layer** (`POST /b1s/v2/Orders`):
```json
{
  "CardCode": "C20000",
  "DocDate": "2026-10-11",
  "DocDueDate": "2026-10-18",
  "Comments": "Giao hàng giờ hành chính",
  "DiscountPercent": 5.0,
  "DocumentLines": [
    { "ItemCode": "A00001", "Quantity": 5, "Price": 25000000.0, "DiscountPercent": 10.0, "WarehouseCode": "01" }
  ]
}
```

### PUT /api/orders/{docEntry} — Sửa đơn hàng
**Quyền:** sales+. Áp dụng cùng validation chiết khấu như khi tạo.

### POST /api/orders/{docEntry}/cancel — Hủy đơn
**Quyền:** manager+. Tương ứng B1: `POST /b1s/v2/Orders(n)/Cancel`.

### POST /api/orders/{docEntry}/close — Đóng dòng
**Quyền:** manager+. Body `{ "line_nums": [...] }`. Tương ứng B1: `POST /b1s/v2/Orders(n)/Close`.

---

## 3. Master data (`/api/masterdata`)

| Endpoint | Mô tả |
|---|---|
| GET `/api/masterdata/customers?q=` | Autocomplete KH (search server, top 20) |
| GET `/api/masterdata/customers/all` | Toàn bộ KH (tối đa 5000) — cho cache client (v2.1) |
| GET `/api/masterdata/items?q=` | Autocomplete hàng hóa (search server, top 20) |
| GET `/api/masterdata/items/all` | Toàn bộ hàng hóa (tối đa 5000) — cho cache client (v2.1) |
| GET `/api/masterdata/warehouses` | Danh sách kho |
| GET `/api/masterdata/tax-codes` | Thuế suất |
| GET `/api/masterdata/payment-terms` | Điều khoản thanh toán |
| GET `/api/masterdata/shipping-types` | Hình thức vận chuyển |
| GET `/api/masterdata/sales-persons` | Nhân viên kinh doanh |
| GET `/api/masterdata/customers/{cardCode}/addresses` | Địa chỉ KH |
| GET `/api/masterdata/customers/{cardCode}/contacts` | Liên hệ KH |

**Quyền:** viewer+.

### GET /api/masterdata/customers/all — Toàn bộ khách hàng (v2.1)

Phục vụ `MasterDataCache` phía client: tải 1 lần, autocomplete lọc trên bộ nhớ.

Response `200`:
```json
[
  { "card_code": "C20000", "card_name": "Công ty TNHH An Phát", "phone": "0901111111",
    "sales_person_code": 1, "price_list_num": 1 }
]
```

### GET /api/masterdata/items/all — Toàn bộ hàng hóa (v2.1)

Response `200`:
```json
[
  { "item_code": "A00001", "item_name": "Bàn làm việc gỗ", "sales_unit": "Cái", "price": 1500000 }
]
```

### POST /api/auth/refresh — Đổi refresh token (v2.2)

Request:
```json
{ "refresh_token": "<refresh_token>" }
```

Response `200` — cặp token mới (token cũ bị thu hồi):
```json
{ "access_token": "<jwt>", "token_type": "bearer",
  "refresh_token": "<new_refresh>", "expires_in": 28800 }
```

Lỗi `401`: `{ "detail": "Refresh token không hợp lệ hoặc đã hết hạn" }`

### POST /api/auth/logout — Thu hồi refresh token (v2.2)

Request: `{ "refresh_token": "<refresh_token>" }` → `200 { "ok": true }`

### GET /api/orders/stats — Thống kê dashboard (v2.2)

Response `200`:
```json
{
  "total_orders": 120, "total_revenue": 1500000000,
  "open_orders": 45, "closed_orders": 70, "cancelled_orders": 5,
  "revenue_by_day": [{ "date": "11/10", "revenue": 50000000, "orders": 3 }],
  "top_customers": [{ "card_code": "C20000", "card_name": "…", "revenue": 200000000, "orders": 12 }],
  "top_items": [{ "item_code": "A00001", "item_name": "…", "quantity": 100, "revenue": 150000000 }]
}
```

### GET /api/orders/export — Xuất Excel (v2.2)

Query: `status`, `card_code` (lọc như danh sách đơn).
Response `200` — file `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
(`donhang-yyyyMMdd-HHmmss.xlsx`), 7 cột: Số đơn, Ngày, Mã KH, Tên KH, Tổng tiền, Trạng thái, Ghi chú.

---

## 4. Đồng bộ & giám sát

### GET /api/sync/status — Trạng thái đồng bộ
**Quyền:** viewer+. **v2.3:** lọc theo `CompanyDB` của user hiện tại (count, last sync, running của đúng DB). Response: `[{ "entity": "customers", "label": "Khách hàng", "count": 1250, "last_finished_at": "...", ... }]`

### POST /api/sync — Trigger đồng bộ
**Quyền:** manager+. **Request:** `{ "entities": ["customers", "items", "tax_codes"] }`
**v2.3:** response thêm `job_id`; job được theo dõi theo tenant.

### GET /api/sync/job/{jobId} — Trạng thái job (v2.3)
**Quyền:** viewer+. Chỉ trả job thuộc `CompanyDB` của user (khác DB → 404).
**Response:** `{ "job_id": "...", "status": "running|done|error", "started_by": "...", "started_at": "...", "finished_at": "...", "total_records": 0, "error": "", "entities": [...] }`

### GET /api/audit — Nhật ký Service Layer
**Quyền:** sales+. **Query:** `operation`, `status`, `q`, `limit`.

| Endpoint | Quyền | Mô tả |
|---|---|---|
| GET `/api/audit/failed-count` | sales+ | Số bản ghi lỗi |
| GET `/api/audit/{id}` | sales+ | Chi tiết 1 bản ghi |
| POST `/api/audit/{id}/retry` | manager+ | Thử lại 1 bản ghi lỗi |
| POST `/api/audit/retry-all` | manager+ | Thử lại tất cả lỗi |

---

## 5. Cài đặt (chỉ admin)

| Endpoint | Mô tả |
|---|---|
| GET `/api/settings/connection` | Cấu hình hiện tại (mật khẩu → `"********"`, kèm `from_db`) |
| PUT `/api/settings/connection` | Lưu cấu hình; server xóa pool và build lại client runtime |
| POST `/api/settings/connection/test` | Test kết nối B1 (không lưu) → `{ "ok", "message" }` |
| GET `/api/settings/discount-limits` | `{ "sales": 10, "manager": 20 }` |
| PUT `/api/settings/discount-limits` | Lưu ngưỡng CK; validate 0–100 và `sales ≤ manager` |
| GET `/api/settings/databases` | `{ "databases": [{ "code", "name" }], "default": "SBODEMO_VN" }` |
| PUT `/api/settings/databases` | Lưu danh sách DB; validate ≥1 DB, mã không trùng, DB mặc định phải trong danh sách |

---
*Tài liệu v2.0 khớp với implementation tại commit hiện tại của `trantrai/sap-b1-sales-blazor`.*
