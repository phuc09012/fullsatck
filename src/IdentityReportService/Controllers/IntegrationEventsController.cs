using System.Security.Cryptography;
using System.Text;
using IdentityReportService.Data;
using IdentityReportService.Models;
using IdentityReportService.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Contracts;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("integration/events")]
public class IntegrationEventsController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly InternalApiOptions _internalApiOptions;

    public IntegrationEventsController(
        IdentityDbContext context,
        IOptions<InternalApiOptions> internalApiOptions)
    {
        _context = context;
        _internalApiOptions = internalApiOptions.Value;
    }

    [HttpPost("book-borrowed")]
    public async Task<IActionResult> BookBorrowed(BookBorrowedEvent evt, CancellationToken cancellationToken)
    {
        if (!IsInternalRequest())
        {
            return Unauthorized(new { message = "Internal API key is required." });
        }

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
        projection.FinePaidAmount = 0;
        projection.FinePaidAtUtc = null;
        projection.Status = BorrowStatus.Borrowed;
        projection.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Accepted();
    }

    [HttpPost("book-returned")]
    public async Task<IActionResult> BookReturned(BookReturnedEvent evt, CancellationToken cancellationToken)
    {
        if (!IsInternalRequest())
        {
            return Unauthorized(new { message = "Internal API key is required." });
        }

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
        projection.Status = BorrowStatus.Returned;
        projection.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Accepted();
    }

    [HttpPost("fine-paid")]
    public async Task<IActionResult> FinePaid(FinePaidEvent evt, CancellationToken cancellationToken)
    {
        if (!IsInternalRequest())
        {
            return Unauthorized(new { message = "Internal API key is required." });
        }

        var projection = await _context.BorrowingProjections.FindAsync([evt.BorrowingId], cancellationToken);
        if (projection is null)
        {
            projection = new BorrowingProjection { BorrowingId = evt.BorrowingId };
            _context.BorrowingProjections.Add(projection);
        }

        projection.ReaderId = evt.ReaderId;
        projection.BookId = evt.BookId;
        projection.FineAmount = evt.FineAmount;
        projection.FinePaidAmount = evt.PaidAmount;
        projection.FinePaidAtUtc = evt.PaidAtUtc;
        projection.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Accepted();
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
}
