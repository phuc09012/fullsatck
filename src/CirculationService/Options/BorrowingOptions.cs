namespace CirculationService.Options;

public class BorrowingOptions
{
    public int MaxActiveBorrowingsPerReader { get; set; } = 5;
    public int DefaultBorrowDays { get; set; } = 14;
    public int MaxRenewalDays { get; set; } = 30;
    public decimal FinePerOverdueDay { get; set; } = 2000m;
    public bool AllowReaderSelfCheckout { get; set; } = true;
}
