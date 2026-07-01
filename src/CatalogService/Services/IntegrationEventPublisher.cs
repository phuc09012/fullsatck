using System.Net.Http.Json;
using CatalogService.Options;
using Microsoft.Extensions.Options;
using Shared.Contracts;

namespace CatalogService.Services;

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

    public async Task PublishAvailabilityChangedAsync(Models.Book book, CancellationToken cancellationToken = default)
    {
        var payload = new BookAvailabilityChangedEvent(
            book.Id,
            book.Title,
            book.AvailableCopies,
            book.TotalCopies,
            book.CanBorrow,
            DateTimeOffset.UtcNow);

        await PublishAsync(IntegrationEventNames.BookAvailabilityChanged, payload, cancellationToken);
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
