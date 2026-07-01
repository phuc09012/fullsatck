using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shared.Contracts;

namespace CatalogService.Services;

public class ExternalBookLookupService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalBookLookupService> _logger;

    public ExternalBookLookupService(HttpClient httpClient, ILogger<ExternalBookLookupService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExternalBookCandidateResponse>> SearchAsync(
        string? query,
        string? isbn,
        int limit,
        CancellationToken cancellationToken = default)
    {
        query = query?.Trim();
        isbn = isbn?.Trim();
        if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(isbn))
        {
            return [];
        }

        limit = Math.Clamp(limit, 1, 10);
        var searchUrl = BuildSearchUrl(query, isbn, limit);

        OpenLibrarySearchResponse? response;
        try
        {
            response = await _httpClient.GetFromJsonAsync<OpenLibrarySearchResponse>(
                searchUrl,
                JsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open Library search failed for {Query} / {Isbn}", query, isbn);
            return [];
        }

        if (response?.Docs is null || response.Docs.Length == 0)
        {
            return [];
        }

        var candidates = new List<ExternalBookCandidateResponse>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in response.Docs.Take(limit))
        {
            if (string.IsNullOrWhiteSpace(doc.Title))
            {
                continue;
            }

            var selectedIsbn = SelectIsbn(doc.Isbns);
            var sourceId = doc.Key ?? selectedIsbn ?? doc.Title;
            if (!seen.Add(sourceId))
            {
                continue;
            }

            var sourceUrl = doc.Key is null
                ? "https://openlibrary.org"
                : $"https://openlibrary.org{doc.Key}";
            var work = doc.Key is null
                ? null
                : await GetWorkAsync(doc.Key, cancellationToken);
            var description = Truncate(ReadDescription(work), 1800);
            if (string.IsNullOrWhiteSpace(description))
            {
                description = BuildFallbackDescription(doc);
            }

            var subjects = doc.Subjects?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray() ?? [];
            var suggestedCategory = SelectSuggestedCategory(subjects);
            var content = BuildContent(description, subjects, sourceUrl);

            candidates.Add(new ExternalBookCandidateResponse(
                Source: "Open Library",
                SourceId: sourceId,
                SourceUrl: sourceUrl,
                Isbn: selectedIsbn,
                Title: doc.Title.Trim(),
                Author: JoinFirst(doc.Authors, "Unknown author"),
                Publisher: JoinFirst(doc.Publishers, "Unknown publisher"),
                PublishedYear: doc.FirstPublishYear,
                SuggestedCategory: suggestedCategory,
                CoverImageUrl: doc.CoverId.HasValue
                    ? $"https://covers.openlibrary.org/b/id/{doc.CoverId.Value}-L.jpg"
                    : null,
                Description: description,
                Content: content));
        }

        return candidates;
    }

    private async Task<OpenLibraryWorkResponse?> GetWorkAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<OpenLibraryWorkResponse>(
                $"{key.TrimStart('/')}.json",
                JsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Open Library work details failed for {Key}", key);
            return null;
        }
    }

    private static string BuildSearchUrl(string? query, string? isbn, int limit)
    {
        var fields = Uri.EscapeDataString("key,title,author_name,publisher,first_publish_year,isbn,cover_i,subject");
        var search = string.IsNullOrWhiteSpace(isbn)
            ? $"q={Uri.EscapeDataString(query ?? string.Empty)}"
            : $"isbn={Uri.EscapeDataString(isbn)}";

        return $"search.json?{search}&limit={limit}&fields={fields}";
    }

    private static string? SelectIsbn(IReadOnlyList<string>? isbns)
    {
        if (isbns is null || isbns.Count == 0)
        {
            return null;
        }

        return isbns.FirstOrDefault(x => NormalizeIsbn(x).Length == 13)
            ?? isbns.FirstOrDefault(x => NormalizeIsbn(x).Length == 10)
            ?? isbns.FirstOrDefault();
    }

    private static string NormalizeIsbn(string isbn)
        => new(isbn.Where(char.IsLetterOrDigit).ToArray());

    private static string JoinFirst(IReadOnlyList<string>? values, string fallback)
    {
        if (values is null || values.Count == 0)
        {
            return fallback;
        }

        var joined = string.Join(", ", values.Where(x => !string.IsNullOrWhiteSpace(x)).Take(3));
        return string.IsNullOrWhiteSpace(joined) ? fallback : joined;
    }

    private static string? SelectSuggestedCategory(IReadOnlyList<string> subjects)
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Accessible book",
            "Protected DAISY",
            "In library",
            "Internet Archive Wishlist",
            "OverDrive",
            "Large type books"
        };

        return subjects
            .FirstOrDefault(x => !ignored.Contains(x) && x.Length <= 128);
    }

    private static string? ReadDescription(OpenLibraryWorkResponse? work)
    {
        if (work is null)
        {
            return null;
        }

        return ReadJsonText(work.Description) ?? ReadJsonText(work.FirstSentence);
    }

    private static string? ReadJsonText(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString()?.Trim();
        }

        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("value", out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()?.Trim();
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var parts = element.EnumerateArray()
                .Select(ReadJsonText)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(3);
            return string.Join(" ", parts).Trim();
        }

        return null;
    }

    private static string BuildFallbackDescription(OpenLibraryDoc doc)
    {
        var parts = new List<string>
        {
            doc.Title ?? "Untitled book"
        };

        var author = JoinFirst(doc.Authors, string.Empty);
        if (!string.IsNullOrWhiteSpace(author))
        {
            parts.Add($"by {author}");
        }

        if (doc.FirstPublishYear.HasValue)
        {
            parts.Add($"first published in {doc.FirstPublishYear.Value}");
        }

        return string.Join(", ", parts) + ".";
    }

    private static string BuildContent(string? description, IReadOnlyList<string> subjects, string sourceUrl)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(description))
        {
            parts.Add(description.Trim());
        }

        if (subjects.Count > 0)
        {
            parts.Add($"Subjects: {string.Join(", ", subjects.Take(8))}.");
        }

        parts.Add($"Source metadata: {sourceUrl}");
        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "...";
    }

    private sealed class OpenLibrarySearchResponse
    {
        [JsonPropertyName("docs")]
        public OpenLibraryDoc[] Docs { get; set; } = [];
    }

    private sealed class OpenLibraryDoc
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("author_name")]
        public string[]? Authors { get; set; }

        [JsonPropertyName("publisher")]
        public string[]? Publishers { get; set; }

        [JsonPropertyName("first_publish_year")]
        public int? FirstPublishYear { get; set; }

        [JsonPropertyName("isbn")]
        public string[]? Isbns { get; set; }

        [JsonPropertyName("cover_i")]
        public int? CoverId { get; set; }

        [JsonPropertyName("subject")]
        public string[]? Subjects { get; set; }
    }

    private sealed class OpenLibraryWorkResponse
    {
        [JsonPropertyName("description")]
        public JsonElement Description { get; set; }

        [JsonPropertyName("first_sentence")]
        public JsonElement FirstSentence { get; set; }
    }
}
