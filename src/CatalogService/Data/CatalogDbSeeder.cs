using CatalogService.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace CatalogService.Data;

public static class CatalogDbSeeder
{
    public static async Task SeedAsync(CatalogDbContext context, CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'dbo.BookCategories', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.BookCategories
                (
                    Id uniqueidentifier NOT NULL CONSTRAINT PK_BookCategories PRIMARY KEY,
                    Name nvarchar(128) NOT NULL,
                    Description nvarchar(500) NULL,
                    IsActive bit NOT NULL CONSTRAINT DF_BookCategories_IsActive DEFAULT(1),
                    CreatedAtUtc datetimeoffset NOT NULL CONSTRAINT DF_BookCategories_CreatedAtUtc DEFAULT(SYSDATETIMEOFFSET()),
                    UpdatedAtUtc datetimeoffset NOT NULL CONSTRAINT DF_BookCategories_UpdatedAtUtc DEFAULT(SYSDATETIMEOFFSET())
                );

                CREATE UNIQUE INDEX IX_BookCategories_Name ON dbo.BookCategories(Name);
            END;

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

            IF COL_LENGTH(N'dbo.Books', N'Content') IS NULL
            BEGIN
                ALTER TABLE dbo.Books ADD Content nvarchar(max) NULL;
            END

            IF COL_LENGTH(N'dbo.Books', N'MaxBorrowingsPerReader') IS NULL
            BEGIN
                ALTER TABLE dbo.Books
                ADD MaxBorrowingsPerReader int NOT NULL
                    CONSTRAINT DF_Books_MaxBorrowingsPerReader DEFAULT(1);
            END
            """,
            cancellationToken);

        var books = new[]
        {
            new Book
            {
                Id = DemoSeedData.Books.CleanCodeId,
                Isbn = "9780132350884",
                Title = "Clean Code",
                Author = "Robert C. Martin",
                Publisher = "Prentice Hall",
                PublishedYear = 2008,
                Category = "Software Engineering",
                TotalCopies = 12,
                AvailableCopies = 11,
                MinimumCopies = 2,
                MaxBorrowingsPerReader = 2,
                Description = "A handbook of agile software craftsmanship.",
                Content = "Imported demo note: this record stores metadata and a short introduction only, not the full copyrighted book text."
            },
            new Book
            {
                Id = DemoSeedData.Books.HeadFirstDesignPatternsId,
                Isbn = "9780596007126",
                Title = "Head First Design Patterns",
                Author = "Eric Freeman",
                Publisher = "O'Reilly Media",
                PublishedYear = 2004,
                Category = "Software Engineering",
                TotalCopies = 8,
                AvailableCopies = 7,
                MinimumCopies = 2,
                MaxBorrowingsPerReader = 2,
                Description = "A visual guide to classic design patterns.",
                Content = "Imported demo note: use the online import feature to enrich catalog records with cover, description, and source links."
            },
            new Book
            {
                Id = DemoSeedData.Books.AspNetCoreInActionId,
                Isbn = "9781617298345",
                Title = "ASP.NET Core in Action",
                Author = "Andrew Lock",
                Publisher = "Manning",
                PublishedYear = 2021,
                Category = "Web Development",
                TotalCopies = 10,
                AvailableCopies = 10,
                MinimumCopies = 3,
                MaxBorrowingsPerReader = 3,
                Description = "A practical guide to building modern web apps.",
                Content = "This content field is intended for summaries, librarian notes, and public preview metadata."
            },
            new Book
            {
                Id = DemoSeedData.Books.DomainDrivenDesignId,
                Isbn = "9780321125217",
                Title = "Domain-Driven Design",
                Author = "Eric Evans",
                Publisher = "Addison-Wesley",
                PublishedYear = 2003,
                Category = "Architecture",
                TotalCopies = 6,
                AvailableCopies = 6,
                MinimumCopies = 1,
                MaxBorrowingsPerReader = 1,
                Description = "Tackling complexity in the heart of software.",
                Content = "Use this field for a catalog introduction or teaching note instead of full book chapters."
            },
            new Book
            {
                Id = DemoSeedData.Books.MicroservicesPatternsId,
                Isbn = "9781617294545",
                Title = "Microservices Patterns",
                Author = "Chris Richardson",
                Publisher = "Manning",
                PublishedYear = 2018,
                Category = "Architecture",
                TotalCopies = 5,
                AvailableCopies = 5,
                MinimumCopies = 1,
                MaxBorrowingsPerReader = 1,
                Description = "Practical patterns for distributed systems.",
                Content = "Metadata can be imported from open web APIs, then reviewed by staff before saving."
            }
        };

        foreach (var book in books)
        {
            if (!await context.Books.AnyAsync(x => x.Id == book.Id || x.Isbn == book.Isbn, cancellationToken))
            {
                context.Books.Add(book);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var categories = books
            .Select(x => x.Category)
            .Concat(await context.Books.Select(x => x.Category).ToListAsync(cancellationToken))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeCategoryName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        foreach (var category in categories)
        {
            if (!await context.BookCategories.AnyAsync(x => x.Name == category, cancellationToken))
            {
                context.BookCategories.Add(new BookCategory
                {
                    Name = category,
                    Description = "Seeded from existing catalog data."
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeCategoryName(string value)
        => string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
