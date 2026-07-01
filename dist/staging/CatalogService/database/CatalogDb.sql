IF DB_ID(N'CatalogDb') IS NULL
BEGIN
    CREATE DATABASE [CatalogDb];
END
GO

USE [CatalogDb];
GO

IF OBJECT_ID(N'dbo.Books', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Books
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Books PRIMARY KEY,
        Isbn nvarchar(32) NOT NULL,
        Title nvarchar(256) NOT NULL,
        Author nvarchar(256) NOT NULL,
        Publisher nvarchar(256) NOT NULL,
        PublishedYear int NOT NULL,
        Category nvarchar(128) NOT NULL,
        CoverImageUrl nvarchar(512) NULL,
        Description nvarchar(2000) NULL,
        TotalCopies int NOT NULL,
        AvailableCopies int NOT NULL,
        MinimumCopies int NOT NULL,
        IsArchived bit NOT NULL,
        CreatedAtUtc datetimeoffset(7) NOT NULL,
        UpdatedAtUtc datetimeoffset(7) NOT NULL
    );

    CREATE UNIQUE INDEX IX_Books_Isbn ON dbo.Books(Isbn);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE Id = '22222222-2222-2222-2222-222222222201')
BEGIN
    INSERT INTO dbo.Books
    (Id, Isbn, Title, Author, Publisher, PublishedYear, Category, CoverImageUrl, Description, TotalCopies, AvailableCopies, MinimumCopies, IsArchived, CreatedAtUtc, UpdatedAtUtc)
    VALUES
    ('22222222-2222-2222-2222-222222222201', '9780132350884', 'Clean Code', 'Robert C. Martin', 'Prentice Hall', 2008, 'Software Engineering', NULL, 'A handbook of agile software craftsmanship.', 12, 11, 2, 0, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('22222222-2222-2222-2222-222222222202', '9780596007126', 'Head First Design Patterns', 'Eric Freeman', 'O''Reilly Media', 2004, 'Software Engineering', NULL, 'A visual guide to classic design patterns.', 8, 7, 2, 0, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('22222222-2222-2222-2222-222222222203', '9781617298345', 'ASP.NET Core in Action', 'Andrew Lock', 'Manning', 2021, 'Web Development', NULL, 'A practical guide to building modern web apps.', 10, 10, 3, 0, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('22222222-2222-2222-2222-222222222204', '9780321125217', 'Domain-Driven Design', 'Eric Evans', 'Addison-Wesley', 2003, 'Architecture', NULL, 'Tackling complexity in the heart of software.', 6, 6, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('22222222-2222-2222-2222-222222222205', '9781617294545', 'Microservices Patterns', 'Chris Richardson', 'Manning', 2018, 'Architecture', NULL, 'Practical patterns for distributed systems.', 5, 5, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
END
GO
