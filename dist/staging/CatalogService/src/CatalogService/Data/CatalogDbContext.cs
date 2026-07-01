using CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<Book> Books => Set<Book>();

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
            entity.Property(x => x.CoverImageUrl).HasMaxLength(512);
            entity.Property(x => x.Description).HasMaxLength(2000);
        });
    }
}
