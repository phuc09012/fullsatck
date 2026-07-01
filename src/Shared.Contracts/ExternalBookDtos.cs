namespace Shared.Contracts;

public sealed record ExternalBookCandidateResponse(
    string Source,
    string SourceId,
    string SourceUrl,
    string? Isbn,
    string Title,
    string Author,
    string Publisher,
    int? PublishedYear,
    string? SuggestedCategory,
    string? CoverImageUrl,
    string? Description,
    string? Content);
