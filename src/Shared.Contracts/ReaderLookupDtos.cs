namespace Shared.Contracts;

public sealed record ReaderLookupResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string Status,
    DateTimeOffset ExpiredAtUtc,
    bool IsActive);
