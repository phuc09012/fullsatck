using IdentityReportService.Data;
using IdentityReportService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("integration/events")]
public class IntegrationEventsController : ControllerBase
{
    private readonly IdentityDbContext _context;

    public IntegrationEventsController(IdentityDbContext context)
    {
        _context = context;
    }

    [HttpPost("book-borrowed")]
    public async Task<IActionResult> BookBorrowed(BookBorrowedEvent evt, CancellationToken cancellationToken)
    {
        var projection = await _context.BorrowingProjections.FindAsync([evt.BorrowingId], cancellationToken);
        if (projection is null)
        {
            projection = new BorrowingProjection { BorrowingId = evt.BorrowingId };
            _context.BorrowingProjections.Add(projection);
        }

        projection.ReaderId = evt.ReaderId;
        projection.BookId = evt.BookId;
        projection.BookTitle = evt.BookTitle;
        projection.BorrowedAtUtc = evt.BorrowedAtUtc;
        projection.DueAtUtc = evt.DueAtUtc;
        projection.ReturnedAtUtc = null;
        projection.FineAmount = 0;
        projection.Status = BorrowStatus.Borrowed;
        projection.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Accepted();
    }

    [HttpPost("book-returned")]
    public async Task<IActionResult> BookReturned(BookReturnedEvent evt, CancellationToken cancellationToken)
    {
        var projection = await _context.BorrowingProjections.FindAsync([evt.BorrowingId], cancellationToken);
        if (projection is null)
        {
            projection = new BorrowingProjection { BorrowingId = evt.BorrowingId };
            _context.BorrowingProjections.Add(projection);
        }

        projection.ReaderId = evt.ReaderId;
        projection.BookId = evt.BookId;
        projection.BookTitle = evt.BookTitle;
        projection.ReturnedAtUtc = evt.ReturnedAtUtc;
        projection.FineAmount = evt.FineAmount;
        projection.Status = evt.FineAmount > 0 ? BorrowStatus.Overdue : BorrowStatus.Returned;
        projection.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Accepted();
    }
}
