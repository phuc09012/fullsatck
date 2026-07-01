using Shared.Contracts;
using CirculationService.Models;
using Microsoft.EntityFrameworkCore;

namespace CirculationService.Data;

public static class CirculationDbSeeder
{
    public static async Task SeedAsync(CirculationDbContext context, CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        var snapshots = new[]
        {
            new CatalogBookSnapshot
            {
                BookId = DemoSeedData.Books.CleanCodeId,
                Title = "Clean Code",
                AvailableCopies = 11,
                TotalCopies = 12,
                CanBorrow = true,
                UpdatedAtUtc = now
            },
            new CatalogBookSnapshot
            {
                BookId = DemoSeedData.Books.HeadFirstDesignPatternsId,
                Title = "Head First Design Patterns",
                AvailableCopies = 7,
                TotalCopies = 8,
                CanBorrow = true,
                UpdatedAtUtc = now
            },
            new CatalogBookSnapshot
            {
                BookId = DemoSeedData.Books.AspNetCoreInActionId,
                Title = "ASP.NET Core in Action",
                AvailableCopies = 10,
                TotalCopies = 10,
                CanBorrow = true,
                UpdatedAtUtc = now
            },
            new CatalogBookSnapshot
            {
                BookId = DemoSeedData.Books.DomainDrivenDesignId,
                Title = "Domain-Driven Design",
                AvailableCopies = 6,
                TotalCopies = 6,
                CanBorrow = true,
                UpdatedAtUtc = now
            },
            new CatalogBookSnapshot
            {
                BookId = DemoSeedData.Books.MicroservicesPatternsId,
                Title = "Microservices Patterns",
                AvailableCopies = 5,
                TotalCopies = 5,
                CanBorrow = true,
                UpdatedAtUtc = now
            }
        };

        foreach (var snapshot in snapshots)
        {
            if (!await context.CatalogBookSnapshots.AnyAsync(x => x.BookId == snapshot.BookId, cancellationToken))
            {
                context.CatalogBookSnapshots.Add(snapshot);
            }
        }

        var borrowings = new[]
        {
            new BorrowingRecord
            {
                Id = DemoSeedData.Borrowings.ActiveCleanCodeId,
                ReaderId = DemoSeedData.Users.Reader1Id,
                BookId = DemoSeedData.Books.CleanCodeId,
                BookTitle = "Clean Code",
                BorrowedAtUtc = now.AddDays(-2),
                DueAtUtc = now.AddDays(12),
                ReturnedAtUtc = null,
                Status = Shared.Contracts.BorrowStatus.Borrowed,
                FineAmount = 0,
                CreatedAtUtc = now.AddDays(-2),
                UpdatedAtUtc = now.AddDays(-2)
            },
            new BorrowingRecord
            {
                Id = DemoSeedData.Borrowings.OverdueHeadFirstId,
                ReaderId = DemoSeedData.Users.Reader2Id,
                BookId = DemoSeedData.Books.HeadFirstDesignPatternsId,
                BookTitle = "Head First Design Patterns",
                BorrowedAtUtc = now.AddDays(-20),
                DueAtUtc = now.AddDays(-5),
                ReturnedAtUtc = null,
                Status = Shared.Contracts.BorrowStatus.Overdue,
                FineAmount = 0,
                CreatedAtUtc = now.AddDays(-20),
                UpdatedAtUtc = now.AddDays(-20)
            },
            new BorrowingRecord
            {
                Id = DemoSeedData.Borrowings.ReturnedAspNetCoreId,
                ReaderId = DemoSeedData.Users.Reader3Id,
                BookId = DemoSeedData.Books.AspNetCoreInActionId,
                BookTitle = "ASP.NET Core in Action",
                BorrowedAtUtc = now.AddDays(-14),
                DueAtUtc = now.AddDays(-1),
                ReturnedAtUtc = now.AddDays(-1),
                Status = Shared.Contracts.BorrowStatus.Returned,
                FineAmount = 0,
                CreatedAtUtc = now.AddDays(-14),
                UpdatedAtUtc = now.AddDays(-1)
            }
        };

        foreach (var borrowing in borrowings)
        {
            if (!await context.BorrowingRecords.AnyAsync(x => x.Id == borrowing.Id, cancellationToken))
            {
                context.BorrowingRecords.Add(borrowing);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
