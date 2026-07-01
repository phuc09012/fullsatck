namespace Shared.Contracts;

public static class IntegrationEventNames
{
    public const string BookAvailabilityChanged = "book.availability.changed";
    public const string BookBorrowed = "book.borrowed";
    public const string BookReturned = "book.returned";
    public const string ReaderCreated = "reader.created";
}
