namespace CatalogService.Options;

public class IntegrationEventDeliveryOptions
{
    public Dictionary<string, string[]> Subscribers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
