namespace Shared.Contracts;

public sealed record BookInventorySummaryResponse(
    int TotalBooks,
    int ArchivedBooks,
    int ActiveBooks,
    int BorrowedBooks,
    int LowStockBooks,
    int TotalCopies,
    int AvailableCopies,
    int BorrowedCopies);

public sealed record CirculationSummaryResponse(
    int TotalBorrowings,
    int ActiveBorrowings,
    int ReturnedBorrowings,
    int OverdueBorrowings,
    decimal OutstandingDebt,
    decimal TotalFineCollected);

public sealed record ReaderDetailResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string LibraryCardNumber,
    DateTimeOffset ExpiredAtUtc,
    string Status,
    int TotalBorrowings,
    int ActiveBorrowings,
    int OverdueBorrowings,
    decimal OutstandingDebt);
