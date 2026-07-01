using CatalogService.Data;
using CatalogService.Models;
using CatalogService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly CatalogDbContext _context;
    private readonly IntegrationEventPublisher _publisher;

    public BooksController(CatalogDbContext context, IntegrationEventPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var books = await _context.Books
            .OrderBy(x => x.Title)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(books);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var book = await _context.Books.FindAsync([id], cancellationToken);
        return book is null ? NotFound() : Ok(ToResponse(book));
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<BookResponse>>> Search([FromQuery] string keyword, CancellationToken cancellationToken)
    {
        keyword = keyword.Trim();

        var books = await _context.Books
            .Where(x => x.Title.Contains(keyword) || x.Author.Contains(keyword) || x.Category.Contains(keyword))
            .OrderBy(x => x.Title)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(books);
    }

    [HttpPost]
    public async Task<ActionResult<BookResponse>> Create(BookUpsertRequest request, CancellationToken cancellationToken)
    {
        var book = new Book
        {
            Isbn = request.Isbn.Trim(),
            Title = request.Title.Trim(),
            Author = request.Author.Trim(),
            Publisher = request.Publisher.Trim(),
            PublishedYear = request.PublishedYear,
            Category = request.Category.Trim(),
            TotalCopies = request.TotalCopies,
            AvailableCopies = request.TotalCopies,
            MinimumCopies = request.MinimumCopies,
            CoverImageUrl = request.CoverImageUrl?.Trim(),
            Description = request.Description?.Trim()
        };

        _context.Books.Add(book);
        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.PublishAvailabilityChangedAsync(book, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = book.Id }, ToResponse(book));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BookResponse>> Update(Guid id, BookUpsertRequest request, CancellationToken cancellationToken)
    {
        var book = await _context.Books.FindAsync([id], cancellationToken);
        if (book is null)
        {
            return NotFound();
        }

        book.Isbn = request.Isbn.Trim();
        book.Title = request.Title.Trim();
        book.Author = request.Author.Trim();
        book.Publisher = request.Publisher.Trim();
        book.PublishedYear = request.PublishedYear;
        book.Category = request.Category.Trim();
        book.TotalCopies = request.TotalCopies;
        book.MinimumCopies = request.MinimumCopies;
        book.CoverImageUrl = request.CoverImageUrl?.Trim();
        book.Description = request.Description?.Trim();
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

    [HttpPost("{id:guid}/restore")]
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
            book.CanBorrow,
            book.IsArchived,
            book.CoverImageUrl,
            book.Description,
            book.CreatedAtUtc,
            book.UpdatedAtUtc);
}
