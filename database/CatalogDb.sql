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
        CoverImageUrl nvarchar(max) NULL,
        Description nvarchar(2000) NULL,
        Content nvarchar(max) NULL,
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

IF EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    WHERE t.name = N'Books'
      AND c.name = N'CoverImageUrl'
      AND c.max_length <> -1
)
BEGIN
    ALTER TABLE dbo.Books ALTER COLUMN CoverImageUrl nvarchar(max) NULL;
END
GO

IF COL_LENGTH(N'dbo.Books', N'Content') IS NULL
BEGIN
    ALTER TABLE dbo.Books ADD Content nvarchar(max) NULL;
END
GO

IF OBJECT_ID(N'dbo.BookCategories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BookCategories
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_BookCategories PRIMARY KEY,
        Name nvarchar(128) NOT NULL,
        Description nvarchar(500) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_BookCategories_IsActive DEFAULT(1),
        CreatedAtUtc datetimeoffset(7) NOT NULL,
        UpdatedAtUtc datetimeoffset(7) NOT NULL
    );

    CREATE UNIQUE INDEX IX_BookCategories_Name ON dbo.BookCategories(Name);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE Id = '22222222-2222-2222-2222-222222222201')
BEGIN
    INSERT INTO dbo.Books
    (Id, Isbn, Title, Author, Publisher, PublishedYear, Category, CoverImageUrl, Description, Content, TotalCopies, AvailableCopies, MinimumCopies, IsArchived, CreatedAtUtc, UpdatedAtUtc)
    VALUES
    ('22222222-2222-2222-2222-222222222201', '9780132350884', 'Clean Code', 'Robert C. Martin', 'Prentice Hall', 2008, 'Software Engineering', NULL, 'A handbook of agile software craftsmanship.', 'Demo note: store summaries, librarian notes, and imported metadata here, not full copyrighted book text.', 12, 11, 2, 0, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('22222222-2222-2222-2222-222222222202', '9780596007126', 'Head First Design Patterns', 'Eric Freeman', 'O''Reilly Media', 2004, 'Software Engineering', NULL, 'A visual guide to classic design patterns.', 'Demo note: the online import feature can enrich catalog records with cover, description, and source links.', 8, 7, 2, 0, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('22222222-2222-2222-2222-222222222203', '9781617298345', 'ASP.NET Core in Action', 'Andrew Lock', 'Manning', 2021, 'Web Development', NULL, 'A practical guide to building modern web apps.', 'This content field is intended for public preview metadata and internal catalog notes.', 10, 10, 3, 0, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('22222222-2222-2222-2222-222222222204', '9780321125217', 'Domain-Driven Design', 'Eric Evans', 'Addison-Wesley', 2003, 'Architecture', NULL, 'Tackling complexity in the heart of software.', 'Use this field for a catalog introduction or teaching note instead of full book chapters.', 6, 6, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('22222222-2222-2222-2222-222222222205', '9781617294545', 'Microservices Patterns', 'Chris Richardson', 'Manning', 2018, 'Architecture', NULL, 'Practical patterns for distributed systems.', 'Metadata can be imported from open web APIs, then reviewed by staff before saving.', 5, 5, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
END
GO

INSERT INTO dbo.BookCategories (Id, Name, Description, IsActive, CreatedAtUtc, UpdatedAtUtc)
SELECT NEWID(), source.Category, N'Seeded from existing catalog data.', 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
FROM (
    SELECT DISTINCT LTRIM(RTRIM(Category)) AS Category
    FROM dbo.Books
    WHERE Category IS NOT NULL
      AND LTRIM(RTRIM(Category)) <> N''
) AS source
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.BookCategories existing
    WHERE existing.Name = source.Category
);
GO
