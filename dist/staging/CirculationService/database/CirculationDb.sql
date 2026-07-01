IF DB_ID(N'CirculationDb') IS NULL
BEGIN
    CREATE DATABASE [CirculationDb];
END
GO

USE [CirculationDb];
GO

IF OBJECT_ID(N'dbo.BorrowingRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BorrowingRecords
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_BorrowingRecords PRIMARY KEY,
        ReaderId uniqueidentifier NOT NULL,
        BookId uniqueidentifier NOT NULL,
        BookTitle nvarchar(256) NOT NULL,
        BorrowedAtUtc datetimeoffset(7) NOT NULL,
        DueAtUtc datetimeoffset(7) NOT NULL,
        ReturnedAtUtc datetimeoffset(7) NULL,
        Status int NOT NULL,
        FineAmount decimal(18,2) NOT NULL,
        CreatedAtUtc datetimeoffset(7) NOT NULL,
        UpdatedAtUtc datetimeoffset(7) NOT NULL
    );

    CREATE INDEX IX_BorrowingRecords_ReaderId_Status ON dbo.BorrowingRecords(ReaderId, Status);
END
GO

IF OBJECT_ID(N'dbo.CatalogBookSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CatalogBookSnapshots
    (
        BookId uniqueidentifier NOT NULL CONSTRAINT PK_CatalogBookSnapshots PRIMARY KEY,
        Title nvarchar(256) NOT NULL,
        AvailableCopies int NOT NULL,
        TotalCopies int NOT NULL,
        CanBorrow bit NOT NULL,
        UpdatedAtUtc datetimeoffset(7) NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.CatalogBookSnapshots WHERE BookId = '22222222-2222-2222-2222-222222222201')
BEGIN
    INSERT INTO dbo.CatalogBookSnapshots (BookId, Title, AvailableCopies, TotalCopies, CanBorrow, UpdatedAtUtc)
    VALUES
    ('22222222-2222-2222-2222-222222222201', 'Clean Code', 11, 12, 1, SYSUTCDATETIME()),
    ('22222222-2222-2222-2222-222222222202', 'Head First Design Patterns', 7, 8, 1, SYSUTCDATETIME()),
    ('22222222-2222-2222-2222-222222222203', 'ASP.NET Core in Action', 10, 10, 1, SYSUTCDATETIME()),
    ('22222222-2222-2222-2222-222222222204', 'Domain-Driven Design', 6, 6, 1, SYSUTCDATETIME()),
    ('22222222-2222-2222-2222-222222222205', 'Microservices Patterns', 5, 5, 1, SYSUTCDATETIME());
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.BorrowingRecords WHERE Id = '33333333-3333-3333-3333-333333333301')
BEGIN
    INSERT INTO dbo.BorrowingRecords
    (Id, ReaderId, BookId, BookTitle, BorrowedAtUtc, DueAtUtc, ReturnedAtUtc, Status, FineAmount, CreatedAtUtc, UpdatedAtUtc)
    VALUES
    ('33333333-3333-3333-3333-333333333301', '11111111-1111-1111-1111-111111111201', '22222222-2222-2222-2222-222222222201', 'Clean Code', DATEADD(DAY, -2, SYSUTCDATETIME()), DATEADD(DAY, 12, SYSUTCDATETIME()), NULL, 1, 0, DATEADD(DAY, -2, SYSUTCDATETIME()), DATEADD(DAY, -2, SYSUTCDATETIME())),
    ('33333333-3333-3333-3333-333333333302', '11111111-1111-1111-1111-111111111202', '22222222-2222-2222-2222-222222222202', 'Head First Design Patterns', DATEADD(DAY, -20, SYSUTCDATETIME()), DATEADD(DAY, -5, SYSUTCDATETIME()), NULL, 3, 0, DATEADD(DAY, -20, SYSUTCDATETIME()), DATEADD(DAY, -20, SYSUTCDATETIME())),
    ('33333333-3333-3333-3333-333333333303', '11111111-1111-1111-1111-111111111203', '22222222-2222-2222-2222-222222222203', 'ASP.NET Core in Action', DATEADD(DAY, -14, SYSUTCDATETIME()), DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, -1, SYSUTCDATETIME()), 2, 0, DATEADD(DAY, -14, SYSUTCDATETIME()), DATEADD(DAY, -1, SYSUTCDATETIME()));
END
GO
