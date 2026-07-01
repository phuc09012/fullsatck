namespace Shared.Contracts;

public sealed record ReaderCreatedEvent(
    Guid UserId,
    string Email,
    string FullName,
    DateTimeOffset CreatedAtUtc);
