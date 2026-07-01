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
    }
}
