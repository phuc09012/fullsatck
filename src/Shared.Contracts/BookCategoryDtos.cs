namespace Shared.Contracts;

public sealed record BookCategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record BookCategoryCreateRequest(
    string Name,
    string? Description);

public sealed record BookCategoryUpdateRequest(
    string Name,
    string? Description,
    bool IsActive);
