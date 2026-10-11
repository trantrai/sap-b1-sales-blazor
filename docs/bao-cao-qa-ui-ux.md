# Báo cáo QA/QC UI/UX — Web quản lý Sales Order SAP B1

Ngày: 2026-10-11 · Phạm vi: 8 màn hình (desktop 1440px + mobile 375px) · Chế độ mock
Phương pháp: 5 pass theo skill `design-qa-audit` + test chức năng bằng Playwright + API.

---

## Pass 1 — Visual Originality · Verdict: `acceptable but safe`

- Đây là **product surface** (công cụ nghiệp vụ ERP nội bộ), không phải landing/marketing —
  ưu tiên mật độ thông tin và hiệu quả thao tác, đúng register.
- Bản sắc: Fluent UI + palette navy `#1E3A5F` / gold `#F2B705` / coral `#E0522F`,
  topbar viền gold, badge "SO". Nhất quán cả 8 màn hình.
- Không có cliché trang trí thừa (không gradient tím, không blob, không glass panel).

## Pass 2 — UI & Hierarchy · Verdict: `đạt` (sau khi sửa 4 issue)

| # | Issue | Vị trí | Mức | Trạng thái |
|---|---|---|---|---|
| 1 | Sidebar mobile 64px làm vỡ chữ nav (CSS ẩn `.fm-menu-item span` không khớp `FluentNavItem`) | Mọi màn, ≤900px | **Blocker** | ✅ Sửa: sidebar thành drawer trượt + nút hamburger ☰ trên topbar, tự đóng khi chuyển trang |
| 2 | Nút "Tạo đơn hàng" bị disabled mà không cho biết lý do | `/orders/new` | Major | ✅ Sửa: thêm gợi ý dưới nút ("Hãy chọn khách hàng ở mục 1.", "Dòng 2 chưa đủ thông tin…") |
| 3 | Bảng trống hiện "No data to show." tiếng Anh | Sync (+4 bảng khác) | Minor | ✅ Sửa: `EmptyContent` tiếng Việt cho cả 5 `FluentDataGrid` |
| 4 | Khối tổng tiền + bảng DB tràn/cắt trên mobile | `/orders/new`, `/settings` | Minor | ✅ Sửa: tổng tiền xếp chồng full-width; bảng bọc `overflow-x` |

Điểm tốt: hierarchy rõ (tiêu đề → card → action), spacing theo token, nút
Primary/Outline phân biệt rõ, trạng thái đơn dùng chip màu nhất quán.

## Pass 3 — Text Integrity · Verdict: `pass`

- Toàn bộ UI tiếng Việt; không còn chuỗi tiếng Anh sót (đã quét).
- Microcopy hướng task: "Lưu ngưỡng chiết khấu", "+ Thêm database", "Kiểm tra kết nối".
- Thông điệp lỗi nêu rõ nguyên nhân + cách xử lý:
  "Database 'NODB' chưa được khai báo trong hệ thống.",
  "Vai trò 'sales' chỉ được áp chiết khấu tối đa 10%…".

## Pass 4 — Mobile-First · Verdict: `pass` (sau sửa)

- Drawer điều hướng: mở bằng hamburger, đóng bằng backdrop/chuyển trang. Touch target đạt.
- Form grid 1 cột, bảng cuộn ngang trong `fm-table-wrap`, không scroll ngang trang.
- Tổng tiền full-width, nút submit không vỡ chữ.

## Pass 5 — Polish · Ghi nhận

- Đã tách blocker chức năng khỏi chi tiết mỹ thuật; không redesign.
- Còn lại (không chặn): cột "Thao tác" ở Giám sát hiện mã thô `order.create`
  (đã có `OperationLabel` nhưng mock chưa map hết); cột action bảng Người dùng
  thiếu header.

---

## Test chức năng (Playwright + API)

| # | Kịch bản | Kết quả |
|---|---|---|
| F1 | Login sai mật khẩu | ✅ Báo lỗi "Sai tên đăng nhập hoặc mật khẩu" |
| F2 | Login dropdown database | ✅ Hiện đủ 2 DB đã khai báo; chọn TESTDB → `/me` trả `company_db=TESTDB` |
| F3 | Login DB không tồn tại | ✅ `400` "Database 'NODB' chưa được khai báo…" |
| F4 | Cách ly dữ liệu theo DB | ✅ Đơn tạo trong TESTDB không hiện ở SBODEMO_VN |
| F5 | Tạo đơn thiếu KH | ✅ Nút disabled + gợi ý "Hãy chọn khách hàng ở mục 1." |
| F6 | CK vượt ngưỡng (sales 10%) | ✅ `403` kèm ngưỡng hiện hành |
| F7 | CK dòng 10% + CK đơn 5% | ✅ 112.500.000 → 106.875.000 (khớp công thức B1) |
| F8 | Lưu ngưỡng CK sai (sales > manager) | ✅ `400` |
| F9 | Thêm/xóa DB trong Settings | ✅ Lưu được; validate trùng mã, ≥1 DB, DB mặc định phải trong danh sách |
| F10 | Mở drawer mobile → chuyển trang | ✅ Drawer tự đóng |

Build: `dotnet build` 0 lỗi. Đã push GitHub (commit `245be82`).

---

## Phụ lục — QA v2.2 (2026-10-11)

Phạm vi: Dashboard, xuất Excel, copy đơn, refresh token, audit người dùng, rate limit,
backup SQLite, dark mode, tìm kiếm toàn cục (Ctrl+K), in đơn.

| # | Kịch bản | Kết quả |
|---|---|---|
| G1 | `GET /api/orders/stats` | ✅ KPI + 30 ngày doanh số + top 5 KH/hàng |
| G2 | Dashboard UI | ✅ 5 thẻ KPI, biểu đồ cột SVG có tooltip, ảnh `dashboard.png` |
| G3 | Xuất Excel | ✅ File `.xlsx` mở được, header navy, đúng bộ lọc |
| G4 | Copy đơn hàng | ✅ `/orders/new?copyFrom=` điền sẵn KH/dòng/CK, banner xanh |
| G5 | Refresh token | ✅ Login trả cặp token; refresh xoay vòng; token cũ hết hiệu lực |
| G6 | Client tự refresh khi 401 | ✅ Retry 1 lần sau refresh thành công |
| G7 | Logout thu hồi token | ✅ Refresh sau logout → 401 |
| G8 | Tab audit người dùng | ✅ 98 bản ghi: đăng nhập/xuất Excel/gia hạn phiên, label tiếng Việt |
| G9 | Rate limit login | ✅ 10×401 rồi 429 (test 12 request liên tiếp) |
| G10 | Backup SQLite | ✅ File `backup-*.db` sinh ra sau startup, mở được |
| G11 | Dark mode | ✅ `setThemeMode('dark')` + CSS override; nhớ lựa chọn qua localStorage |
| G12 | Ctrl+K tìm kiếm | ✅ Focus ô search; tìm đơn/KH/hàng từ cache |
| G13 | In đơn hàng | ✅ Nút In + CSS `@media print` ẩn nav/nút |

Issue đã sửa trong đợt QA:
- `Blazor.theme.setDarkMode` không tồn tại → dùng `setThemeMode('dark'/'light')`.
- Tab audit không đổi nội dung → chuyển sang `@bind-ActiveTabId` + `StateHasChanged()` sau load.

Build: `dotnet build` 0 lỗi.
