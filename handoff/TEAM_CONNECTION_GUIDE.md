# Hướng Dẫn Kết Nối Cho 2 Nhóm Còn Lại

Tài liệu này dành cho 2 nhóm đang giữ các service riêng của hệ thống. Mục tiêu là để các nhóm tải code về, chạy đúng cấu hình, và các service có thể gọi qua lại được với nhau.

## 1. Kiến Trúc Kết Nối

Hệ thống đang chia theo microservice:

- `CatalogService`: quản lý sách, thể loại, tồn kho
- `CirculationService`: quản lý mượn, trả, gia hạn, phạt
- `IdentityReportService`: đăng nhập, tài khoản, độc giả, báo cáo
- `ApiGateway`: cổng vào chung cho frontend

Các service không nên gọi trực tiếp UI. Frontend chỉ gọi `ApiGateway`, còn service nội bộ gọi nhau qua hostname/service name hoặc URL công khai đã cấu hình.

## 2. Điều Kiện Để Chạy Được Với Nhau

Muốn 2 nhóm tải về rồi chạy mà vẫn kết nối được, cần đồng bộ các giá trị sau:

- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:Key`
- `InternalApi:Key`

Nếu 4 giá trị này khác nhau, đăng nhập và các API nội bộ sẽ lỗi.

## 3. Cách Chạy Đúng Khi Cùng Một Máy

Đây là cách đơn giản nhất.

### 3.1 Cài đặt

- Cài `Docker Desktop`
- Cài `Git`
- Có `.NET SDK` nếu muốn chạy trực tiếp bằng `dotnet run`
- SQL Server phải có sẵn hoặc dùng luôn container trong `docker compose`

### 3.2 Chạy toàn bộ hệ thống bằng Docker

Từ thư mục gốc dự án:

```powershell
docker compose up -d --build
```

Lệnh này sẽ bật:

- SQL Server
- `CatalogService`
- `CirculationService`
- `IdentityReportService`
- `ApiGateway`
- Frontend

### 3.3 Nếu chỉ chạy từng service

Chỉ dùng khi muốn test riêng từng phần:

```powershell
docker compose up -d --build sqlserver
docker compose up -d --build catalogservice
docker compose up -d --build circulationservice
docker compose up -d --build identityreportservice
docker compose up -d --build apigateway
docker compose up -d --build frontend
```

## 4. Khi Mỗi Nhóm Chạy Riêng

Nếu 2 nhóm chạy trên 2 máy khác nhau, các giá trị `localhost` trong `appsettings.json` sẽ không còn đúng nữa.

### 4.1 Nhóm `CatalogService`

Nhóm này phải sửa:

- `ConnectionStrings:CatalogDb`
- `IntegrationEvents:Subscribers:book.availability.changed`

Ví dụ:

```json
"ConnectionStrings": {
  "CatalogDb": "Server=YOUR_SQL_SERVER;Database=CatalogDb;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False"
},
"IntegrationEvents": {
  "Subscribers": {
    "book.availability.changed": [
      "http://YOUR_CIRCULATION_HOST:5002/integration/events/book-availability-changed"
    ]
  }
}
```

### 4.2 Nhóm `CirculationService`

Nhóm này phải sửa:

- `ConnectionStrings:CirculationDb`
- `CatalogService:BaseUrl`
- `IdentityService:BaseUrl`
- `IntegrationEvents:Subscribers`

Ví dụ:

```json
"CatalogService": {
  "BaseUrl": "http://YOUR_CATALOG_HOST:5001"
},
"IdentityService": {
  "BaseUrl": "http://YOUR_IDENTITY_HOST:5003"
},
"IntegrationEvents": {
  "Subscribers": {
    "book.borrowed": [
      "http://YOUR_IDENTITY_HOST:5003/integration/events/book-borrowed"
    ],
    "book.returned": [
      "http://YOUR_IDENTITY_HOST:5003/integration/events/book-returned"
    ],
    "fine.paid": [
      "http://YOUR_IDENTITY_HOST:5003/integration/events/fine-paid"
    ]
  }
}
```

### 4.3 Nhóm `IdentityReportService`

Nhóm này chỉ cần:

- `ConnectionStrings:IdentityDb`
- đồng bộ JWT và `InternalApi:Key`

## 5. Database Cần Có

Mỗi service dùng DB riêng:

- `CatalogDb`
- `CirculationDb`
- `IdentityDb`

Nếu không dùng DB container của dự án, hãy chạy 3 file SQL trong thư mục `database/`:

- `database/CatalogDb.sql`
- `database/CirculationDb.sql`
- `database/IdentityDb.sql`

Thứ tự gợi ý:

1. Tạo database
2. Chạy script seed
3. Kiểm tra có bảng và dữ liệu mẫu

## 6. Các Cổng Mặc Định

Khi chạy local theo cấu hình hiện tại:

- `ApiGateway`: `http://localhost:5000`
- `CatalogService`: `http://localhost:5001`
- `CirculationService`: `http://localhost:5002`
- `IdentityReportService`: `http://localhost:5003`

Nếu chạy bằng Docker Compose, các service nội bộ sẽ gọi nhau bằng:

- `catalogservice`
- `circulationservice`
- `identityreportservice`

## 7. Điều Bắt Buộc Khi Chia Repo Riêng

Khi tách repo, 2 nhóm cần giữ các phần sau giống nhau:

- cùng giá trị JWT
- cùng `InternalApi:Key`
- cùng tên database
- cùng endpoint nội bộ
- cùng format DTO / contracts

Nếu 1 nhóm đổi cấu trúc API mà không báo nhóm còn lại, hệ thống sẽ lệch ngay.

## 8. Checklist Trước Khi Nộp

### CatalogService

- [ ] Tạo/sửa/xóa sách hoạt động
- [ ] Tạo/sửa/xóa thể loại hoạt động
- [ ] Import sách online hoạt động
- [ ] API tồn kho và event sang Circulation chạy được

### CirculationService

- [ ] Mượn sách hoạt động
- [ ] Trả sách hoạt động
- [ ] Gia hạn hoạt động
- [ ] Rule mượn sách lưu đúng
- [ ] Phạt và thanh toán phạt hoạt động

### IdentityReportService

- [ ] Đăng nhập hoạt động
- [ ] Quản lý tài khoản hoạt động
- [ ] Xem hồ sơ độc giả hoạt động
- [ ] Báo cáo và dashboard có dữ liệu

## 9. Cách Kiểm Tra Nhanh

Sau khi chạy xong:

```powershell
docker compose ps
```

Kiểm tra từng service:

```powershell
Invoke-WebRequest http://localhost:5000 -UseBasicParsing
Invoke-WebRequest http://localhost:5001 -UseBasicParsing
Invoke-WebRequest http://localhost:5002 -UseBasicParsing
Invoke-WebRequest http://localhost:5003 -UseBasicParsing
```

## 10. Nếu Muốn Public Cho Người Khác Vào Test

Khuyến nghị:

- dùng `docker compose -f docker-compose.public.yml up -d --build`
- hoặc dùng Cloudflare Tunnel nếu muốn URL cố định

Khi public, không dùng `localhost` giữa các máy. Hãy dùng hostname hoặc domain thật.

## 11. Kết Luận

Nếu 2 nhóm chạy cùng bộ cấu hình và cùng môi trường Docker, các service sẽ gọi được nhau bình thường.
Nếu chạy riêng từng máy, phải thay `localhost` bằng IP/domain truy cập được từ service còn lại.

