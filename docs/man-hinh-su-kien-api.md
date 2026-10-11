# Tài liệu chi tiết: Màn hình — Sự kiện — API endpoint

Web quản lý đơn hàng bán SAP B1 (`~/workspace/sap-b1-sales-blazor/`).
Kiến trúc: ASP.NET Core API + Blazor WebAssembly hosted, FluentBlazor 5.0.
JSON dùng snake_case. Auth: JWT Bearer (claims `sub`, `role`, `b1_db`), lưu trong `localStorage` (key `sw_token`).

> **Phiên bản 2.0 — 11/10/2026:** cập nhật chọn database lúc đăng nhập, quản lý danh
> sách database, chiết khấu + ngưỡng cấu hình được, drawer mobile.

---

## Quy ước chung

| Mục | Chi tiết |
|---|---|
| Base URL (mock) | `http://localhost:5050` |
| Auth header | `Authorization: Bearer <access_token>` |
| Phân cấp vai trò | viewer < sales < manager < admin |
| Lỗi chuẩn API | `400` dữ liệu sai (kèm `detail`), `401` chưa đăng nhập, `403` thiếu quyền / vượt ngưỡng CK, `404` không tìm thấy, `409` xung đột ETag / trùng `external_id`, `412` sai ETag |

---

## 1. Đăng nhập — `/login` (`Login.razor`)

**Mục đích:** xác thực người dùng + chọn database (CompanyDB) cho phiên làm việc.

**Sự kiện:**

| # | Thành phần / hành động | Xử lý |
|---|---|---|
| 1.1 | Mở trang | Tải danh sách database từ API public, mặc định chọn DB mặc định hệ thống |
| 1.2 | Nhập "Tên đăng nhập" | bind `username` |
| 1.3 | Nhập "Mật khẩu" | bind `password` (kiểu password) |
| 1.4 | Chọn "Cơ sở dữ liệu" | bind `companyDb` (dropdown từ danh sách đã khai báo) |
| 1.5 | Bấm "Đăng nhập" | Gọi API đăng nhập kèm `company_db`; lưu `access_token` vào `localStorage`; chuyển hướng về `/` |

**API:**

| Method | Endpoint | Body | Kết quả |
|---|---|---|---|
| GET | `/api/auth/databases` | — | `{databases:[{code,name}], default}` (public) |
| POST | `/api/auth/login` | `{"username","password","company_db"}` | `200` → `{"access_token","token_type"}`; `401` sai thông tin; `400` DB chưa khai báo |

**Lưu ý:** trang công khai. Sau login, client gọi `GET /api/auth/me` lấy user + vai trò
+ `company_db` đã chọn (dùng cho phân quyền menu và bind mặc định ở Settings).

---

## 2. Danh sách đơn hàng — `/` (`Orders.razor`)

**Mục đích:** tra cứu, lọc, phân trang danh sách Sales Order.

**Sự kiện:**

| # | Thành phần / hành động | Xử lý |
|---|---|---|
| 2.1 | Chọn "Trạng thái" (mở / đã đóng / đã hủy / tất cả) | bind `status` |
| 2.2 | Nhập "Mã khách hàng" | bind `cardCode` |
| 2.3 | Bấm "Lọc" (`ApplyFilter`) | Gọi API danh sách với tham số lọc, reset về trang 1 |
| 2.4 | Bấm "Trước" / "Sau" (`PrevPage` / `NextPage`) | Tăng/giảm `skip`, gọi lại API |
| 2.5 | Bấm vào một dòng đơn | Điều hướng tới `/orders/{DocEntry}` |
| 2.6 | Bấm "Xuất Excel" (v2.2) | `ExportExcel` — tải file `.xlsx` danh sách đơn theo bộ lọc hiện tại |

**API:**

| Method | Endpoint | Tham số | Kết quả |
|---|---|---|---|
| GET | `/api/orders` | `status`, `card_code`, `top`, `skip` | `{"items":[...],"total":n}` — mỗi item có `doc_entry`, `doc_num`, `card_code`, `card_name`, `doc_date`, `doc_total`, `status` |
| GET | `/api/orders/export` | `status`, `card_code` | File Excel `.xlsx` (v2.2) |

**Phân quyền:** yêu cầu vai trò `sales` trở lên.

---

## 3. Tạo đơn hàng — `/orders/new` (`OrderCreate.razor`)

**Mục đích:** lập Sales Order mới, gồm chiết khấu dòng và chiết khấu đơn hàng.

### 3.1. Phần "Khách hàng"

| # | Thành phần / hành động | Xử lý |
|---|---|---|
| 3.1.1 | Gõ ô tìm KH (autocomplete) | `OnCustomerSearch` → `MasterDataCache.SearchCustomersAsync`: **chỉ tìm khi nhập ≥ 2 ký tự**, lọc trên bộ nhớ (tối đa 20 gợi ý), không gọi API mỗi lần gõ |
| 3.1.2 | Chọn một KH | `OnCustomerSelected` — điền `card_code`/`card_name`; tự động tải địa chỉ giao/nhận hàng và liên hệ |
| 3.1.3 | Bấm "X" (`ClearCustomer`) | Xóa KH đã chọn |
| 3.1.4 | Nhập "Ngày giao hàng" | bind `docDueDate` |
| 3.1.5 | Nhập "Chiết khấu đơn hàng (%)" | bind `headerDiscount` (0–100); tổng tiền tính lại ngay trên client |
| 3.1.6 | Nhập "Ghi chú" | bind `comments` |

### 3.2. Phần "Dòng hàng"

| # | Thành phần / hành động | Xử lý |
|---|---|---|
| 3.2.1 | Gõ ô "Hàng hoá" (autocomplete) | `ItemSearch.OnSearch` → `MasterDataCache.SearchItemsAsync`: **chỉ tìm khi nhập ≥ 2 ký tự**, lọc trên bộ nhớ |
| 3.2.2 | Chọn hàng (`OnItemPicked`) | Điền `item_code`, `item_name`, `price` (giá mặc định), `tax_code` |
| 3.2.3 | Sửa "SL" | bind `ln.Quantity` |
| 3.2.4 | Sửa "Đơn giá" | bind `ln.Price` |
| 3.2.5 | Sửa "CK %" | bind `ln.DiscountPercent` (0–100, có `aria-label` cho accessibility) |
| 3.2.6 | Chọn "Kho" | bind `ln.Warehouse` |
| 3.2.7 | Chọn "Ngày giao" | bind `ln.ShipDate` |
| 3.2.8 | Chọn "Thuế suất" | bind `ln.TaxCode` |
| 3.2.9 | Bấm "X" ở dòng | Xóa dòng khỏi danh sách |
| 3.2.10 | Bấm "+ Thêm dòng hàng" (`AddLine`) | Thêm dòng trống mới |

### 3.3. Phần "Thông tin bổ sung" (trường chuẩn SAP B1)

Số PO của KH (`NumAtCard`), ngày hạch toán (`TaxDate`), tiền tệ (`DocCurrency`),
tỷ giá (`DocRate`), điều khoản thanh toán (`PaymentGroupCode`), hình thức vận chuyển
(`TransportationCode`), địa chỉ giao hàng / hóa đơn (`ShipToCode`/`PayToCode`), mã dự án (`Project`).

### 3.4. Tổng tiền & Nút tạo đơn

Breakdown hiển thị theo thời gian thực:

- **Tổng sau CK dòng** = Σ `quantity × price × (1 − line_disc%)`
- **Chiết khấu đơn hàng (x%)** = Tổng sau CK dòng × `header_disc%`
- **TỔNG CỘNG** = Tổng sau CK dòng − CK đơn hàng

| # | Hành động | Xử lý |
|---|---|---|
| 3.4.1 | Bấm "Tạo đơn hàng" (`Submit`) | Validate client (KH bắt buộc, ≥1 dòng có hàng/SL/giá, CK 0–100) → gọi API tạo đơn |

**API:**

| Method | Endpoint | Ghi chú |
|---|---|---|
| GET | `/api/masterdata/customers?q=` | autocomplete KH (search server) |
| GET | `/api/masterdata/customers/all` | toàn bộ KH — nạp 1 lần cho `MasterDataCache` (v2.1) |
| GET | `/api/masterdata/customers/{cardCode}/addresses` | địa chỉ giao/nhận hàng |
| GET | `/api/masterdata/customers/{cardCode}/contacts` | người liên hệ |
| GET | `/api/masterdata/items?q=` | autocomplete hàng hóa (search server) |
| GET | `/api/masterdata/items/all` | toàn bộ hàng hóa — nạp 1 lần cho `MasterDataCache` (v2.1) |
| GET | `/api/masterdata/warehouses` | danh sách kho |
| GET | `/api/masterdata/tax-codes` | danh sách thuế suất |
| GET | `/api/masterdata/payment-terms` | điều khoản thanh toán |
| GET | `/api/masterdata/shipping-types` | hình thức vận chuyển |
| GET | `/api/masterdata/sales-persons` | nhân viên kinh doanh |
| POST | `/api/orders` | tạo đơn. Body gồm `external_id`, `card_code`, `discount_percent` (CK đơn hàng), `lines[]` (mỗi dòng có `discount_percent`) |

**Validation phía server** (`OrdersController.ValidateDiscounts`):

- CK (dòng và đơn hàng) phải trong khoảng 0–100%, ngược lại trả `400`
- Ngưỡng theo vai trò lấy từ Settings (mặc định sales ≤ 10%, manager ≤ 20%, admin không giới hạn); vượt ngưỡng trả `403` kèm thông điệp nêu rõ ngưỡng hiện hành
- Trùng `external_id` → `409`

**Ánh xạ SAP B1:** `discount_percent` (header) → `ORDR.DiscPrcnt`;
`lines[].discount_percent` → `RDR1.DiscPrcnt`.

**Phân quyền:** yêu cầu vai trò `sales` trở lên.

---

## 4. Chi tiết đơn hàng — `/orders/{DocEntry}` (`OrderDetail.razor`)

**Mục đích:** xem chi tiết đơn, hiển thị chiết khấu và breakdown tổng tiền; sửa ghi chú,
hủy đơn, đóng dòng.

**Sự kiện:**

| # | Thành phần / hành động | Xử lý |
|---|---|---|
| 4.1 | Mở trang | Tải chi tiết đơn qua API |
| 4.2 | Bấm "Sửa" ghi chú (chỉ khi đơn còn mở, role `sales`+) | bật chế độ `editing` |
| 4.3 | Bấm "Lưu" (`SaveComments`) | Gọi API cập nhật ghi chú (kèm ETag chống ghi đè) |
| 4.4 | Bấm "Bỏ" | tắt chế độ `editing`, hủy thay đổi |
| 4.5 | Bấm "Hủy đơn" (`CancelOrder`, role `manager`+) | `confirm()` → gọi API hủy |
| 4.6 | Bấm "Đóng dòng" (`CloseOrder`, role `manager`+) | gọi API đóng các dòng còn mở |
| 4.7 | Bấm "Copy thành đơn mới" (v2.2, role `sales`+) | Điều hướng `/orders/new?copyFrom={DocEntry}` — điền sẵn KH, dòng hàng, CK |
| 4.8 | Bấm "In" (v2.2) | `window.print()` — CSS `@media print` chỉ in nội dung đơn |

**Hiển thị chiết khấu:**

- Mỗi dòng: cột **CK %** + thành tiền sau CK dòng
- Header: **Chiết khấu đơn hàng (x%)**
- Breakdown: Tổng sau CK dòng → Chiết khấu đơn hàng → Tổng cộng (`DocTotal`)

**API:**

| Method | Endpoint | Ghi chú |
|---|---|---|
| GET | `/api/orders/{docEntry}` | chi tiết đầy đủ: header (`discount_percent`, `doc_total`, `status`...), `lines[]` (mỗi dòng có `discount_percent`, `line_total`) |
| PUT | `/api/orders/{docEntry}` | cập nhật (ghi chú...). Body có thể kèm `discount_percent` — chịu cùng validation CK như khi tạo |
| POST | `/api/orders/{docEntry}/cancel` | hủy đơn (bound action SAP B1 `Cancel`) |
| POST | `/api/orders/{docEntry}/close` | đóng dòng còn mở, body `{"line_nums":[...]}` (bound action `Close`) |

**Ánh xạ SAP B1:** Sales Order ObjType `17`, bảng `ORDR`/`RDR1`, khóa `DocEntry`.

**Phân quyền:** xem — `sales`+; hủy/đóng dòng — `manager`+.

---

## 5. Đồng bộ dữ liệu — `/sync` (`SyncPage.razor`)

**Mục đích:** nạp master data (KH, hàng hóa, thuế suất...) từ SAP B1 vào SQLite local
để tra cứu nhanh.

**Sự kiện:**

| # | Thành phần / hành động | Xử lý |
|---|---|---|
| 5.1 | Mở trang (`OnInitializedAsync` → `Load`) | Gọi API trạng thái đồng bộ |
| 5.2 | Chọn entity cần đồng bộ (checkbox: customers, items, tax_codes...) | bind danh sách `entities` |
| 5.3 | Bấm "Đồng bộ" (`Start`) | Gọi API trigger sync → nhận `job_id`, poll `GET /api/sync/job/{job_id}` mỗi 2s cho đúng job của tenant; khi done/error thì tải lại status và xoá cache master data |

**API:**

| Method | Endpoint | Body | Kết quả |
|---|---|---|---|
| GET | `/api/sync/status` | — | `[{"entity","last_sync","count",...}]` — **v2.3:** lọc theo CompanyDB |
| POST | `/api/sync` | `{"entities":["customers","items","tax_codes"]}` | `{"started":true,"job_id":"...","message":"..."}` — **v2.3:** thêm job_id |
| GET | `/api/sync/job/{jobId}` | — | `{"job_id","status":"running\|done\|error","total_records","error",...}` — **v2.3** |

**Phân quyền:** yêu cầu vai trò `manager` trở lên.

---

## 6. Giám sát tích hợp — `/audit` (`AuditPage.razor`)

**Mục đích:** xem nhật ký mọi thao tác gọi Service Layer, lọc lỗi và retry.

**Sự kiện:**

| # | Thành phần / hành động | Xử lý |
|---|---|---|
| 6.1 | Chọn "Thao tác" (`op`: tạo/sửa/hủy/đóng/sync...) | bind `op` |
| 6.2 | Chọn "Trạng thái" (`status`: ok/failed...) | bind `status` |
| 6.3 | Nhập từ khóa tìm kiếm | bind `q` |
| 6.4 | Bấm "Lọc" (`Load`) | Gọi API audit với tham số lọc |
| 6.5 | Bấm vào một dòng log | Mở dialog chi tiết (`GetAuditDetailAsync`) |
| 6.6 | Bấm "Thử lại" trong dialog (`RetryFromDialog`, role `manager`+) | Gọi API retry một bản ghi |
| 6.7 | Bấm "Thử lại tất cả lỗi" (`DoRetryAll`, role `manager`+) | Gọi API retry hàng loạt |
| 6.8 | Bấm "Đóng" (`CloseDetail`) | Đóng dialog |

**API:**

| Method | Endpoint | Tham số | Kết quả |
|---|---|---|---|
| GET | `/api/audit` | `operation`, `status`, `q`, `limit` | `{"items":[...],"total":n}` |
| GET | `/api/audit/failed-count` | — | `{"count":n}` (badge cảnh báo) |
| GET | `/api/audit/{id}` | — | chi tiết: request/response payload, lỗi |
| POST | `/api/audit/{id}/retry` | — | thử lại thao tác lỗi |
| POST | `/api/audit/retry-all` | — | thử lại toàn bộ bản ghi lỗi |

**Phân quyền:** xem — `sales`+; retry — `manager`+.

---

## 7. Người dùng & phân quyền — `/users` (`UsersPage.razor`)

**Mục đích:** quản trị tài khoản và xem ma trận phân quyền RBAC.

**Sự kiện:**

| # | Thành phần / hành động | Xử lý |
|---|---|---|
| 7.1 | Mở trang (`OnInitializedAsync` → `Load`) | Tải danh sách user + ma trận quyền |
| 7.2 | Bấm "Thêm người dùng" | `OpenDialog(null)` — mở dialog tạo mới |
| 7.3 | Bấm "Sửa" trên một user | `OpenDialog(user)` — mở dialog sửa |
| 7.4 | Nhập username / họ tên / vai trò / NVKD / mật khẩu trong dialog | bind `dlgUsername`, `dlgFullName`, `dlgRole`, `dlgSlp`, `dlgPassword` |
| 7.5 | Bấm "Lưu" (`SaveDialog`) | Tạo mới hoặc cập nhật user |
| 7.6 | Bấm "Đóng" (`CloseDialog`) | Đóng dialog |
| 7.7 | Bật/tắt "Kích hoạt" (`ToggleActive`) | Khóa/mở tài khoản |

**API:**

| Method | Endpoint | Body | Kết quả |
|---|---|---|---|
| GET | `/api/users` | — | danh sách user |
| GET | `/api/auth/role-matrix` | — | ma trận 10 quyền × 4 vai trò |
| POST | `/api/users` | `{"username","full_name","role","password",...}` | tạo user |
| PUT | `/api/users/{username}` | `{"full_name","role","is_active",...}` | cập nhật user |

**Phân quyền:** chỉ `admin`.

---

## 9. Dashboard — `/dashboard` (`Dashboard.razor`) (v2.2)

**Mục đích:** tổng quan kinh doanh — KPI, biểu đồ doanh số, top KH/hàng hoá.

**Sự kiện:**

| # | Thành phần | Xử lý |
|---|---|---|
| 9.1 | Mở trang | `GetDashboardStatsAsync` — tải 1 lần |
| 9.2 | Thẻ KPI | Tổng đơn, doanh số, đang mở/đã đóng/đã huỷ |
| 9.3 | Biểu đồ cột SVG | Doanh số 30 ngày (tooltip: ngày, doanh số, số đơn) |
| 9.4 | Bảng top 5 | Khách hàng và hàng hoá theo doanh số |

**API:** `GET /api/orders/stats` → `DashboardStats`.

---

## 10. UX chung (v2.2)

| # | Tính năng | Xử lý |
|---|---|---|
| 10.1 | Dark mode (nút mặt trăng, topbar) | `Blazor.theme.setDarkMode`, lưu `localStorage sw_dark` |
| 10.2 | Tìm kiếm toàn cục (topbar) | Gõ ≥2 ký tự → tìm đơn (theo số), KH/hàng (từ `MasterDataCache`); **Ctrl+K** focus ô tìm kiếm |
| 10.3 | Copy đơn hàng | `/orders/new?copyFrom={docEntry}` — điền sẵn toàn bộ |
| 10.4 | In đơn hàng | Nút "In" ở chi tiết đơn + CSS `@media print` |

---

## 8. Cài đặt kết nối — `/settings` (`SettingsPage.razor`)

**Mục đích:** cấu hình kết nối Service Layer, danh sách database truy cập và ngưỡng chiết khấu theo vai trò.

### 8.1. Card "Cài đặt kết nối Service Layer" — thông tin chung (chỉ hiển thị)

### 8.2. Card "Chế độ kết nối"

| # | Hành động | Xử lý |
|---|---|---|
| 8.2.1 | Chọn radio "Demo (dữ liệu giả)" / "SAP B1 thật" | bind `modeMock`; khi chọn demo, các trường Service Layer bị mờ (disabled) |

### 8.3. Card "Thông tin Service Layer"

Các trường: Service Layer URL, **CompanyDB (dropdown từ danh sách đã khai báo —
mặc định bind theo database đã chọn lúc đăng nhập)**, tên đăng nhập kỹ thuật,
mật khẩu (để trống = giữ nguyên, đã mã hóa AES-GCM trong DB), timeout (giây), kiểm tra SSL.

| # | Hành động | Xử lý |
|---|---|---|
| 8.3.1 | Bấm "Kiểm tra kết nối" (`DoTest`) | Gọi API test (không lưu), hiển thị thông điệp thành công/thất bại |
| 8.3.2 | Bấm "Lưu & áp dụng ngay" (`DoSave`) | Nếu chuyển sang B1 thật: `confirm()` → gọi API lưu; server xóa pool và build lại client runtime ngay, không cần restart |

### 8.4. Card "Danh sách database truy cập"

| # | Hành động | Xử lý |
|---|---|---|
| 8.4.1 | Nhập "Mã database mới" + "Tên hiển thị", bấm "+ Thêm database" (`AddDb`) | Validate mã không trống/không trùng → thêm vào danh sách chờ lưu |
| 8.4.2 | Bấm "Xóa" trên một dòng | Xóa khỏi danh sách (không cho xóa khi chỉ còn 1 DB) |
| 8.4.3 | Bấm "Lưu danh sách database" (`DoSaveDbs`) | Gọi API lưu; danh sách mới hiện ngay ở màn Đăng nhập |

### 8.5. Card "Ngưỡng chiết khấu theo vai trò"

| # | Hành động | Xử lý |
|---|---|---|
| 8.5.1 | Nhập "Sales tối đa (%)" | bind `discSales` |
| 8.5.2 | Nhập "Manager tối đa (%)" | bind `discManager` |
| 8.5.3 | Bấm "Lưu ngưỡng chiết khấu" (`DoSaveLimits`) | Gọi API lưu; hiển thị thông báo xác nhận |

**API:**

| Method | Endpoint | Body | Kết quả |
|---|---|---|---|
| GET | `/api/settings/connection` | — | cấu hình hiện tại (mật khẩu trả về `"********"`), kèm `from_db` |
| PUT | `/api/settings/connection` | `ConnSettingsIn` | lưu + xóa pool client; `400` nếu URL/CompanyDB/user sai |
| POST | `/api/settings/connection/test` | `ConnTestIn` | `{"ok","message"}` — thử đăng nhập B1, không lưu |
| GET | `/api/settings/databases` | — | `{databases, default}` |
| PUT | `/api/settings/databases` | `[{code,name}]` | lưu danh sách; `400` nếu rỗng/trùng mã/DB mặc định không trong danh sách |
| GET | `/api/settings/discount-limits` | — | `{"sales":10,"manager":20}` (mặc định khi chưa cấu hình) |
| PUT | `/api/settings/discount-limits` | `{"sales":10,"manager":20}` | lưu ngưỡng; `400` nếu ngoài 0–100 hoặc `sales > manager` |

**Lưu trữ:** bảng `app_settings` (SQLite): `b1_databases` (JSON), `b1_company_db`,
`disc_limit_sales`, `disc_limit_manager`.
`OrdersController` đọc ngưỡng CK mỗi lần validate; `B1ClientPool` đọc DB mỗi lần
lấy client — thay đổi có hiệu lực ngay.

**Phân quyền:** chỉ `admin`.

---

## Phụ lục A — Luồng xác thực, phân quyền & đa database (client)

1. Màn Login tải danh sách database từ `GET /api/auth/databases` (public).
2. `AuthState.LoginAsync(username, password, companyDb)` → `POST /api/auth/login`
   → JWT chứa claims `sub`, `role`, `b1_db`; lưu token trong `localStorage`.
3. `ApiClient.SendAsync` tự gắn header `Authorization: Bearer <token>`.
4. `401` → điều hướng về `/login`.
5. Mỗi request: `RequireRole` đọc claim `b1_db` → `AppUser.CompanyDb` →
   `B1ClientProxy` (scoped) điều hướng tới đúng client của database qua `B1ClientPool`
   (singleton, một client mỗi CompanyDB; mock: dữ liệu RAM tách biệt từng DB).
6. Mỗi trang gọi `Auth.GetAuthenticationStateAsync()` trong `OnInitializedAsync`,
   kiểm tra `Auth.HasRole(...)`; không đủ quyền → `Nav.NavigateTo("/")`.
7. Menu điều hướng ẩn/hiện mục theo vai trò; mobile (≤900px) dùng drawer + nút hamburger ☰.

## Phụ lục B — Công thức chiết khấu (đồng nhất client & server)

```
line_total(i)      = quantity × price × (1 − line_disc% / 100)
tổng_sau_CK_dòng   = Σ line_total(i)
CK_đơn_hàng        = tổng_sau_CK_dòng × header_disc% / 100
doc_total          = tổng_sau_CK_dòng − CK_đơn_hàng
```

Ví dụ kiểm chứng: 1 dòng SL=5, giá=25.000.000, CK dòng 10% → line_total =
112.500.000; CK đơn hàng 5% → CK = 5.625.000; doc_total = 106.875.000.
