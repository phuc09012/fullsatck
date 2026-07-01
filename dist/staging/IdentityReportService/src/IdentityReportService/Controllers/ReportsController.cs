using IdentityReportService.Contracts;
using IdentityReportService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = $"{LibraryRoles.Admin},{LibraryRoles.Librarian}")]
public class ReportsController : ControllerBase
{
    private readonly IdentityDbContext _context;

    public ReportsController(IdentityDbContext context)
    {
        _context = context;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<object>> Dashboard(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var borrowings = await _context.BorrowingProjections.ToListAsync(cancellationToken);
        var activeBorrowings = borrowings.Where(x => x.IsActive).ToList();

        var response = new
        {
            TotalReaders = await _context.ReaderProfiles.CountAsync(cancellationToken),
            TotalBorrowings = borrowings.Count,
            ActiveBorrowings = activeBorrowings.Count,
            OverdueBorrowings = activeBorrowings.Count(x => x.DueAtUtc < now),
            TotalFineCollected = borrowings.Where(x => x.ReturnedAtUtc.HasValue).Sum(x => x.FineAmount),
            OutstandingDebt = activeBorrowings.Sum(x => x.DueAtUtc < now ? Math.Max(0, (int)Math.Ceiling((now - x.DueAtUtc).TotalDays)) * 2000m : 0),
            TopBooks = borrowings
                .GroupBy(x => x.BookTitle)
                .Select(group => new { BookTitle = group.Key, BorrowCount = group.Count() })
                .OrderByDescending(x => x.BorrowCount)
                .Take(10)
                .ToList(),
            BorrowTrend = borrowings
                .GroupBy(x => x.BorrowedAtUtc.Date)
                .Select(group => new { Date = group.Key, Count = group.Count() })
                .OrderBy(x => x.Date)
                .ToList()
        };

        return Ok(response);
    }
}
