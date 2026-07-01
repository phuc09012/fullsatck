using System.Net.Http.Json;
using CirculationService.Options;
using CirculationService.Models;
using Microsoft.Extensions.Options;
using Shared.Contracts;

namespace CirculationService.Services;

public class IntegrationEventPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IntegrationEventPublisher> _logger;
    private readonly IntegrationEventDeliveryOptions _options;

    public IntegrationEventPublisher(
        HttpClient httpClient,
        IOptions<IntegrationEventDeliveryOptions> options,
        ILogger<IntegrationEventPublisher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public Task PublishBorrowedAsync(BorrowingRecord record, CancellationToken cancellationToken = default)
    {
        return PublishAsync(
            IntegrationEventNames.BookBorrowed,
            new BookBorrowedEvent(record.Id, record.BookId, record.ReaderId, record.BookTitle, record.BorrowedAtUtc, record.DueAtUtc),
            cancellationToken);
    }

    public Task PublishReturnedAsync(BorrowingRecord record, CancellationToken cancellationToken = default)
    {
        return PublishAsync(
            IntegrationEventNames.BookReturned,
            new BookReturnedEvent(record.Id, record.BookId, record.ReaderId, record.BookTitle, record.ReturnedAtUtc ?? DateTimeOffset.UtcNow, record.FineAmount),
            cancellationToken);
    }

    private async Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        if (!_options.Subscribers.TryGetValue(eventName, out var subscribers) || subscribers.Length == 0)
        {
            return;
        }

        foreach (var subscriber in subscribers)
        {
            try
            {
                using var response = await _httpClient.PostAsJsonAsync(subscriber, payload, cancellationToken);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deliver integration event {EventName} to {Subscriber}", eventName, subscriber);
            }
        }
    }
}
