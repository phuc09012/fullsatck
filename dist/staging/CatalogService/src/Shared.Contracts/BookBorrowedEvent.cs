namespace Shared.Contracts;

public sealed record BookBorrowedEvent(
    Guid BorrowingId,
    Guid BookId,
    Guid ReaderId,
    string BookTitle,
    DateTimeOffset BorrowedAtUtc,
    DateTimeOffset DueAtUtc);
