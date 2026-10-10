# Deploy website bằng Docker

## 1. Chạy trên máy có Docker (Windows / Linux / Mac)

```bash
cd sap-b1-sales-blazor

# Sửa JWT_SECRET trong docker-compose.yml thành chuỗi bí mật ≥ 32 ký tự
# (khuyến nghị, tránh dùng giá trị demo)

docker compose up -d --build
```

Mở trình duyệt: **http://localhost:8080** → đăng nhập `admin` / `admin123` (chế độ demo).

Xem log / dừng:

```bash
docker compose logs -f
docker compose down        # dữ liệu SQLite vẫn giữ trong ./data
```

## 2. Chuyển sang SAP B1 thật

Sửa `docker-compose.yml` (bỏ comment + điền đúng):

```yaml
B1_MOCK: "false"
B1_SERVICE_LAYER_URL: "https://b1-cong-ty:50000/b1s/v2"
B1_COMPANY_DB: "TEN_DB"
B1_USERNAME: "tai_khoan_ky_thuat"
B1_PASSWORD: "mat_khau"
```

Rồi `docker compose up -d --build`. Hoặc làm ngay trên giao diện:
trang **Cài đặt kết nối** (admin) → chọn "SAP B1 thật" → Lưu & áp dụng ngay.

## 3. Đưa lên internet (VPS / cloud)

- **VPS riêng** (Ubuntu): cài Docker, copy project lên, `docker compose up -d --build`,
  trỏ domain về IP VPS, dùng reverse proxy (nginx/Caddy) + HTTPS.
- **Railway / Render / Fly.io**: tạo service từ repo Git chứa project này,
  Dockerfile được nhận diện tự động. Nhớ set biến môi trường `JWT_SECRET`
  (≥ 32 ký tự) và mount volume cho `/app/data` nếu platform hỗ trợ
  (không có volume thì SQLite reset mỗi lần deploy lại — vẫn dùng được cho demo).

## 4. Build image thủ công

```bash
docker build -t salesweb:latest .
docker run -d --name salesweb -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  -e B1_MOCK=true \
  -e JWT_SECRET="doi-mat-khau-nay-di-toi-thieu-32-ky-tu-xxxx" \
  salesweb:latest
```

## Lưu ý

- Image ~230MB (runtime aspnet:9.0).
- Health check: `GET /health` → `{"status":"ok","b1_mode":"mock"}`.
- SQLite + file cấu hình nằm trong volume `./data` — backup thư mục này là đủ.
