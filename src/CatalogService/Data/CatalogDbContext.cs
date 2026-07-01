using CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookCategory> BookCategories => Set<BookCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Isbn).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => x.Isbn).IsUnique();
            entity.Property(x => x.Title).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Author).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Publisher).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(128).IsRequired();
            entity.Property(x => x.CoverImageUrl).HasColumnType("nvarchar(max)");
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.Content).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<BookCategory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Description).HasMaxLength(500);
        });
    }
}
