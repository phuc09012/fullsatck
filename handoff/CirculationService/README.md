# CirculationService Handoff

## Mục đích

Service này quản lý mượn trả sách, tính hạn trả, và cập nhật snapshot sách từ Catalog.

## Database cần kết nối

- Database name: `CirculationDb`
- SQL Server mặc định: `localhost,1433`
- Tài khoản mặc định trong repo:
  - `sa`
  - password: `Your_password123`

## Cách dựng DB ngoài

1. Mở SQL Server Management Studio hoặc Azure Data Studio.
2. Kết nối vào SQL Server của bạn.
3. Chạy file [`database/CirculationDb.sql`](../../database/CirculationDb.sql).
4. Kiểm tra đã có database `CirculationDb`, bảng `BorrowingRecords` và `CatalogBookSnapshots`.

## Cấu hình kết nối

File cần quan tâm:

- [`src/CirculationService/appsettings.json`](../../src/CirculationService/appsettings.json)

Chuỗi cấu hình mẫu:

```json
"ConnectionStrings": {
  "CirculationDb": "Server=localhost,1433;Database=CirculationDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;Encrypt=False"
},
"CatalogService": {
  "BaseUrl": "http://localhost:5001"
}
```

Các cấu hình cần thống nhất với nhóm khác:

```json
"Jwt": {
  "Issuer": "LibraryAuth",
  "Audience": "LibraryUsers",
  "Key": "ChangeThisKeyToSomethingAtLeast32CharsLong!"
},
"InternalApi": {
  "Key": "LibraryInternalSecretChangeMe!"
}
```

`InternalApi:Key` dùng để gọi Catalog/Identity và để nhận event nội bộ.

## Chạy local

Từ thư mục gốc project:

```bash
dotnet restore
dotnet run --project src/CirculationService/CirculationService.csproj
```

## Chạy bằng Docker

```bash
docker build -f src/CirculationService/Dockerfile -t circulationservice .
docker run --rm -p 5002:8080 circulationservice
```

Nếu chạy Docker với SQL Server ngoài, truyền các biến môi trường:

```bash
ConnectionStrings__CirculationDb=Server=YOUR_SQL_SERVER;Database=CirculationDb;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False
CatalogService__BaseUrl=http://YOUR_CATALOG_HOST:5001
```

## Phụ thuộc

- Cần `CatalogService` đang chạy để kiểm tra trạng thái sách và trừ/cộng tồn kho.
- Cần `IdentityReportService` để kiểm tra độc giả trước khi mượn.
- Khi chạy full hệ thống, service này gửi event `book.borrowed`, `book.returned`, `fine.paid` sang `IdentityReportService`.

## Endpoint chính

- `GET /api/borrowings` - Admin/Librarian
- `GET /api/borrowings/{id}`
- `GET /api/borrowings/reader/{readerId}`
- `GET /api/borrowings/overdue` - Admin/Librarian
- `GET /api/borrowings/summary`
- `GET /api/borrowings/fines`
- `POST /api/borrowings`
- `POST /api/borrowings/{id}/renew`
- `POST /api/borrowings/{id}/return`
- `POST /api/borrowings/{id}/fine-payment` - Admin/Librarian
- `GET /api/circulation-rules` - Admin/Librarian
- `PUT /api/circulation-rules` - Admin
- `POST /integration/events/book-availability-changed` - internal API key

## Dữ liệu mẫu

Script `CirculationDb.sql` đã có snapshot và 3 giao dịch mẫu để test dashboard, overdue và return flow.
