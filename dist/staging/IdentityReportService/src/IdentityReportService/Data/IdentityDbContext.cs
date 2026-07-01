using IdentityReportService.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityReportService.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ReaderProfile> ReaderProfiles => Set<ReaderProfile>();
    public DbSet<BorrowingProjection> BorrowingProjections => Set<BorrowingProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<ReaderProfile>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.LibraryCardNumber).IsUnique();
            entity.Property(x => x.LibraryCardNumber).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<BorrowingProjection>(entity =>
        {
            entity.HasKey(x => x.BorrowingId);
            entity.Property(x => x.BookTitle).HasMaxLength(256).IsRequired();
            entity.Property(x => x.FineAmount).HasColumnType("decimal(18,2)");
            entity.HasIndex(x => new { x.ReaderId, x.Status });
        });
    }
}
