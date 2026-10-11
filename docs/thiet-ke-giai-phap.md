# THIẾT KẾ GIẢI PHÁP CHI TIẾT
## Website quản lý Sales Order SAP Business One (HANA)
**Công nghệ:** .NET 9 API (C#) + Blazor WebAssembly + Fluent UI Blazor v5 — tích hợp SAP B1 qua Service Layer

| Mục | Nội dung |
|---|---|
| Phiên bản tài liệu | **2.0 — 11/10/2026** (cập nhật theo code thực tế: chiết khấu, đa database, QA mobile) |
| Tác giả | Muse (dựa trên các skills: sap-b1-assistant, sap-b1-master-data, fluentui-blazor, blazor, dotnet-blazor-patterns, dotnet-blazor-auth, erp-consultant, integration-planning, design-qa-audit) |
| Đối tượng | Kiến trúc sư giải pháp, team phát triển, key user |

> **Lịch sử phiên bản:** v1.0 (11/10/2026) — thiết kế ban đầu. v2.0 — cập nhật theo
> implementation thực tế: chiết khấu dòng/header + ngưỡng theo vai trò cấu hình được;
> chọn database truy cập (đa CompanyDB); kiến trúc `B1ClientPool`/`B1ClientProxy`;
> sửa endpoint cho khớp code thật.

---

## 1. Tổng quan

### 1.1. Mục tiêu
Xây dựng website quản lý đơn hàng bán (Sales Order) cho SAP Business One phiên bản HANA, cho phép nhân viên kinh doanh tạo/sửa/theo dõi đơn hàng qua giao diện web hiện đại, dữ liệu đồng bộ hai chiều với SAP B1 thông qua **Service Layer** (OData v4) — không ghi trực tiếp vào database B1.

### 1.2. Phạm vi

**Trong phạm vi (In-scope):**
- Quản lý Sales Order: tạo, xem, sửa (PUT), hủy (Cancel), đóng dòng (Close) chứng từ
- **Chiết khấu dòng và chiết khấu đơn hàng** (map `RDR1.DiscPrcnt` / `ORDR.DiscPrcnt`), validation theo vai trò
- **Ngưỡng chiết khấu theo vai trò** — admin tự cấu hình trong Settings
- **Đa database**: khai báo danh sách CompanyDB, user chọn database lúc đăng nhập
- Tra cứu master data: Khách hàng (Business Partners), Hàng hóa (Items), Kho (Warehouses), Thuế suất, Điều khoản thanh toán, Hình thức vận chuyển, NVKD
- Đồng bộ master data B1 về database local để tra cứu nhanh
- Phân quyền 4 cấp: viewer / sales / manager / admin
- Nhật ký kiểm tra (audit log) mọi thao tác Service Layer + retry khi lỗi

**Ngoài phạm vi (Out-of-scope, giai đoạn 1):**
- Các chứng từ khác (Delivery, AR Invoice, Purchase...) — kiến trúc mở để bổ sung sau
- Tích hợp thanh toán, ký số
- App mobile native

### 1.3. Nguyên tắc thiết kế
1. **Service Layer là cổng duy nhất** ghi dữ liệu vào B1 — tuyệt đối không UPDATE/INSERT trực tiếp vào bảng B1 (vi phạm toàn vẹn nghiệp vụ).
2. **Backend là API thuần** (.NET 9), frontend Blazor WASM gọi qua HTTP — tách bạch để sau này mở API cho hệ thống khác.
3. **Chế độ mock** cho phép phát triển/demo không cần B1 thật (B1_MOCK=true).
4. **Idempotent**: tạo đơn an toàn khi retry (dùng mã ngoài external_id).
5. Mọi quyết định kiến trúc quan trọng được ghi decision log (theo phong cách Solution Blueprint).

### 1.4. Giả định
- SAP B1 v10 (HANA), Service Layer đã bật và truy cập được từ server ứng dụng.
- Đã có tài khoản Service Layer (dạng B1SiteUser hoặc user thường) với quyền tạo/sửa Sales Order.
- ObjType Sales Order = **17**, bảng **ORDR/RDR1**, khóa chính **DocEntry** (theo `sap-b1-assistant/references/objects/object-types.md`).

---

## 2. Kiến trúc tổng thể

```
┌──────────────┐      HTTPS/JSON       ┌──────────────────────┐     OData v4      ┌─────────────────┐
│ Blazor WASM  │ ◄──────────────────► │  ASP.NET Core API    │ ◄───────────────► │ SAP B1          │
│ (Fluent UI)  │   JWT Bearer token   │  (.NET 9)            │  B1SESSION cookie │ Service Layer   │
└──────────────┘   (claim: b1_db)     │  ┌────────────────┐  │  (per CompanyDB)│ /b1s/v2         │
                                      │  │ Controllers  │  │                   └─────────────────┘
                                      │  ├────────────────┤  │
                                      │  │ B1ClientProxy│  │     ┌──────────┐
                                      │  │ (scoped)     │──┼────►│ SQLite   │
                                      │  ├────────────────┤  │     │ (local)  │
                                      │  │ B1ClientPool │  │     └──────────┘
                                      │  │ 1 client     │  │      users, audit,
                                      │  │ per CompanyDB│  │      master cache,
                                      │  └────────────────┘       app_settings
                                      └──────────────────────┘
```

**Đa database (v2.0):** mỗi request được `B1ClientProxy` (scoped) điều hướng tới
đúng client của CompanyDB mà user đã chọn lúc đăng nhập (claim `b1_db` trong JWT).
`B1ClientPool` (singleton) cache một client cho mỗi CompanyDB — ở chế độ live mỗi
DB có session Service Layer riêng; ở chế độ mock mỗi DB có dữ liệu RAM tách biệt.
Controllers giữ nguyên DI `IB1Client`, không cần sửa khi thêm database mới.

### 2.1. Luồng xử lý điển hình — Đăng nhập + chọn database
1. Màn Login tải danh sách database từ `GET /api/auth/databases` (public).
2. User nhập tài khoản + chọn database → `POST /api/auth/login {username, password, company_db}`.
3. Server kiểm tra `company_db` có trong danh sách khai báo → tạo JWT kèm claim `b1_db`.
4. Mọi API sau đó: `RequireRole` đọc claim → `AppUser.CompanyDb` → proxy chọn đúng B1 client.

### 2.2. Luồng xử lý điển hình — Tạo Sales Order (có chiết khấu)
1. User nhập đơn trên form Blazor → validate client-side; tổng tiền tính realtime theo công thức B1.
2. Client gọi `POST /api/orders` kèm JWT.
3. API kiểm tra quyền → `ValidateDiscounts`: CK trong 0–100%; ngưỡng theo vai trò đọc từ
   Settings (`disc_limit_sales`/`disc_limit_manager`; admin không giới hạn) → vượt ngưỡng trả `403`.
4. Kiểm tra idempotency (`external_id`) → map DTO sang payload Service Layer
   (`DiscountPercent` → `ORDR.DiscPrcnt`, `DocumentLines[].DiscountPercent` → `RDR1.DiscPrcnt`).
5. `POST /b1s/v2/Orders` → nhận `DocEntry` → lưu audit → trả 201 cho client.
6. Lỗi B1 được dịch sang thông điệp tiếng Việt thân thiện.

### 2.3. Quyết định kiến trúc (Decision log)

| # | Quyết định | Lý do | Phương án loại bỏ |
|---|---|---|---|
| AD-01 | Dùng Service Layer thay vì DI API | REST/OData, không cần cài B1 client trên server, chạy đa nền tảng (Linux/Docker) | DI API (chỉ Windows, COM) |
| AD-02 | Blazor WASM hosted trong ASP.NET Core | Một deployment duy nhất, dùng chung auth/session với API | Tách frontend React riêng (tốn 2 deploy) |
| AD-03 | Fluent UI Blazor v5 | Component chuẩn Microsoft, theme CSS-based, phù hợp phong cách Fluent đã chốt | MudBlazor (đổi design system) |
| AD-04 | SQLite cho dữ liệu local | Đủ cho users/audit/cache, zero-ops, dễ backup | SQL Server (nặng, tốn license cho DB phụ) |
| AD-05 | Không xóa vật lý đơn trong B1 | B1 cấm xóa Sales Order đã lưu (lỗi -5006); chỉ cho Cancel/Close | DELETE /Orders(n) |
| AD-06 | Đa database qua `B1ClientPool` + proxy (v2.0) | Một user chọn 1 DB lúc login; các DB dùng chung SL URL + user kỹ thuật; controllers không đổi | Client singleton + đổi DB toàn cục (user A đổi DB ảnh hưởng user B) |
| AD-07 | Ngưỡng CK cấu hình trong Settings (v2.0) | Admin tự điều chỉnh chính sách CK, hiệu lực ngay không cần deploy | Hardcode ngưỡng theo role trong code |

---

## 3. Backend — .NET Core API (C#)

### 3.1. Cấu trúc solution

```
sap-b1-sales-blazor/
├── SalesWeb.Shared/            # DTO dùng chung (snake_case để tương thích JSON cũ)
│   └── Dtos/                   # OrderDto, OrderLineDto, CustomerDto, ItemDto, ...
├── SalesWeb.Server/            # ASP.NET Core API + host Blazor WASM
│   ├── Controllers/            # OrdersController, MasterDataController, SyncController,
│   │                           # AuthController, UsersController, AuditController, SettingsController
│   ├── Services/
│   │   ├── B1/                 # B1ServiceLayerClient, B1OrderService, B1MasterDataService,
│   │                           # B1SessionManager, B1ErrorMapper, MockB1Client
│   │   ├── OrderService.cs     # Nghiệp vụ đơn hàng (idempotency, ETag)
│   │   ├── SyncService.cs      # Đồng bộ master data B1 → SQLite
│   │   └── AuditService.cs     # Ghi audit log
│   ├── Data/                   # DbContext SQLite (users, roles, audit, master cache, settings)
│   └── Middleware/             # JWT auth, exception handler, request logging
└── SalesWeb.Client/            # Blazor WebAssembly (xem mục 4)
```

### 3.2. Thiết kế API endpoints (khớp implementation)

| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/auth/login` | public | Đăng nhập `{username, password, company_db}` → JWT (claim `b1_db`) |
| GET | `/api/auth/databases` | public | Danh sách database + DB mặc định (màn Login) |
| GET | `/api/auth/me` | viewer+ | Thông tin user hiện tại (kèm `company_db` đã chọn) |
| GET | `/api/auth/users` | admin | Danh sách user |
| POST | `/api/auth/users` | admin | Tạo user |
| PATCH | `/api/auth/users/{username}` | admin | Sửa user (role, active, password…) |
| GET | `/api/auth/roles` | admin | Ma trận phân quyền RBAC |
| GET | `/api/orders?status=&card_code=&top=&skip=` | sales+ | Danh sách đơn (phân trang) |
| GET | `/api/orders/{docEntry}` | sales+ | Chi tiết đơn (kèm ETag) |
| POST | `/api/orders` | sales+ | Tạo đơn (idempotent theo `external_id`; validate CK) |
| PUT | `/api/orders/{docEntry}` | sales+ | Sửa đơn (ghi chú…; validate CK) |
| POST | `/api/orders/{docEntry}/cancel` | manager+ | Hủy đơn → `Orders(n)/Cancel` |
| POST | `/api/orders/{docEntry}/close` | manager+ | Đóng dòng → `Orders(n)/Close` |
| GET | `/api/masterdata/customers?q=` | viewer+ | Autocomplete KH |
| GET | `/api/masterdata/items?q=` | viewer+ | Autocomplete hàng hóa |
| GET | `/api/masterdata/warehouses` | viewer+ | Danh sách kho |
| GET | `/api/masterdata/tax-codes` | viewer+ | Thuế suất |
| GET | `/api/masterdata/payment-terms` | viewer+ | Điều khoản thanh toán |
| GET | `/api/masterdata/shipping-types` | viewer+ | Hình thức vận chuyển |
| GET | `/api/masterdata/sales-persons` | viewer+ | Nhân viên kinh doanh |
| GET | `/api/masterdata/customers/{cardCode}/addresses` | viewer+ | Địa chỉ KH |
| GET | `/api/masterdata/customers/{cardCode}/contacts` | viewer+ | Liên hệ KH |
| GET | `/api/sync/status` | manager+ | Trạng thái đồng bộ từng entity |
| POST | `/api/sync` | manager+ | Trigger đồng bộ `{entities:[...]}` |
| GET | `/api/audit?operation=&status=&q=&limit=` | sales+ | Nhật ký Service Layer |
| GET | `/api/audit/failed-count` | sales+ | Số bản ghi lỗi (badge) |
| GET | `/api/audit/{id}` | sales+ | Chi tiết 1 bản ghi |
| POST | `/api/audit/{id}/retry` | manager+ | Thử lại 1 bản ghi lỗi |
| POST | `/api/audit/retry-all` | manager+ | Thử lại tất cả lỗi |
| GET | `/api/settings/connection` | admin | Cấu hình kết nối hiện tại |
| PUT | `/api/settings/connection` | admin | Lưu + rebuild client runtime |
| POST | `/api/settings/connection/test` | admin | Test kết nối (không lưu) |
| GET | `/api/settings/discount-limits` | admin | Ngưỡng CK theo vai trò |
| PUT | `/api/settings/discount-limits` | admin | Lưu ngưỡng CK |
| GET | `/api/settings/databases` | admin | Danh sách database đã khai báo |
| PUT | `/api/settings/databases` | admin | Lưu danh sách database |
| GET | `/health` | public | `{api, b1, db}` |

Chuẩn response lỗi thực tế: `{ "detail": "<thông điệp tiếng Việt>" }` kèm HTTP status
(`400`/`401`/`403`/`404`/`409`).

### 3.3. Tầng tích hợp B1 Service Layer

**Quản lý session** (`B1SessionManager`):
- Đăng nhập: `POST /b1s/v2/Login` với `{ CompanyDB, UserName, Password }` → lưu cookie `B1SESSION` (+ `ROUTEID` nếu có load balancer).
- Session timeout mặc định 30 phút → tự re-login khi gặp 401, retry đúng 1 lần.
- Dùng `IHttpClientFactory` với named client `b1`; cookie lưu trong `CookieContainer` per-session, thread-safe qua `SemaphoreSlim`.

**Các thao tác Sales Order** (theo `sap-b1-assistant/references/servicelayer/`):

| Nghiệp vụ | Service Layer | Lưu ý |
|---|---|---|
| Tạo đơn | `POST /b1s/v2/Orders` + `DocumentLines[]` | Trả 201 + entity đã tạo (lấy `DocEntry`); dùng `Prefer: return-no-content` nếu không cần body |
| Đọc đơn | `GET /b1s/v2/Orders({docEntry})?$expand=DocumentLines` | `$select` để giảm payload khi list |
| Sửa đơn | `PATCH /b1s/v2/Orders({docEntry})` | **Chỉ gửi field thay đổi** — PUT sẽ reset field không gửi về default; property read-only trong PATCH bị bỏ qua lặng lẽ |
| Hủy đơn | `POST /b1s/v2/Orders({n})/Cancel` | Bound action; đơn đã có chứng từ liên kết sẽ báo lỗi B1 |
| Đóng đơn | `POST /b1s/v2/Orders({n})/Close` | Bound action |
| Tìm kiếm | `GET /b1s/v2/Orders?$filter=...&$top=20&$skip=0&$orderby=DocEntry desc` | OData query options chuẩn |

**Retry & resilience**: retry tối đa 3 lần với backoff cho lỗi mạng/5xx/timeout; **không retry** lỗi nghiệp vụ 4xx của B1.

**Mock mode**: `MockB1Client` implement cùng interface, sinh dữ liệu giả — dùng cho dev/demo khi `B1_MOCK=true`.

### 3.4. Mapping dữ liệu B1 (Sales Order — ObjType 17)

Header (`ORDR` → entity `Orders`):

| Field B1 | Kiểu | API field | Ghi chú |
|---|---|---|---|
| DocEntry | int (key) | `doc_entry` | Khóa chính, do B1 sinh |
| DocNum | int | `doc_num` | Số chứng từ hiển thị |
| CardCode | string | `customer_code` | Mã KH (`OCRD.CardCode`) |
| DocDate | date | `doc_date` | Ngày chứng từ |
| DocDueDate | date | `due_date` | Ngày giao hàng |
| DocTotal | decimal | `total` | Tổng tiền (read-only khi tạo) |
| DocStatus | char | `status` | O=Open, C=Closed, N=Cancelled |
| Comments | string | `comments` | Ghi chú |
| DiscPrcnt | decimal | `discount_percent` | **Chiết khấu header** (% trên tổng đơn, 0–100) |
| U_... | — | `udf` (object) | Trường UDF công ty tự định nghĩa |

Dòng (`RDR1` → `DocumentLines[]`):

| Field B1 | API field | Ghi chú |
|---|---|---|
| ItemCode | `item_code` | Bắt buộc |
| Quantity | `quantity` | Bắt buộc, > 0 |
| Price | `unit_price` | Theo bảng giá hoặc nhập tay (tùy quyền) |
| WarehouseCode | `warehouse` | Mặc định theo setup |
| DiscPrcnt | `discount_percent` | **Chiết khấu dòng** (% trên dòng, 0–100) |
| LineTotal | `line_total` | Read-only (đã trừ CK dòng) |

### 3.5. Xử lý chiết khấu (v2.0)

**Công thức** (đồng nhất client, server và SAP B1):
```
line_total(i)    = quantity × price × (1 − line_disc% / 100)
tổng_sau_CK_dòng = Σ line_total(i)
CK_đơn_hàng      = tổng_sau_CK_dòng × header_disc% / 100
doc_total        = tổng_sau_CK_dòng − CK_đơn_hàng
```

**Validation server** (`OrdersController.ValidateDiscounts`, áp dụng cho tạo và sửa):
1. Mọi CK (dòng + header) phải trong 0–100%, ngược lại `400`.
2. Ngưỡng theo vai trò đọc từ Settings (`app_settings`: `disc_limit_sales`,
   `disc_limit_manager`; mặc định 10/20; admin không giới hạn) — vượt ngưỡng trả `403`
   kèm thông điệp nêu rõ ngưỡng hiện hành.

**Cấu hình ngưỡng** (chỉ admin): `GET/PUT /api/settings/discount-limits`
`{sales, manager}` — validate 0–100 và `sales ≤ manager`; hiệu lực ngay không cần restart.

### 3.6. Đa database / đa CompanyDB (v2.0)

- Admin khai báo danh sách database trong Settings (`app_settings.b1_databases`,
  JSON `[{code, name}]`); API `GET/PUT /api/settings/databases`.
- Màn Login tải danh sách qua `GET /api/auth/databases` (public); user chọn DB cho phiên làm việc.
- `POST /api/auth/login` nhận thêm `company_db` (phải thuộc danh sách, ngược lại `400`);
  JWT chứa claim `b1_db`.
- `RequireRole` dựng `AppUser.CompanyDb` từ claim (fallback DB mặc định hệ thống).
- `B1ClientProxy` (scoped, đăng ký thay `IB1Client` singleton) điều hướng mỗi request
  tới client của đúng DB qua `B1ClientPool` (singleton, một client mỗi CompanyDB).
- Lưu cài đặt kết nối → `pool.ClearAsync()` để client build lại theo cấu hình mới.
- Các DB dùng chung Service Layer URL + tài khoản kỹ thuật (mô hình B1 chuẩn:
  một server, nhiều company, cùng user kỹ thuật).

### 3.7. Bảo mật
- **JWT HS256** cho session web; mật khẩu hash **PBKDF2 200k iterations**.
- **RBAC** — ma trận 10 quyền × 4 vai trò:

| Quyền | viewer | sales | manager | admin |
|---|---|---|---|---|
| orders.view | ✓ | ✓ | ✓ | ✓ |
| orders.create | | ✓ | ✓ | ✓ |
| orders.edit | | ✓ | ✓ | ✓ |
| orders.cancel | | | ✓ | ✓ |
| orders.close | | | ✓ | ✓ |
| master.view | ✓ | ✓ | ✓ | ✓ |
| sync.run | | | ✓ | ✓ |
| audit.view | | | ✓ | ✓ |
| users.manage | | | | ✓ |
| settings.manage | | | | ✓ |

- Credential B1 lưu trong DB settings, **mã hóa Fernet**, không bao giờ log.
- Guard chống truy cập trực tiếp URL theo role ở cả client (redirect login) và server (`[Authorize(Policy=...)]`).

### 3.6. Idempotency & Concurrency
- **Tạo đơn**: client sinh `external_id` (GUID); server kiểm tra đã tồn tại chưa trước khi gọi B1 → retry an toàn.
- **Sửa đơn**: dùng ETag của Service Layer — client gửi `If-Match`, server chuyển thành header khi PATCH sang B1; conflict → 409 kèm dữ liệu mới nhất.
- **Audit log** mọi thao tác ghi (ai, khi nào, DocEntry, before/after rút gọn).

### 3.7. Xử lý lỗi B1 thường gặp

| Tình huống | Hành động |
|---|---|
| Session hết hạn (401) | Tự login lại, retry 1 lần |
| Kỳ kế toán bị khóa | Dịch message B1 sang tiếng Việt thân thiện, trả 422 |
| Hủy đơn đã có Delivery/Invoice liên kết | Trả 422 + gợi ý tạo đơn điều chỉnh |
| Không kết nối được Service Layer | Circuit-breaker: sau 5 lỗi liên tiếp, trả 503 trong 60s |

---

## 4. Frontend — Fluent Blazor UI

Áp dụng các skill: `fluentui-blazor` (Fluent UI Blazor **v5**), `blazor`, `dotnet-blazor-patterns`, `frontend-design`, `ui-pattern-library`, `design-qa-audit`.

### 4.1. Cấu trúc Client project

```
SalesWeb.Client/
├── Layout/
│   ├── MainLayout.razor        # Khung chung: header, nav, <FluentProviders/>
│   └── NavMenu.razor           # Dùng FluentNav + FluentNavItem (v5, không còn FluentNavMenu)
├── Pages/
│   ├── Login.razor
│   ├── Orders/Orders.razor     # Danh sách đơn
│   ├── Orders/OrderDetail.razor
│   ├── Orders/OrderEdit.razor  # Tạo/sửa (dùng chung form)
│   ├── Sync.razor              # Đồng bộ master data
│   ├── Monitoring.razor        # Giám sát / audit log
│   ├── Users.razor
│   └── Settings.razor
├── Components/                 # Component dùng chung (xem 4.4)
├── Services/
│   ├── ApiClient.cs            # HttpClient gọi API, tự gắn JWT
│   ├── AuthState.cs            # Trạng thái đăng nhập (CascadingAuthenticationState)
│   └── ThemeService.cs         # Custom theme qua IThemeService
└── wwwroot/css/app.css         # CSS bổ sung + CSS variables
```

### 4.2. Theme & Layout (Fluent UI Blazor v5)

> Lưu ý quan trọng (từ skill `fluentui-blazor`): **v5 không còn component `<FluentDesignTheme>`**. Theme được áp dụng qua CSS custom properties + JS. Custom theme (navy `#1E3A5F` / gold `#F2B705` / coral `#E0522F`) triển khai bằng `IThemeService.CreateCustomThemeAsync(new ThemeSettings { Color = "#1E3A5F", IsExact = true })`.

- `Program.cs`: `builder.Services.AddFluentUIComponents();`
- `MainLayout.razor`: đặt `<FluentProviders />` một lần ở root (thay cho việc khai báo riêng lẻ Dialog/Tooltip/KeyCode providers).
- Layout: header cố định (logo + tên công ty + user menu), nav trái dùng `FluentNav`/`FluentNavItem` (v5), vùng nội dung chính responsive.
- Không dùng emoji thay icon — dùng icon Fluent có sẵn trong package v5.

### 4.3. Thiết kế màn hình (khớp implementation v2.0)

**a) Đăng nhập (`Login.razor` — `/login`)**
- Form username/password + dropdown **"Cơ sở dữ liệu"** (tải từ `GET /api/auth/databases`).
- Database được chọn lưu vào JWT; mọi thao tác trong phiên dùng đúng DB đó.

**b) Danh sách đơn hàng (`Orders.razor` — `/`)**
- `FluentDataGrid`: Số đơn, Ngày, Khách hàng, Tổng tiền, Trạng thái (chip màu), Thao tác.
- Lọc theo trạng thái + mã KH; phân trang Prev/Next; empty text tiếng Việt.

**c) Tạo đơn hàng (`OrderCreate.razor` — `/orders/new`)**
- Autocomplete KH (kèm địa chỉ giao/nhận, liên hệ) và hàng hóa (debounce).
- Cột **CK %** từng dòng + ô **Chiết khấu đơn hàng (%)**; breakdown realtime:
  Tổng sau CK dòng → CK đơn hàng → TỔNG CỘNG.
- Nút "Tạo đơn hàng" disabled khi form chưa hợp lệ, kèm gợi ý lý do
  ("Hãy chọn khách hàng ở mục 1."…).
- Trường chuẩn B1: NumAtCard, TaxDate, DocCurrency/DocRate, PaymentGroupCode,
  TransportationCode, ShipToCode/PayToCode, Project.

**d) Chi tiết đơn (`OrderDetail.razor` — `/orders/{docEntry}`)**
- Hiển thị CK từng dòng, CK đơn hàng và breakdown tổng tiền.
- Sửa ghi chú (sales+, kèm ETag), Hủy đơn / Đóng dòng (manager+, `confirm()`).

**e) Đồng bộ (`SyncPage.razor`), Giám sát (`AuditPage.razor`), Người dùng (`UsersPage.razor`)**
- Sync: trạng thái từng entity + trigger theo entity đã chọn.
- Audit: lọc operation/status/từ khóa, xem chi tiết, retry đơn/lẻ (manager+), badge số lỗi.
- Users: CRUD user, bật/tắt tài khoản, ma trận RBAC 10 quyền × 4 vai trò.

**f) Cài đặt (`SettingsPage.razor` — `/settings`, chỉ admin)**
- Chế độ Demo/B1 thật; thông tin Service Layer (URL, **CompanyDB dạng dropdown**
  từ danh sách đã khai báo — mặc định theo DB đã chọn lúc login, mật khẩu mã hóa AES-GCM).
- Card **"Danh sách database truy cập"**: thêm/xóa/lưu các CompanyDB.
- Card **"Ngưỡng chiết khấu theo vai trò"**: sales/manager tối đa (%).

**g) Mobile (≤900px)** — theo báo cáo QA v2.0:
- Sidebar thành drawer trượt + nút hamburger ☰ trên topbar (tự đóng khi chuyển trang).
- Form 1 cột, bảng cuộn ngang, khối tổng tiền xếp chồng full-width.

### 4.4. Components dùng chung
| Component | Dùng |
|---|---|
| `OrderStatusBadge.razor` | Hiển thị trạng thái đơn (màu theo O/C/N) |
| `CustomerPicker.razor` | Autocomplete khách hàng |
| `ItemPicker.razor` | Autocomplete hàng hóa + giá |
| `ConfirmDialog.razor` | Hộp xác nhận Hủy/Đóng đơn |
| `ErrorAlert.razor` | Hiển thị lỗi API thống nhất |

### 4.5. Gọi API & trạng thái
- `ApiClient` (typed `HttpClient`): tự gắn JWT, tự refresh khi 401 một lần, parse `code` lỗi chuẩn.
- Auth: `CascadingAuthenticationState` + `AuthorizeView` theo policy; guard trong `OnInitializedAsync` **phải** `await` trạng thái auth trước khi redirect (tránh redirect oan khi load trực tiếp URL).
- Không dùng state management phức tạp (Fluxor) — component state + cascading value đủ cho quy mô này.

### 4.6. UX & kiểm tra chất lượng
- Áp dụng checklist `design-qa-audit` trước khi giao: test trên mobile (breakpoint, touch target), kiểm tra overflow bảng, trạng thái loading/error/empty của mọi màn hình.
- Form: label rõ ràng, validate inline, thông báo thành công/bại bằng `FluentToast` (v5 dùng component trực tiếp, không còn `IToastService`).

---

## 5. Triển khai

- **Docker**: multi-stage build (`sdk:9.0` → `aspnet:9.0`), một container duy nhất phục vụ cả API + Blazor WASM.
- Cấu hình qua biến môi trường: `B1_MOCK`, `B1_URL`, `B1_COMPANYDB`, `JWT_SECRET`, `ConnectionStrings__Default`.
- SQLite persist qua Docker volume `./data`.
- Health check: `GET /health` kiểm tra API + ping Service Layer (`GET /b1s/v2/$metadata`).

## 6. Kiểm thử

| Tầng | Công cụ | Phạm vi |
|---|---|---|
| API unit/integration | xUnit + WebApplicationFactory | Controllers, idempotency, RBAC, B1ErrorMapper |
| Component Blazor | bUnit (skill `dotnet-blazor-testing`) | Form tạo đơn, picker, badge |
| E2E | Playwright (skill `dotnet-blazor-playwright`) | Luồng tạo → duyệt → hủy đơn với mock B1 |
| B1 thật | Postman collection | Kịch bản Service Layer: Login, CRUD Orders |

## 7. Rủi ro & giảm thiểu

| Rủi ro | Mức | Giảm thiểu |
|---|---|---|
| Service Layer chậm/timeout giờ cao điểm | Trung bình | Cache master data local + Sync định kỳ; timeout & retry có giới hạn |
| Thay đổi UDF/schema B1 của khách hàng | Trung bình | Đọc `$metadata` khi khởi động; mapping UDF cấu hình được, không hard-code |
| Session B1 hết hạn giữa chừng | Thấp | Tự re-login + retry 1 lần (đã thiết kế) |
| User tạo trùng đơn do double-click | Thấp | Idempotency `external_id` + disable nút khi submit |

## 8. Lộ trình

| Giai đoạn | Nội dung | Kết quả |
|---|---|---|
| 1 (hiện tại) | API + UI quản lý Sales Order, mock mode | Demo được không cần B1 thật |
| 2 | Kết nối B1 thật, UAT với dữ liệu thật | Go-live Sales Order |
| 3 | Mở rộng Delivery, AR Invoice (tái dùng B1Client + layout) | Phủ kín quy trình bán hàng |
| 4 | Webhook B1 (FP2602) đẩy thay đổi về web realtime | Theo dõi realtime |

---
*Tài liệu được soạn với sự hỗ trợ của các workspace skills. Mọi tên entity Service Layer và object type đã đối chiếu với references trong `sap-b1-assistant` (B1 10.0).*
