using IdentityReportService.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace IdentityReportService.Data;

public static class IdentityDbSeeder
{
    public static async Task SeedAsync(IdentityDbContext context, IPasswordHasher<AppUser> passwordHasher, CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.BorrowingProjections', 'FinePaidAmount') IS NULL
            BEGIN
                ALTER TABLE dbo.BorrowingProjections
                ADD FinePaidAmount decimal(18,2) NOT NULL
                    CONSTRAINT DF_BorrowingProjections_FinePaidAmount DEFAULT(0);
            END;

            IF COL_LENGTH('dbo.BorrowingProjections', 'FinePaidAtUtc') IS NULL
            BEGIN
                ALTER TABLE dbo.BorrowingProjections
                ADD FinePaidAtUtc datetimeoffset NULL;
            END;
            """,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;

        var seedUsers = new[]
        {
            new AppUser
            {
                Id = DemoSeedData.Users.AdminId,
                Email = "admin@library.local",
                FullName = "Library Admin",
                Role = LibraryRoles.Admin
            },
            new AppUser
            {
                Id = DemoSeedData.Users.LibrarianId,
                Email = "librarian@library.local",
                FullName = "Library Librarian",
                Role = LibraryRoles.Librarian
            },
            new AppUser
            {
                Id = DemoSeedData.Users.Reader1Id,
                Email = "reader1@library.local",
                FullName = "Nguyen Van An",
                Role = LibraryRoles.Reader
            },
            new AppUser
            {
                Id = DemoSeedData.Users.Reader2Id,
                Email = "reader2@library.local",
                FullName = "Tran Thi Bich",
                Role = LibraryRoles.Reader
            },
            new AppUser
            {
                Id = DemoSeedData.Users.Reader3Id,
                Email = "reader3@library.local",
                FullName = "Le Minh Khoa",
                Role = LibraryRoles.Reader
            }
        };

        foreach (var user in seedUsers)
        {
            if (!await context.Users.AnyAsync(x => x.Email == user.Email, cancellationToken))
            {
                user.PasswordHash = user.Role switch
                {
                    LibraryRoles.Admin => passwordHasher.HashPassword(user, "Admin@123"),
                    LibraryRoles.Librarian => passwordHasher.HashPassword(user, "Librarian@123"),
                    _ => passwordHasher.HashPassword(user, "Reader@123")
                };

                context.Users.Add(user);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var profiles = new[]
        {
            new ReaderProfile
            {
                UserId = DemoSeedData.Users.Reader1Id,
                LibraryCardNumber = "CARD-READER-001",
                ExpiredAtUtc = now.AddYears(1),
                Status = "Active"
            },
            new ReaderProfile
            {
                UserId = DemoSeedData.Users.Reader2Id,
                LibraryCardNumber = "CARD-READER-002",
                ExpiredAtUtc = now.AddYears(1),
                Status = "Active"
            },
            new ReaderProfile
            {
                UserId = DemoSeedData.Users.Reader3Id,
                LibraryCardNumber = "CARD-READER-003",
                ExpiredAtUtc = now.AddYears(1),
                Status = "Active"
            }
        };

        foreach (var profile in profiles)
        {
            if (!await context.ReaderProfiles.AnyAsync(x => x.UserId == profile.UserId, cancellationToken))
            {
                context.ReaderProfiles.Add(profile);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var projections = new[]
        {
            new BorrowingProjection
            {
                BorrowingId = DemoSeedData.Borrowings.ActiveCleanCodeId,
                ReaderId = DemoSeedData.Users.Reader1Id,
                BookId = DemoSeedData.Books.CleanCodeId,
                BookTitle = "Clean Code",
                BorrowedAtUtc = now.AddDays(-2),
                DueAtUtc = now.AddDays(12),
                ReturnedAtUtc = null,
                FineAmount = 0,
                FinePaidAmount = 0,
                Status = BorrowStatus.Borrowed,
                UpdatedAtUtc = now.AddDays(-2)
            },
            new BorrowingProjection
            {
                BorrowingId = DemoSeedData.Borrowings.OverdueHeadFirstId,
                ReaderId = DemoSeedData.Users.Reader2Id,
                BookId = DemoSeedData.Books.HeadFirstDesignPatternsId,
                BookTitle = "Head First Design Patterns",
                BorrowedAtUtc = now.AddDays(-20),
                DueAtUtc = now.AddDays(-5),
                ReturnedAtUtc = null,
                FineAmount = 0,
                FinePaidAmount = 0,
                Status = BorrowStatus.Overdue,
                UpdatedAtUtc = now.AddDays(-20)
            },
            new BorrowingProjection
            {
                BorrowingId = DemoSeedData.Borrowings.ReturnedAspNetCoreId,
                ReaderId = DemoSeedData.Users.Reader3Id,
                BookId = DemoSeedData.Books.AspNetCoreInActionId,
                BookTitle = "ASP.NET Core in Action",
                BorrowedAtUtc = now.AddDays(-14),
                DueAtUtc = now.AddDays(-1),
                ReturnedAtUtc = now.AddDays(-1),
                FineAmount = 0,
                FinePaidAmount = 0,
                Status = BorrowStatus.Returned,
                UpdatedAtUtc = now.AddDays(-1)
            }
        };

        foreach (var projection in projections)
        {
            if (!await context.BorrowingProjections.AnyAsync(x => x.BorrowingId == projection.BorrowingId, cancellationToken))
            {
                context.BorrowingProjections.Add(projection);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
