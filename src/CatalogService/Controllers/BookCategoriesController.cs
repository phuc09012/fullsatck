using CatalogService.Data;
using CatalogService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/book-categories")]
public class BookCategoriesController : ControllerBase
{
    private readonly CatalogDbContext _context;

    public BookCategoriesController(CatalogDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookCategoryResponse>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        if (includeInactive && !User.IsInRole(LibraryRoles.Admin))
        {
            return Forbid();
        }

        var query = _context.BookCategories.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var categories = await query
            .OrderBy(x => x.Name)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookCategoryResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var category = await _context.BookCategories.FindAsync([id], cancellationToken);
        if (category is null || (!category.IsActive && !User.IsInRole(LibraryRoles.Admin)))
        {
            return NotFound();
        }

        return Ok(ToResponse(category));
    }

    [HttpPost]
    [Authorize(Roles = LibraryRoles.Admin)]
    public async Task<ActionResult<BookCategoryResponse>> Create(BookCategoryCreateRequest request, CancellationToken cancellationToken)
    {
        var validationError = Validate(request.Name, request.Description);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var name = NormalizeName(request.Name);
        if (await _context.BookCategories.AnyAsync(x => x.Name == name, cancellationToken))
        {
            return Conflict(new { message = "Category already exists." });
        }

        var category = new BookCategory
        {
            Name = name,
            Description = NormalizeOptional(request.Description)
        };

        _context.BookCategories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, ToResponse(category));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = LibraryRoles.Admin)]
    public async Task<ActionResult<BookCategoryResponse>> Update(
        Guid id,
        BookCategoryUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _context.BookCategories.FindAsync([id], cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        var validationError = Validate(request.Name, request.Description);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var name = NormalizeName(request.Name);
        var nameExists = await _context.BookCategories.AnyAsync(
            x => x.Id != id && x.Name == name,
            cancellationToken);
        if (nameExists)
        {
            return Conflict(new { message = "Category already exists." });
        }

        var previousName = category.Name;
        category.Name = name;
        category.Description = NormalizeOptional(request.Description);
        category.IsActive = request.IsActive;
        category.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (!string.Equals(previousName, name, StringComparison.Ordinal))
        {
            var books = await _context.Books
                .Where(x => x.Category == previousName)
                .ToListAsync(cancellationToken);

            foreach (var book in books)
            {
                book.Category = name;
                book.UpdateStockSnapshot();
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(category));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = LibraryRoles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var category = await _context.BookCategories.FindAsync([id], cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        var isUsed = await _context.Books.AnyAsync(x => x.Category == category.Name, cancellationToken);
        if (isUsed)
        {
            return BadRequest(new { message = "Cannot delete category while books are using it. Deactivate it instead." });
        }

        _context.BookCategories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static BookCategoryResponse ToResponse(BookCategory category)
        => new(
            category.Id,
            category.Name,
            category.Description,
            category.IsActive,
            category.CreatedAtUtc,
            category.UpdatedAtUtc);

    private static string? Validate(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Category name is required.";
        }

        if (NormalizeName(name).Length > 128)
        {
            return "Category name cannot exceed 128 characters.";
        }

        if (description?.Trim().Length > 500)
        {
            return "Category description cannot exceed 500 characters.";
        }

        return null;
    }

    private static string NormalizeName(string value)
        => string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
