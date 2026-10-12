# SAP B1 Sales Order — Next.js Full-Stack

Hệ thống quản lý đơn hàng bán SAP Business One (HANA), xây dựng bằng **Next.js 16** full-stack: App Router + TypeScript + Tailwind CSS v4 + SQLite + JWT.

## Tính năng

- **11 màn hình:** Đăng nhập, Dashboard, Danh sách đơn, Tạo đơn, Chi tiết đơn, Đồng bộ dữ liệu, Giám sát tích hợp, Audit, Người dùng & phân quyền (ma trận RBAC), Cài đặt kết nối
- **API đầy đủ:** auth (login/refresh/logout), orders (CRUD + stats + export Excel), masterdata (KH/hàng hoá), sync, audit, settings, users
- **Tích hợp SAP B1:** Service Layer client (mock + live), idempotency, retry
- **Design system:** sidebar navy `#12305a`, menu active vàng `#ffc928`, nút blue `#1d4e89`, font Plus Jakarta Sans

## Yêu cầu

- Node.js 18+
- npm

## Cài đặt

```bash
git clone https://github.com/trantrai/sap-b1-sales-nextjs.git
cd sap-b1-sales-nextjs
npm install
```

## Chạy development

```bash
npm run dev
```

Mở http://localhost:3000 — đăng nhập `admin` / `admin123`.

## Build production

```bash
npm run build
npm start
```

## Biến môi trường

Tạo file `.env.local`:

```env
JWT_SECRET=your-secret-key-min-32-chars
B1_MOCK=true
```

| Biến | Mặc định | Mô tả |
|------|----------|-------|
| `JWT_SECRET` | `dev-secret-change-me` | Khóa ký JWT (bắt buộc đổi ở production) |
| `B1_MOCK` | `true` | `true` = dùng dữ liệu giả, `false` = kết nối SAP B1 thật |

## Deploy lên Vercel

### Cách 1: Vercel Dashboard (khuyến nghị)

1. Vào https://vercel.com → **Add New Project**
2. **Import** repo `trantrai/sap-b1-sales-nextjs`
3. Framework Preset: **Next.js** (tự nhận)
4. Thêm biến môi trường:
   - `JWT_SECRET` = chuỗi ngẫu nhiên ≥ 32 ký tự
   - `B1_MOCK` = `true`
5. Nhấn **Deploy**

### Cách 2: Vercel CLI

```bash
npm i -g vercel
vercel login
vercel --prod
```

### Lưu ý khi deploy

- **Database:** App dùng `better-sqlite3` (file local). Trên Vercel, filesystem là ephemeral — dữ liệu sẽ mất khi redeploy. Để persist, chuyển sang **Vercel Postgres** hoặc **Turso**:
  ```bash
  # Vercel Postgres
  vercel storage create postgres
  ```
  Sau đó cập nhật `src/lib/db.ts` để dùng driver tương ứng.
- **Build command:** `npm run build` (mặc định)
- **Output directory:** `.next` (mặc định)

## Deploy bằng Docker

```dockerfile
FROM node:20-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:20-alpine
WORKDIR /app
COPY --from=builder /app/.next ./.next
COPY --from=builder /app/node_modules ./node_modules
COPY --from=builder /app/package.json ./
COPY --from=builder /app/public ./public
EXPOSE 3000
CMD ["npm", "start"]
```

```bash
docker build -t sap-b1-sales-nextjs .
docker run -p 3000:3000 \
  -e JWT_SECRET=your-secret \
  -e B1_MOCK=true \
  sap-b1-sales-nextjs
```

## Cấu trúc project

```
src/
├── app/
│   ├── api/              # API routes
│   │   ├── auth/         # login, refresh, logout, databases, users
│   │   ├── orders/       # CRUD, stats, export, cancel, close
│   │   ├── masterdata/   # customers, items
│   │   ├── sync/         # status, all, audit-log, retry-failed
│   │   ├── audit/        # audit logs
│   │   └── settings/     # settings, databases, test-connection
│   ├── dashboard/        # Trang dashboard
│   ├── login/            # Trang đăng nhập
│   ├── orders/           # Danh sách, tạo mới, chi tiết
│   ├── sync/             # Đồng bộ dữ liệu
│   ├── monitor/          # Giám sát tích hợp
│   ├── audit/            # Audit người dùng
│   ├── users/            # Quản lý người dùng + RBAC
│   ├── settings/         # Cài đặt kết nối
│   ├── layout.tsx
│   └── globals.css       # Design tokens + Tailwind v4 theme
├── components/
│   └── Layout.tsx        # Sidebar navy + topbar
└── lib/
    ├── db.ts             # SQLite (better-sqlite3)
    ├── auth.ts           # JWT (jose)
    └── b1-client.ts      # SAP B1 Service Layer client
```

## API Endpoints

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| POST | `/api/auth/login` | Đăng nhập, trả về JWT |
| POST | `/api/auth/refresh` | Refresh token |
| POST | `/api/auth/logout` | Đăng xuất |
| GET | `/api/auth/databases` | Danh sách CompanyDB |
| GET | `/api/auth/users` | Danh sách người dùng |
| GET | `/api/orders` | Danh sách đơn (filter: status, card_code, doc_num) |
| POST | `/api/orders` | Tạo đơn mới |
| GET | `/api/orders/[id]` | Chi tiết đơn |
| POST | `/api/orders/[id]/cancel` | Hủy đơn |
| POST | `/api/orders/[id]/close` | Đóng đơn |
| GET | `/api/orders/stats` | Thống kê dashboard |
| GET | `/api/orders/export` | Xuất Excel |
| GET | `/api/masterdata/customers` | Tra cứu khách hàng |
| GET | `/api/masterdata/items` | Tra cứu hàng hoá |
| GET | `/api/sync/status` | Trạng thái đồng bộ |
| POST | `/api/sync/all` | Đồng bộ tất cả |
| GET | `/api/audit` | Nhật ký audit |
| GET/PUT | `/api/settings` | Cài đặt |

## Giấy phép

MIT
