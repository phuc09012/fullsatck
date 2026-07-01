namespace IdentityReportService.Models;

public class ReaderProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string LibraryCardNumber { get; set; } = string.Empty;
    public DateTimeOffset ExpiredAtUtc { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
