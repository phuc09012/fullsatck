namespace Shared.Contracts;

public sealed record BookReturnedEvent(
    Guid BorrowingId,
    Guid BookId,
    Guid ReaderId,
    string BookTitle,
    DateTimeOffset ReturnedAtUtc,
    decimal FineAmount);
