namespace CirculationService.Contracts;

public sealed record BorrowRequest(
    Guid ReaderId,
    Guid BookId,
    int? BorrowDays);

public sealed record ReturnRequest(DateTimeOffset? ReturnedAtUtc);

public sealed record BorrowResponse(
    Guid BorrowingId,
    Guid ReaderId,
    Guid BookId,
    string BookTitle,
    DateTimeOffset BorrowedAtUtc,
    DateTimeOffset DueAtUtc,
    string Status);

public sealed record BorrowingRecordResponse(
    Guid Id,
    Guid ReaderId,
    Guid BookId,
    string BookTitle,
    DateTimeOffset BorrowedAtUtc,
    DateTimeOffset DueAtUtc,
    DateTimeOffset? ReturnedAtUtc,
    string Status,
    decimal FineAmount);

public sealed record CatalogAvailabilityEvent(
    Guid BookId,
    string Title,
    int AvailableCopies,
    int TotalCopies,
    bool CanBorrow,
    DateTimeOffset ChangedAtUtc);
