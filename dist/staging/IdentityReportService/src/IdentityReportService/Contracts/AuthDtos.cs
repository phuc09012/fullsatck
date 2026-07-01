namespace IdentityReportService.Contracts;

public sealed record RegisterRequest(string Email, string Password, string FullName);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);

public sealed record ReaderResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string LibraryCardNumber,
    DateTimeOffset ExpiredAtUtc,
    string Status);

public sealed record BorrowingProjectionResponse(
    Guid BorrowingId,
    Guid ReaderId,
    Guid BookId,
    string BookTitle,
    DateTimeOffset BorrowedAtUtc,
    DateTimeOffset DueAtUtc,
    DateTimeOffset? ReturnedAtUtc,
    string Status,
    decimal FineAmount);
