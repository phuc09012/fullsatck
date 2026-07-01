using Shared.Contracts;

namespace IdentityReportService.Models;

public class BorrowingProjection
{
    public Guid BorrowingId { get; set; }
    public Guid ReaderId { get; set; }
    public Guid BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public DateTimeOffset BorrowedAtUtc { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public DateTimeOffset? ReturnedAtUtc { get; set; }
    public decimal FineAmount { get; set; }
    public BorrowStatus Status { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool IsActive => Status == BorrowStatus.Borrowed || Status == BorrowStatus.Overdue;
}
