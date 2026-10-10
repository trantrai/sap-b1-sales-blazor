# Sale Order — SAP B1 Web App (ASP.NET Core + Blazor)

Web quản lý đơn hàng bán SAP Business One (HANA) qua Service Layer.

- **Backend + Frontend chung một app**: ASP.NET Core API + Blazor WebAssembly (hosted), không tách backend
- **UI**: FluentBlazor 5.0, palette navy `#1E3A5F` / gold `#F2B705` / coral `#E0522F`
- **Chế độ mock mặc định** (chưa cần SAP thật): `B1_MOCK=true`

## Chạy nhanh (mock)

```bash
dotnet run --project SalesWeb.Server --urls http://localhost:5050
```

Mở http://localhost:5050 — đăng nhập `admin` / `admin123`.

## Kết nối SAP B1 thật

Vào **Cài đặt kết nối** (quyền admin) trong app, hoặc biến môi trường:

```bash
B1_MOCK=false B1_BASE_URL=https://<hana>:50000 B1_COMPANY_DB=SBODemoVN \
B1_USERNAME=manager B1_PASSWORD=*** dotnet run --project SalesWeb.Server
```

## Docker

Xem [DEPLOY-DOCKER.md](DEPLOY-DOCKER.md).

```bash
docker compose up -d --build   # http://localhost:8080
```

## Tính năng

- CRUD đơn hàng bán (idempotent theo `external_id`, sửa có ETag, hủy/đóng dòng)
- Tra cứu KH / hàng hóa / kho / thuế suất, autocomplete
- Đồng bộ master data vào SQLite local (`POST /api/sync`)
- Audit mọi thao tác Service Layer + retry thông minh (`/audit`)
- Auth JWT + RBAC 4 cấp: viewer < sales < manager < admin
- Cài đặt kết nối B1 runtime (mật khẩu mã hóa AES-GCM), quản lý user

## Cấu trúc

```
SalesWeb.Shared/   DTO dùng chung (JSON snake_case)
SalesWeb.Server/   ASP.NET Core API + phục vụ Blazor
SalesWeb.Client/   Blazor WebAssembly + FluentBlazor (8 trang)
```
