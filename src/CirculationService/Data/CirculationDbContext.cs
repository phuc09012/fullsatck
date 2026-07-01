using CirculationService.Models;
using Microsoft.EntityFrameworkCore;

namespace CirculationService.Data;

public class CirculationDbContext : DbContext
{
    public CirculationDbContext(DbContextOptions<CirculationDbContext> options) : base(options)
    {
    }

    public DbSet<BorrowingRecord> BorrowingRecords => Set<BorrowingRecord>();
    public DbSet<CatalogBookSnapshot> CatalogBookSnapshots => Set<CatalogBookSnapshot>();
    public DbSet<BorrowingPolicy> BorrowingPolicies => Set<BorrowingPolicy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BorrowingRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BookTitle).HasMaxLength(256).IsRequired();
            entity.Property(x => x.FineAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.FinePaidAmount).HasColumnType("decimal(18,2)");
            entity.HasIndex(x => new { x.ReaderId, x.Status });
        });

        modelBuilder.Entity<CatalogBookSnapshot>(entity =>
        {
            entity.HasKey(x => x.BookId);
            entity.Property(x => x.Title).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<BorrowingPolicy>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FinePerOverdueDay).HasColumnType("decimal(18,2)");
        });
    }
}
