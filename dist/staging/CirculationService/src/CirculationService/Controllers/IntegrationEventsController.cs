using CirculationService.Contracts;
using CirculationService.Data;
using CirculationService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CirculationService.Controllers;

[ApiController]
[Route("integration/events")]
public class IntegrationEventsController : ControllerBase
{
    private readonly CirculationDbContext _context;

    public IntegrationEventsController(CirculationDbContext context)
    {
        _context = context;
    }

    [HttpPost("book-availability-changed")]
    public async Task<IActionResult> BookAvailabilityChanged(CatalogAvailabilityEvent evt, CancellationToken cancellationToken)
    {
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
    public async Task<ActionResult<IEnumerable<CatalogBookSnapshot>>> GetSnapshots(CancellationToken cancellationToken)
    {
        var snapshots = await _context.CatalogBookSnapshots
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);

        return Ok(snapshots);
    }
}
