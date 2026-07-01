namespace CirculationService.Models;

public class CatalogBookSnapshot
{
    public Guid BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int AvailableCopies { get; set; }
    public int TotalCopies { get; set; }
    public bool CanBorrow { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
