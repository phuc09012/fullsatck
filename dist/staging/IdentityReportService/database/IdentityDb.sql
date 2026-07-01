IF DB_ID(N'IdentityDb') IS NULL
BEGIN
    CREATE DATABASE [IdentityDb];
END
GO

USE [IdentityDb];
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        Email nvarchar(256) NOT NULL,
        PasswordHash nvarchar(512) NOT NULL,
        FullName nvarchar(256) NOT NULL,
        Role nvarchar(64) NOT NULL,
        IsActive bit NOT NULL,
        CreatedAtUtc datetimeoffset(7) NOT NULL,
        UpdatedAtUtc datetimeoffset(7) NOT NULL
    );

    CREATE UNIQUE INDEX IX_Users_Email ON dbo.Users(Email);
END
GO

IF OBJECT_ID(N'dbo.ReaderProfiles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReaderProfiles
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_ReaderProfiles PRIMARY KEY,
        UserId uniqueidentifier NOT NULL,
        LibraryCardNumber nvarchar(64) NOT NULL,
        ExpiredAtUtc datetimeoffset(7) NOT NULL,
        Status nvarchar(32) NOT NULL,
        CreatedAtUtc datetimeoffset(7) NOT NULL,
        UpdatedAtUtc datetimeoffset(7) NOT NULL
    );

    CREATE UNIQUE INDEX IX_ReaderProfiles_UserId ON dbo.ReaderProfiles(UserId);
    CREATE UNIQUE INDEX IX_ReaderProfiles_LibraryCardNumber ON dbo.ReaderProfiles(LibraryCardNumber);
END
GO

IF OBJECT_ID(N'dbo.BorrowingProjections', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BorrowingProjections
    (
        BorrowingId uniqueidentifier NOT NULL CONSTRAINT PK_BorrowingProjections PRIMARY KEY,
        ReaderId uniqueidentifier NOT NULL,
        BookId uniqueidentifier NOT NULL,
        BookTitle nvarchar(256) NOT NULL,
        BorrowedAtUtc datetimeoffset(7) NOT NULL,
        DueAtUtc datetimeoffset(7) NOT NULL,
        ReturnedAtUtc datetimeoffset(7) NULL,
        FineAmount decimal(18,2) NOT NULL,
        Status int NOT NULL,
        UpdatedAtUtc datetimeoffset(7) NOT NULL
    );

    CREATE INDEX IX_BorrowingProjections_ReaderId_Status ON dbo.BorrowingProjections(ReaderId, Status);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = 'admin@library.local')
BEGIN
    INSERT INTO dbo.Users
    (Id, Email, PasswordHash, FullName, Role, IsActive, CreatedAtUtc, UpdatedAtUtc)
    VALUES
    ('11111111-1111-1111-1111-111111111101', 'admin@library.local', 'AQAAAAIAAYagAAAAEJTGOTlKN6h4HUCSxmM2ZZCYszYBIAr9lUosKobpw0SOo4rOAeYbtZF4nvOdvfp/0g==', 'Library Admin', 'Admin', 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('11111111-1111-1111-1111-111111111102', 'librarian@library.local', 'AQAAAAIAAYagAAAAELxZeShf8nFJbwJfyHgDS2itmcTTnYWb9MiUctUMQK6eYB7UKAUiV+I9xe/i2F9EFQ==', 'Library Librarian', 'Librarian', 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('11111111-1111-1111-1111-111111111201', 'reader1@library.local', 'AQAAAAIAAYagAAAAECbJKBa/MwgwqbVSf+GuCpaVDlnAUvUqtRidVmaGWpPhxdXsTqBhJkL8x0Y57AekCw==', 'Nguyen Van An', 'Reader', 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('11111111-1111-1111-1111-111111111202', 'reader2@library.local', 'AQAAAAIAAYagAAAAECbJKBa/MwgwqbVSf+GuCpaVDlnAUvUqtRidVmaGWpPhxdXsTqBhJkL8x0Y57AekCw==', 'Tran Thi Bich', 'Reader', 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('11111111-1111-1111-1111-111111111203', 'reader3@library.local', 'AQAAAAIAAYagAAAAECbJKBa/MwgwqbVSf+GuCpaVDlnAUvUqtRidVmaGWpPhxdXsTqBhJkL8x0Y57AekCw==', 'Le Minh Khoa', 'Reader', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ReaderProfiles WHERE UserId = '11111111-1111-1111-1111-111111111201')
BEGIN
    INSERT INTO dbo.ReaderProfiles
    (Id, UserId, LibraryCardNumber, ExpiredAtUtc, Status, CreatedAtUtc, UpdatedAtUtc)
    VALUES
    (NEWID(), '11111111-1111-1111-1111-111111111201', 'CARD-READER-001', DATEADD(YEAR, 1, SYSUTCDATETIME()), 'Active', SYSUTCDATETIME(), SYSUTCDATETIME()),
    (NEWID(), '11111111-1111-1111-1111-111111111202', 'CARD-READER-002', DATEADD(YEAR, 1, SYSUTCDATETIME()), 'Active', SYSUTCDATETIME(), SYSUTCDATETIME()),
    (NEWID(), '11111111-1111-1111-1111-111111111203', 'CARD-READER-003', DATEADD(YEAR, 1, SYSUTCDATETIME()), 'Active', SYSUTCDATETIME(), SYSUTCDATETIME());
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.BorrowingProjections WHERE BorrowingId = '33333333-3333-3333-3333-333333333301')
BEGIN
    INSERT INTO dbo.BorrowingProjections
    (BorrowingId, ReaderId, BookId, BookTitle, BorrowedAtUtc, DueAtUtc, ReturnedAtUtc, FineAmount, Status, UpdatedAtUtc)
    VALUES
    ('33333333-3333-3333-3333-333333333301', '11111111-1111-1111-1111-111111111201', '22222222-2222-2222-2222-222222222201', 'Clean Code', DATEADD(DAY, -2, SYSUTCDATETIME()), DATEADD(DAY, 12, SYSUTCDATETIME()), NULL, 0, 1, DATEADD(DAY, -2, SYSUTCDATETIME())),
    ('33333333-3333-3333-3333-333333333302', '11111111-1111-1111-1111-111111111202', '22222222-2222-2222-2222-222222222202', 'Head First Design Patterns', DATEADD(DAY, -20, SYSUTCDATETIME()), DATEADD(DAY, -5, SYSUTCDATETIME()), NULL, 0, 3, DATEADD(DAY, -20, SYSUTCDATETIME())),
    ('33333333-3333-3333-3333-333333333303', '11111111-1111-1111-1111-111111111203', '22222222-2222-2222-2222-222222222203', 'ASP.NET Core in Action', DATEADD(DAY, -14, SYSUTCDATETIME()), DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, -1, SYSUTCDATETIME()), 0, 2, DATEADD(DAY, -1, SYSUTCDATETIME()));
END
GO
