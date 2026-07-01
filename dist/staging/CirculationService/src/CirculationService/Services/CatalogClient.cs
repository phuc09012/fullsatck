using System.Net.Http.Json;
using Shared.Contracts;

namespace CirculationService.Services;

public class CatalogClient
{
    private readonly HttpClient _httpClient;

    public CatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<BookResponse?> GetBookAsync(Guid bookId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<BookResponse>($"/api/books/{bookId}", cancellationToken);

    public async Task<bool> BorrowBookAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"/api/books/{bookId}/borrow", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ReturnBookAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"/api/books/{bookId}/return", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
