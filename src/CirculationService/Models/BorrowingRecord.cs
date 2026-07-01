using Shared.Contracts;

namespace CirculationService.Models;

public class BorrowingRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReaderId { get; set; }
    public Guid BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public DateTimeOffset BorrowedAtUtc { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public DateTimeOffset? ReturnedAtUtc { get; set; }
    public BorrowStatus Status { get; set; }
    public decimal FineAmount { get; set; }
    public decimal FinePaidAmount { get; set; }
    public DateTimeOffset? FinePaidAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool IsActive => ReturnedAtUtc is null && (Status == BorrowStatus.Borrowed || Status == BorrowStatus.Overdue);
    public decimal OutstandingFine => Math.Max(0, FineAmount - FinePaidAmount);
}
