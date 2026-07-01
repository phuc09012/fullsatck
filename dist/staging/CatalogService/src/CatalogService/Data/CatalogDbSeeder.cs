using CatalogService.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace CatalogService.Data;

public static class CatalogDbSeeder
{
    public static async Task SeedAsync(CatalogDbContext context, CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);

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
                Description = "A handbook of agile software craftsmanship."
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
                Description = "A visual guide to classic design patterns."
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
                Description = "A practical guide to building modern web apps."
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
                Description = "Tackling complexity in the heart of software."
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
                Description = "Practical patterns for distributed systems."
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
    }
}
