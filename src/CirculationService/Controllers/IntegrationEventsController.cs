using System.Security.Cryptography;
using System.Text;
using CirculationService.Contracts;
using CirculationService.Data;
using CirculationService.Models;
using CirculationService.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Contracts;

namespace CirculationService.Controllers;

[ApiController]
[Route("integration/events")]
public class IntegrationEventsController : ControllerBase
{
    private readonly CirculationDbContext _context;
    private readonly InternalApiOptions _internalApiOptions;

    public IntegrationEventsController(
        CirculationDbContext context,
        IOptions<InternalApiOptions> internalApiOptions)
    {
        _context = context;
        _internalApiOptions = internalApiOptions.Value;
    }

    [HttpPost("book-availability-changed")]
    public async Task<IActionResult> BookAvailabilityChanged(CatalogAvailabilityEvent evt, CancellationToken cancellationToken)
    {
        if (!IsInternalRequest())
        {
            return Unauthorized(new { message = "Internal API key is required." });
        }

        var snapshot = await _context.CatalogBookSnapshots.FindAsync([evt.BookId], cancellationToken);
        if (snapshot is null)
        {
            snapshot = new CatalogBookSnapshot { BookId = evt.BookId };
            _context.CatalogBookSnapshots.Add(snapshot);
        }

        snapshot.Title = evt.Title;
        snapshot.AvailableCopies = evt.AvailableCopies;
        snapshot.TotalCopies = evt.TotalCopies;
        snapshot.CanBorrow = evt.CanBorrow;
        snapshot.UpdatedAtUtc = evt.ChangedAtUtc;

        await _context.SaveChangesAsync(cancellationToken);
        return Accepted();
    }

    [HttpGet("books")]
    [Authorize(Roles = LibraryRoles.Admin + "," + LibraryRoles.Librarian)]
    public async Task<ActionResult<IEnumerable<CatalogBookSnapshot>>> GetSnapshots(CancellationToken cancellationToken)
    {
        var snapshots = await _context.CatalogBookSnapshots
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);

        return Ok(snapshots);
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
