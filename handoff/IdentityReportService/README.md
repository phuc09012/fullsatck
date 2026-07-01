# IdentityReportService Handoff

## Mục đích

Service này quản lý đăng nhập, vai trò người dùng, hồ sơ độc giả và báo cáo.

## Database cần kết nối

- Database name: `IdentityDb`
- SQL Server mặc định: `localhost,1433`
- Tài khoản mặc định trong repo:
  - `sa`
  - password: `Your_password123`

## Cách dựng DB ngoài

1. Mở SQL Server Management Studio hoặc Azure Data Studio.
2. Kết nối vào SQL Server của bạn.
3. Chạy file [`database/IdentityDb.sql`](../../database/IdentityDb.sql).
4. Kiểm tra đã có database `IdentityDb`, bảng `Users`, `ReaderProfiles`, `BorrowingProjections`.

## Cấu hình kết nối

File cần quan tâm:

- [`src/IdentityReportService/appsettings.json`](../../src/IdentityReportService/appsettings.json)

Chuỗi cấu hình mẫu:

```json
"ConnectionStrings": {
  "IdentityDb": "Server=localhost,1433;Database=IdentityDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;Encrypt=False"
},
"Jwt": {
  "Issuer": "LibraryAuth",
  "Audience": "LibraryUsers",
  "Key": "ChangeThisKeyToSomethingAtLeast32CharsLong!",
  "ExpiryMinutes": 480
},
"InternalApi": {
  "Key": "LibraryInternalSecretChangeMe!"
}
```

`InternalApi:Key` dùng để Circulation tra cứu độc giả và gửi event report projection.

## Chạy local

Từ thư mục gốc project:

```bash
dotnet restore
dotnet run --project src/IdentityReportService/IdentityReportService.csproj
```

## Chạy bằng Docker

```bash
docker build -f src/IdentityReportService/Dockerfile -t identityreportservice .
docker run --rm -p 5003:8080 identityreportservice
```

Nếu chạy Docker với SQL Server ngoài, truyền biến môi trường:

```bash
ConnectionStrings__IdentityDb=Server=YOUR_SQL_SERVER;Database=IdentityDb;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False
Jwt__Issuer=LibraryAuth
Jwt__Audience=LibraryUsers
Jwt__Key=ChangeThisKeyToSomethingAtLeast32CharsLong!
Jwt__ExpiryMinutes=480
```

## Tài khoản mẫu

- `admin@library.local` / `Admin@123`
- `librarian@library.local` / `Librarian@123`
- `reader1@library.local` / `Reader@123`
- `reader2@library.local` / `Reader@123`
- `reader3@library.local` / `Reader@123`

## Phụ thuộc

- Chỉ cần SQL Server.
- Service này phát hành JWT và nhận event từ Circulation để cập nhật báo cáo.

## Endpoint chính

- `POST /api/auth/login`
- `POST /api/auth/register`
- `GET /api/auth/me`
- `GET /api/users` - Admin
- `POST /api/users` - Admin
- `PUT /api/users/{id}/status` - Admin
- `PUT /api/users/{id}/role` - Admin
- `GET /api/readers` - Admin/Librarian
- `PUT /api/readers/{id}/status` - Admin/Librarian
- `PUT /api/readers/{id}/expiry` - Admin/Librarian
- `GET /api/reports/dashboard` - Admin/Librarian
- `GET /api/reports/reader/{readerId}` - Admin/Librarian
- `GET /api/reports/overdue-readers` - Admin/Librarian
- `GET /api/internal/readers/{id}` - internal API key
- `POST /integration/events/book-borrowed` - internal API key
- `POST /integration/events/book-returned` - internal API key
- `POST /integration/events/fine-paid` - internal API key

## Dữ liệu mẫu

Script `IdentityDb.sql` đã có sẵn accounts, reader profiles và projections mẫu.
