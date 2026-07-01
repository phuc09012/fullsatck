# CatalogService Handoff

## Mục đích

Service này quản lý sách, thể loại, số lượng tồn kho, lưu trữ/khôi phục sách, import metadata sách online và phát sự kiện availability sang service khác.

## Database cần kết nối

- Database name: `CatalogDb`
- SQL Server mặc định: `localhost,1433`
- Tài khoản mặc định trong repo:
  - `sa`
  - password: `Your_password123`

## Cách dựng DB ngoài

1. Mở SQL Server Management Studio hoặc Azure Data Studio.
2. Kết nối vào SQL Server của bạn.
3. Chạy file [`database/CatalogDb.sql`](../../database/CatalogDb.sql).
4. Kiểm tra đã có database `CatalogDb`, bảng `Books` và `BookCategories`.

## Cấu hình kết nối

File cần quan tâm:

- [`src/CatalogService/appsettings.json`](../../src/CatalogService/appsettings.json)

Chuỗi kết nối mẫu:

```json
"ConnectionStrings": {
  "CatalogDb": "Server=localhost,1433;Database=CatalogDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;Encrypt=False"
}
```

Nếu SQL Server nằm máy khác, đổi `Server=` thành IP hoặc hostname của máy đó.

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

`InternalApi:Key` dùng để Circulation gọi endpoint trừ/cộng tồn kho và để Catalog gửi event sang Circulation.

## Chạy local

Từ thư mục gốc project:

```bash
dotnet restore
dotnet run --project src/CatalogService/CatalogService.csproj
```

Mặc định service chạy ở cổng `5001` qua gateway hoặc cổng do `launchSettings` cấu hình khi chạy trực tiếp.

## Chạy bằng Docker

```bash
docker build -f src/CatalogService/Dockerfile -t catalogservice .
docker run --rm -p 5001:8080 catalogservice
```

Nếu dùng SQL Server ngoài, nhớ sửa connection string trước khi build image hoặc truyền qua environment variable:

```bash
ConnectionStrings__CatalogDb=Server=YOUR_SQL_SERVER;Database=CatalogDb;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False
```

## Phụ thuộc

- Chỉ cần SQL Server.
- Khi chạy full hệ thống, service này gửi event `book.availability.changed` sang `CirculationService`.

## Endpoint chính

- `GET /api/books`
- `GET /api/books/search?keyword=...`
- `GET /api/books/import-search?query=...` - Admin/Librarian, lấy metadata/preview từ Open Library
- `GET /api/books/categories`
- `GET /api/books/summary`
- `GET /api/book-categories`
- `POST /api/book-categories` - Admin
- `PUT /api/book-categories/{id}` - Admin
- `DELETE /api/book-categories/{id}` - Admin
- `POST /api/books` - Admin/Librarian
- `PUT /api/books/{id}` - Admin/Librarian
- `DELETE /api/books/{id}` - Admin/Librarian
- `POST /api/books/{id}/restore` - Admin/Librarian
- `POST /api/books/{id}/borrow` - internal API key
- `POST /api/books/{id}/return` - internal API key

## Dữ liệu mẫu

Script `CatalogDb.sql` đã có sẵn 5 cuốn sách mẫu để team test ngay.

Trường `Content` dùng cho tóm tắt, ghi chú thủ thư, hoặc dữ liệu preview hợp lệ. Không lưu toàn văn sách có bản quyền nếu nguồn không cho phép.
