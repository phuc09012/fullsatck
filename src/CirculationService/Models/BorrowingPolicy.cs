using CirculationService.Options;

namespace CirculationService.Models;

public class BorrowingPolicy
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public int MaxActiveBorrowingsPerReader { get; set; } = 5;
    public int DefaultBorrowDays { get; set; } = 14;
    public int MaxRenewalDays { get; set; } = 30;
    public decimal FinePerOverdueDay { get; set; } = 2000m;
    public bool AllowReaderSelfCheckout { get; set; } = true;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public static BorrowingPolicy FromOptions(BorrowingOptions options)
        => new()
        {
            MaxActiveBorrowingsPerReader = options.MaxActiveBorrowingsPerReader,
            DefaultBorrowDays = options.DefaultBorrowDays,
            MaxRenewalDays = options.MaxRenewalDays,
            FinePerOverdueDay = options.FinePerOverdueDay,
            AllowReaderSelfCheckout = options.AllowReaderSelfCheckout
        };
}
