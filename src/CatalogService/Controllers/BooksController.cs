using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CatalogService.Data;
using CatalogService.Models;
using CatalogService.Options;
using CatalogService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Contracts;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private const string StaffRoles = LibraryRoles.Admin + "," + LibraryRoles.Librarian;

    private readonly CatalogDbContext _context;
    private readonly IntegrationEventPublisher _publisher;
    private readonly ExternalBookLookupService _externalBookLookupService;
    private readonly InternalApiOptions _internalApiOptions;

    public BooksController(
        CatalogDbContext context,
        IntegrationEventPublisher publisher,
        ExternalBookLookupService externalBookLookupService,
        IOptions<InternalApiOptions> internalApiOptions)
    {
        _context = context;
        _publisher = publisher;
        _externalBookLookupService = externalBookLookupService;
        _internalApiOptions = internalApiOptions.Value;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookResponse>>> GetAll(
        [FromQuery] string? category,
        [FromQuery] int? publishedYear,
        [FromQuery] bool? availableOnly,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Books.AsQueryable();

        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim();
            query = query.Where(x => x.Category == normalizedCategory);
        }

        if (publishedYear.HasValue)
        {
            query = query.Where(x => x.PublishedYear == publishedYear.Value);
        }

        if (availableOnly == true)
        {
            query = query.Where(x => !x.IsArchived && x.AvailableCopies > 0);
        }

        var books = await query
            .OrderBy(x => x.Title)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(books);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await _context.BookCategories
            .Where(x => x.IsActive)
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }

    [HttpGet("archived")]
    public async Task<ActionResult<IEnumerable<BookResponse>>> GetArchived(CancellationToken cancellationToken)
    {
        var books = await _context.Books
            .Where(x => x.IsArchived)
            .OrderBy(x => x.Title)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(books);
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<IEnumerable<BookResponse>>> GetLowStock(CancellationToken cancellationToken)
    {
        var books = await _context.Books
            .Where(x => !x.IsArchived && x.AvailableCopies <= x.MinimumCopies)
            .OrderBy(x => x.AvailableCopies)
            .ThenBy(x => x.Title)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(books);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<BookInventorySummaryResponse>> GetSummary(CancellationToken cancellationToken)
    {
        var books = await _context.Books.ToListAsync(cancellationToken);
        var totalCopies = books.Sum(x => x.TotalCopies);
        var availableCopies = books.Sum(x => x.AvailableCopies);

        return Ok(new BookInventorySummaryResponse(
            TotalBooks: books.Count,
            ArchivedBooks: books.Count(x => x.IsArchived),
            ActiveBooks: books.Count(x => !x.IsArchived),
            BorrowedBooks: books.Count(x => !x.IsArchived && x.AvailableCopies < x.TotalCopies),
            LowStockBooks: books.Count(x => !x.IsArchived && x.AvailableCopies <= x.MinimumCopies),
            TotalCopies: totalCopies,
            AvailableCopies: availableCopies,
            BorrowedCopies: totalCopies - availableCopies));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var book = await _context.Books.FindAsync([id], cancellationToken);
        return book is null ? NotFound() : Ok(ToResponse(book));
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<BookResponse>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] int? publishedYear,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
    {
        keyword = keyword?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(keyword) && !publishedYear.HasValue)
        {
            return await GetAll(null, null, null, includeArchived, cancellationToken);
        }

        var wildcard = $"%{keyword}%";
        var query = _context.Books.AsQueryable();

        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        if (publishedYear.HasValue)
        {
            query = query.Where(x => x.PublishedYear == publishedYear.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                EF.Functions.Like(x.Title, wildcard) ||
                EF.Functions.Like(x.Author, wildcard) ||
                EF.Functions.Like(x.Publisher, wildcard) ||
                EF.Functions.Like(x.Category, wildcard) ||
                EF.Functions.Like(x.Isbn, wildcard) ||
                (x.Description != null && EF.Functions.Like(x.Description, wildcard)) ||
                (x.Content != null && EF.Functions.Like(x.Content, wildcard)));
        }

        var candidates = await query
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        var normalizedKeyword = NormalizeText(keyword);
        var tokens = TokenizeKeyword(keyword);

        var books = candidates
            .Select(book => new
            {
                Book = book,
                Score = ScoreBook(book, normalizedKeyword, tokens)
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Book.CanBorrow)
            .ThenBy(x => x.Book.IsArchived)
            .ThenBy(x => x.Book.Title)
            .Select(x => x.Book)
            .ToList();

        return Ok(books);
    }

    [HttpGet("import-search")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<IEnumerable<ExternalBookCandidateResponse>>> ImportSearch(
        [FromQuery] string? query,
        [FromQuery] string? isbn,
        [FromQuery] int limit = 6,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(isbn))
        {
            return BadRequest(new { message = "Query or ISBN is required." });
        }

        var candidates = await _externalBookLookupService.SearchAsync(query, isbn, limit, cancellationToken);
        return Ok(candidates);
    }

    [HttpPost]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<BookResponse>> Create(BookUpsertRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var normalizedIsbn = request.Isbn.Trim();
        if (await _context.Books.AnyAsync(x => x.Isbn == normalizedIsbn, cancellationToken))
        {
            return Conflict(new { message = "ISBN already exists." });
        }

        var category = await ResolveCategoryAsync(request.Category, cancellationToken);
        if (category is null)
        {
            return BadRequest(new { message = "Category must be created by an admin before assigning it to a book." });
        }

        var book = new Book
        {
            Isbn = normalizedIsbn,
            Title = request.Title.Trim(),
            Author = request.Author.Trim(),
            Publisher = request.Publisher.Trim(),
            PublishedYear = request.PublishedYear,
            Category = category.Name,
            TotalCopies = request.TotalCopies,
            AvailableCopies = request.TotalCopies,
            MinimumCopies = request.MinimumCopies,
            MaxBorrowingsPerReader = request.MaxBorrowingsPerReader,
            CoverImageUrl = request.CoverImageUrl?.Trim(),
            Description = request.Description?.Trim(),
            Content = request.Content?.Trim()
        };

        _context.Books.Add(book);
        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.PublishAvailabilityChangedAsync(book, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = book.Id }, ToResponse(book));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<BookResponse>> Update(Guid id, BookUpsertRequest request, CancellationToken cancellationToken)
    {
        var book = await _context.Books.FindAsync([id], cancellationToken);
        if (book is null)
        {
            return NotFound();
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var normalizedIsbn = request.Isbn.Trim();
        var isbnExists = await _context.Books.AnyAsync(x => x.Id != id && x.Isbn == normalizedIsbn, cancellationToken);
        if (isbnExists)
        {
            return Conflict(new { message = "ISBN already exists." });
        }

        var category = await ResolveCategoryAsync(request.Category, cancellationToken);
        if (category is null)
        {
            return BadRequest(new { message = "Category must be created by an admin before assigning it to a book." });
        }

        var borrowedCopies = book.TotalCopies - book.AvailableCopies;
        if (request.TotalCopies < borrowedCopies)
        {
            return BadRequest(new { message = "Total copies cannot be smaller than borrowed copies." });
        }

        book.Isbn = normalizedIsbn;
        book.Title = request.Title.Trim();
        book.Author = request.Author.Trim();
        book.Publisher = request.Publisher.Trim();
        book.PublishedYear = request.PublishedYear;
        book.Category = category.Name;
        book.TotalCopies = request.TotalCopies;
        book.MinimumCopies = request.MinimumCopies;
        book.MaxBorrowingsPerReader = request.MaxBorrowingsPerReader;
        book.CoverImageUrl = request.CoverImageUrl?.Trim();
        book.Description = request.Description?.Trim();
        book.Content = request.Content?.Trim();
        if (book.AvailableCopies > book.TotalCopies)
        {
            book.AvailableCopies = book.TotalCopies;
        }

        book.UpdateStockSnapshot();
        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.PublishAvailabilityChangedAsync(book, cancellationToken);

        return Ok(ToResponse(book));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var book = await _context.Books.FindAsync([id], cancellationToken);
        if (book is null)
        {
            return NotFound();
        }

        book.IsArchived = true;
        book.UpdateStockSnapshot();
        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.PublishAvailabilityChangedAsync(book, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}/permanent")]
    [Authorize(Roles = LibraryRoles.Admin)]
    public async Task<IActionResult> DeletePermanently(Guid id, CancellationToken cancellationToken)
    {
        var book = await _context.Books.FindAsync([id], cancellationToken);
        if (book is null)
        {
            return NotFound();
        }

        var borrowedCopies = book.TotalCopies - book.AvailableCopies;
        if (borrowedCopies > 0)
        {
            return BadRequest(new { message = "Cannot permanently delete a book while copies are borrowed. Archive it instead." });
        }

        _context.Books.Remove(book);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<BookResponse>> Restore(Guid id, CancellationToken cancellationToken)
    {
        var book = await _context.Books.FindAsync([id], cancellationToken);
        if (book is null)
        {
            return NotFound();
        }

        book.IsArchived = false;
        book.UpdateStockSnapshot();
        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.PublishAvailabilityChangedAsync(book, cancellationToken);

        return Ok(ToResponse(book));
    }

    [HttpPost("{id:guid}/borrow")]
    public async Task<ActionResult<BookResponse>> Borrow(Guid id, CancellationToken cancellationToken)
    {
        if (!IsInternalRequest())
        {
            return Unauthorized(new { message = "Internal API key is required." });
        }

        var book = await _context.Books.FindAsync([id], cancellationToken);
        if (book is null)
        {
            return NotFound();
        }

        if (!book.BorrowOne())
        {
            return BadRequest(new { message = "Book is not available for borrowing." });
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.PublishAvailabilityChangedAsync(book, cancellationToken);

        return Ok(ToResponse(book));
    }

    [HttpPost("{id:guid}/return")]
    public async Task<ActionResult<BookResponse>> Return(Guid id, CancellationToken cancellationToken)
    {
        if (!IsInternalRequest())
        {
            return Unauthorized(new { message = "Internal API key is required." });
        }

        var book = await _context.Books.FindAsync([id], cancellationToken);
        if (book is null)
        {
            return NotFound();
        }

        if (!book.ReturnOne())
        {
            return BadRequest(new { message = "Book already reached its total copy limit." });
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.PublishAvailabilityChangedAsync(book, cancellationToken);

        return Ok(ToResponse(book));
    }

    private static BookResponse ToResponse(Book book)
        => new(
            book.Id,
            book.Isbn,
            book.Title,
            book.Author,
            book.Publisher,
            book.PublishedYear,
            book.Category,
            book.TotalCopies,
            book.AvailableCopies,
            book.MinimumCopies,
            book.MaxBorrowingsPerReader,
            book.CanBorrow,
            book.IsArchived,
            book.CoverImageUrl,
            book.Description,
            book.Content,
            book.CreatedAtUtc,
            book.UpdatedAtUtc);

    private static string? ValidateRequest(BookUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Isbn))
        {
            return "ISBN is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "Title is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Author))
        {
            return "Author is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Publisher))
        {
            return "Publisher is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return "Category is required.";
        }

        if (request.TotalCopies <= 0)
        {
            return "Total copies must be greater than zero.";
        }

        if (request.MinimumCopies < 0)
        {
            return "Minimum copies cannot be negative.";
        }

        if (request.MinimumCopies > request.TotalCopies)
        {
            return "Minimum copies cannot be greater than total copies.";
        }

        if (request.MaxBorrowingsPerReader <= 0)
        {
            return "Max borrowings per reader must be greater than zero.";
        }

        if (request.Content?.Length > 20000)
        {
            return "Content cannot exceed 20,000 characters.";
        }

        return null;
    }

    private bool IsInternalRequest()
    {
        var expectedKey = _internalApiOptions.Key;
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            return false;
        }

        if (!Request.Headers.TryGetValue(InternalRequestHeaders.ApiKey, out var providedKey))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey.ToString());
        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private async Task<BookCategory?> ResolveCategoryAsync(string categoryName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return null;
        }

        var normalizedCategory = NormalizeCategoryName(categoryName);
        return await _context.BookCategories
            .SingleOrDefaultAsync(x => x.Name == normalizedCategory && x.IsActive, cancellationToken);
    }

    private static int ScoreBook(BookResponse book, string normalizedKeyword, IReadOnlyList<string> tokens)
    {
        var score = 0;

        score += ScoreField(book.Title, normalizedKeyword, tokens, 120);
        score += ScoreField(book.Author, normalizedKeyword, tokens, 80);
        score += ScoreField(book.Isbn, normalizedKeyword, tokens, 100);
        score += ScoreField(book.Publisher, normalizedKeyword, tokens, 60);
        score += ScoreField(book.Category, normalizedKeyword, tokens, 55);
        score += ScoreField(book.Description, normalizedKeyword, tokens, 35);
        score += ScoreField(book.Content, normalizedKeyword, tokens, 25);

        if (book.CanBorrow)
        {
            score += 10;
        }

        if (book.IsArchived)
        {
            score -= 25;
        }

        return score;
    }

    private static int ScoreField(string? value, string normalizedKeyword, IReadOnlyList<string> tokens, int exactWeight)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var normalizedValue = NormalizeText(value);
        var score = 0;

        if (normalizedValue == normalizedKeyword)
        {
            score += exactWeight;
        }

        if (normalizedValue.Contains(normalizedKeyword, StringComparison.Ordinal))
        {
            score += exactWeight / 2;
        }

        foreach (var token in tokens)
        {
            if (normalizedValue.Contains(token, StringComparison.Ordinal))
            {
                score += Math.Max(6, exactWeight / 5);
            }
        }

        return score;
    }

    private static IReadOnlyList<string> TokenizeKeyword(string keyword)
    {
        return NormalizeText(keyword)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeText(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeCategoryName(string value)
        => string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
