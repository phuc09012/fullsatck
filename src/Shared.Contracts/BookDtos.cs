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
    int MaxBorrowingsPerReader,
    string? CoverImageUrl,
    string? Description,
    string? Content = null);

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
    int MaxBorrowingsPerReader,
    bool CanBorrow,
    bool IsArchived,
    string? CoverImageUrl,
    string? Description,
    string? Content,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
