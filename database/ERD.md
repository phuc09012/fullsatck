# Database ERD

Sơ đồ dưới đây bám theo đúng 3 file SQL hiện tại trong `database/`.

Lưu ý:

- Đây là kiến trúc microservices nên **không có foreign key chéo database**.
- Một số cột là **reference logic** hoặc **snapshot**, không phải ràng buộc SQL cứng.

## 1. CatalogDb

```mermaid
erDiagram
    BOOKS {
        uniqueidentifier Id PK
        nvarchar Isbn
        nvarchar Title
        nvarchar Author
        nvarchar Publisher
        int PublishedYear
        nvarchar Category
        nvarchar CoverImageUrl
        nvarchar Description
        nvarchar Content
        int TotalCopies
        int AvailableCopies
        int MinimumCopies
        bit IsArchived
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }

    BOOK_CATEGORIES {
        uniqueidentifier Id PK
        nvarchar Name
        nvarchar Description
        bit IsActive
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }
```

- `Books.Category` là text thường, dùng để hiển thị và tìm kiếm.
- `BookCategories` là bảng danh mục chuẩn hoá theo tên.
- Hiện tại **không có FK trực tiếp** giữa `Books` và `BookCategories`.

## 2. CirculationDb

```mermaid
erDiagram
    BORROWING_RECORDS {
        uniqueidentifier Id PK
        uniqueidentifier ReaderId
        uniqueidentifier BookId
        nvarchar BookTitle
        datetimeoffset BorrowedAtUtc
        datetimeoffset DueAtUtc
        datetimeoffset ReturnedAtUtc
        int Status
        decimal FineAmount
        decimal FinePaidAmount
        datetimeoffset FinePaidAtUtc
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }

    CATALOG_BOOK_SNAPSHOTS {
        uniqueidentifier BookId PK
        nvarchar Title
        int AvailableCopies
        int TotalCopies
        bit CanBorrow
        datetimeoffset UpdatedAtUtc
    }

    BORROWING_POLICIES {
        int Id PK
        int MaxActiveBorrowingsPerReader
        int DefaultBorrowDays
        int MaxRenewalDays
        decimal FinePerOverdueDay
        bit AllowReaderSelfCheckout
        datetimeoffset UpdatedAtUtc
    }
```

- `BorrowingRecords.ReaderId` là reference logic sang `IdentityDb`.
- `BorrowingRecords.BookId` là reference logic sang `CatalogDb`.
- `CatalogBookSnapshots` là bảng snapshot để lưu trạng thái sách phục vụ mượn/trả.
- `BorrowingPolicies` là bảng cấu hình nghiệp vụ, chỉ có 1 dòng chính.

## 3. IdentityDb

```mermaid
erDiagram
    USERS {
        uniqueidentifier Id PK
        nvarchar Email
        nvarchar PasswordHash
        nvarchar FullName
        nvarchar Role
        bit IsActive
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }

    READER_PROFILES {
        uniqueidentifier Id PK
        uniqueidentifier UserId
        nvarchar LibraryCardNumber
        datetimeoffset ExpiredAtUtc
        nvarchar Status
        datetimeoffset CreatedAtUtc
        datetimeoffset UpdatedAtUtc
    }

    BORROWING_PROJECTIONS {
        uniqueidentifier BorrowingId PK
        uniqueidentifier ReaderId
        uniqueidentifier BookId
        nvarchar BookTitle
        datetimeoffset BorrowedAtUtc
        datetimeoffset DueAtUtc
        datetimeoffset ReturnedAtUtc
        decimal FineAmount
        decimal FinePaidAmount
        datetimeoffset FinePaidAtUtc
        int Status
        datetimeoffset UpdatedAtUtc
    }
```

- `ReaderProfiles.UserId` là reference logic sang `Users.Id`.
- `BorrowingProjections` là bảng projection/báo cáo được cập nhật từ event mượn-trả.
- Đây không phải bảng nguồn gốc duy nhất của mượn/trả; dữ liệu gốc vẫn nằm ở `CirculationDb`.

## 4. Tóm tắt luồng dữ liệu

- `CatalogDb` giữ dữ liệu sách gốc.
- `CirculationDb` giữ giao dịch mượn/trả và luật mượn.
- `IdentityDb` giữ tài khoản, hồ sơ độc giả và projection báo cáo.
- Các service trao đổi qua API/event, không join SQL chéo database.

