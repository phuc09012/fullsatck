# Từ Local Đến Cloudflare

Tài liệu này đi từ lúc chạy app trên máy local cho đến lúc public ra Cloudflare bằng URL cố định.

## 0. Chuẩn bị

- Cài `Docker Desktop`
- Có quyền mở `PowerShell`
- Đứng trong thư mục dự án: `E:\btl-fullstack`
- Nếu muốn URL cố định, cần `Cloudflare Zero Trust` và một `Tunnel token`

## 0.1 Các service trong dự án

- `sqlserver`
- `catalogservice`
- `circulationservice`
- `identityreportservice`
- `apigateway`
- `frontend`
- `cloudflared` — chỉ dùng khi public cố định bằng Cloudflare Tunnel

## 1. Chạy local

### 1.1 Mở thư mục dự án

```powershell
Set-Location E:\btl-fullstack
```

### 1.2 Kiểm tra Docker

```powershell
docker version
docker compose version
```

### 1.3 Khởi động toàn bộ app local

```powershell
docker compose up -d --build sqlserver catalogservice circulationservice identityreportservice apigateway frontend
```

### 1.4 Kiểm tra container

```powershell
docker compose ps
```

### 1.5 Mở ứng dụng

- Frontend: `http://localhost:4173`
- API Gateway: `http://localhost:5000`

### 1.6 Xem log khi cần

```powershell
docker compose logs -f frontend
docker compose logs -f apigateway
docker compose logs -f catalogservice
docker compose logs -f circulationservice
docker compose logs -f identityreportservice
```

### 1.7 Tắt app local

```powershell
docker compose down
```

### 1.8 Khởi động lại nhanh

```powershell
docker compose down
docker compose up -d --build sqlserver catalogservice circulationservice identityreportservice apigateway frontend
```

## 2. Chạy public trên máy chủ/VPS

File compose public đã cấu hình frontend phục vụ qua Nginx và proxy `/api` về API Gateway.

### 2.1 Khởi động public stack

```powershell
Set-Location E:\btl-fullstack
docker compose -f docker-compose.public.yml up -d --build sqlserver catalogservice circulationservice identityreportservice apigateway frontend
```

### 2.2 Kiểm tra trạng thái

```powershell
docker compose -f docker-compose.public.yml ps
```

### 2.3 Mở website

- Web public: `http://<IP-hoặc-domain-của-bạn>`

### 2.4 Xem log public stack

```powershell
docker compose -f docker-compose.public.yml logs -f frontend
docker compose -f docker-compose.public.yml logs -f apigateway
```

### 2.5 Tắt public stack

```powershell
docker compose -f docker-compose.public.yml down
```

## 3. Cố định URL bằng Cloudflare Tunnel

> Quick Tunnel chỉ hợp test tạm thời. Muốn URL cố định thì dùng `named tunnel` và gán hostname trong Cloudflare Zero Trust.

### 3.1 Tạo tunnel trong Cloudflare

Làm trên Cloudflare Zero Trust dashboard:

1. Tạo một `Tunnel`
2. Gán `Public Hostname` cho tunnel
3. Copy `Tunnel token`

### 3.2 Đặt biến môi trường trong PowerShell

```powershell
$env:CLOUDFLARE_TUNNEL_TOKEN="YOUR_TUNNEL_TOKEN"
```

Nếu muốn lưu hẳn vào file `.env`, dùng:

```powershell
@"
MSSQL_SA_PASSWORD=Your_password123
JWT_KEY=ChangeThisKeyToSomethingAtLeast32CharsLong!
INTERNAL_API_KEY=LibraryInternalSecretChangeMe!
CLOUDFLARE_TUNNEL_TOKEN=YOUR_TUNNEL_TOKEN
"@ | Set-Content .env -Encoding UTF8
```

### 3.3 Chạy public stack kèm Cloudflare Tunnel

```powershell
docker compose -f docker-compose.public.yml -f docker-compose.cloudflare.yml up -d --build sqlserver catalogservice circulationservice identityreportservice apigateway frontend cloudflared
```

### 3.4 Kiểm tra tunnel

```powershell
docker compose -f docker-compose.public.yml -f docker-compose.cloudflare.yml ps
docker logs -f library-cloudflared
```

### 3.4.1 Khởi động từng service riêng lẻ

Nếu bạn muốn bật từng phần thay vì bật cả cụm:

```powershell
docker compose up -d --build sqlserver
docker compose up -d --build catalogservice
docker compose up -d --build circulationservice
docker compose up -d --build identityreportservice
docker compose up -d --build apigateway
docker compose up -d --build frontend
```

### 3.5 Mở URL cố định

URL sẽ là hostname bạn đã gán trong Cloudflare dashboard, ví dụ:

```text
https://demo.example.com
```

### 3.6 Dừng Cloudflare Tunnel

```powershell
docker compose -f docker-compose.public.yml -f docker-compose.cloudflare.yml down
```

## 4. Lệnh kiểm tra nhanh

```powershell
docker compose ps
Invoke-WebRequest http://localhost:4173 -UseBasicParsing
```

## 5. Gợi ý vận hành

- Nếu đang test local thì dùng `docker compose up -d --build sqlserver catalogservice circulationservice identityreportservice apigateway frontend`
- Nếu muốn public tạm trên một máy riêng thì dùng `docker compose -f docker-compose.public.yml up -d --build sqlserver catalogservice circulationservice identityreportservice apigateway frontend`
- Nếu muốn URL cố định thì thêm `docker compose -f docker-compose.public.yml -f docker-compose.cloudflare.yml up -d --build sqlserver catalogservice circulationservice identityreportservice apigateway frontend cloudflared`
- Nếu thay token Cloudflare thì chỉ cần cập nhật `.env` rồi chạy lại lệnh `up -d --build`
