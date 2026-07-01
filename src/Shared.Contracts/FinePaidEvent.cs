namespace Shared.Contracts;

public sealed record FinePaidEvent(
    Guid BorrowingId,
    Guid ReaderId,
    Guid BookId,
    decimal FineAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    DateTimeOffset PaidAtUtc);
