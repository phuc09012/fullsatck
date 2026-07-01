namespace Shared.Contracts;

public sealed record BookAvailabilityChangedEvent(
    Guid BookId,
    string Title,
    int AvailableCopies,
    int TotalCopies,
    bool CanBorrow,
    DateTimeOffset ChangedAtUtc);
