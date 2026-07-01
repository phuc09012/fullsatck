using System.Net.Http.Json;
using Shared.Contracts;

namespace CirculationService.Services;

public class ReaderClient
{
    private readonly HttpClient _httpClient;

    public ReaderClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ReaderLookupResponse?> GetReaderAsync(Guid readerId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<ReaderLookupResponse>($"/api/internal/readers/{readerId}", cancellationToken);
}
