namespace Shared.Contracts;

public sealed record BookUpsertRequest(
    string Isbn,
    string Title,
    string Author,
    string Publisher,
    int PublishedYear,
    string Category,
    int TotalCopies,
    int MinimumCopies,
    string? CoverImageUrl,
    string? Description);

public sealed record BookResponse(
    Guid Id,
    string Isbn,
    string Title,
    string Author,
    string Publisher,
    int PublishedYear,
    string Category,
    int TotalCopies,
    int AvailableCopies,
    int MinimumCopies,
    bool CanBorrow,
    bool IsArchived,
    string? CoverImageUrl,
    string? Description,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
