namespace CatalogService.Models;

public class Book
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public int PublishedYear { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? Description { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public int MinimumCopies { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool CanBorrow => !IsArchived && AvailableCopies > 0;

    public void UpdateStockSnapshot()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public bool BorrowOne()
    {
        if (!CanBorrow)
        {
            return false;
        }

        AvailableCopies -= 1;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return true;
    }

    public bool ReturnOne()
    {
        if (AvailableCopies >= TotalCopies)
        {
            return false;
        }

        AvailableCopies += 1;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return true;
    }
}
