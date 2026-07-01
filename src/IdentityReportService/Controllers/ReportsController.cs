using System.Security.Claims;
using IdentityReportService.Contracts;
using IdentityReportService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private const string StaffRoles = $"{LibraryRoles.Admin},{LibraryRoles.Librarian}";
    private const decimal DefaultFinePerOverdueDay = 2000m;

    private readonly IdentityDbContext _context;

    public ReportsController(IdentityDbContext context)
    {
        _context = context;
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = StaffRoles)]
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
            TotalFineCollected = borrowings.Sum(x => x.FinePaidAmount),
            OutstandingDebt = activeBorrowings.Sum(x => CalculateFine(x.DueAtUtc, now))
                + borrowings.Where(x => !x.IsActive).Sum(x => x.OutstandingFine),
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

    [HttpGet("reader/{readerId:guid}")]
    public async Task<ActionResult<object>> ReaderReport(Guid readerId, CancellationToken cancellationToken)
    {
        if (!CanAccessReader(readerId))
        {
            return Forbid();
        }

        var profile = await _context.ReaderProfiles.SingleOrDefaultAsync(x => x.UserId == readerId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        var borrowings = await _context.BorrowingProjections
            .Where(x => x.ReaderId == readerId)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var activeBorrowings = borrowings.Where(x => x.IsActive).ToList();

        return Ok(new
        {
            ReaderId = readerId,
            profile.LibraryCardNumber,
            profile.ExpiredAtUtc,
            profile.Status,
            TotalBorrowings = borrowings.Count,
            ActiveBorrowings = activeBorrowings.Count,
            OverdueBorrowings = activeBorrowings.Count(x => x.DueAtUtc < now),
            DebtAmount = activeBorrowings.Sum(x => CalculateFine(x.DueAtUtc, now))
                + borrowings.Where(x => !x.IsActive).Sum(x => x.OutstandingFine),
            TotalFineCollected = borrowings.Sum(x => x.FinePaidAmount),
            Borrowings = borrowings
                .OrderByDescending(x => x.BorrowedAtUtc)
                .Select(x => new
                {
                    x.BorrowingId,
                    x.BookId,
                    x.BookTitle,
                    x.BorrowedAtUtc,
                    x.DueAtUtc,
                    x.ReturnedAtUtc,
                    Status = x.Status.ToString(),
                    x.FineAmount,
                    x.FinePaidAmount,
                    OutstandingFine = x.OutstandingFine
                })
                .ToList()
        });
    }

    [HttpGet("overdue-readers")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<IEnumerable<object>>> OverdueReaders(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var profiles = await _context.ReaderProfiles.ToListAsync(cancellationToken);
        var users = await _context.Users.ToListAsync(cancellationToken);
        var borrowings = await _context.BorrowingProjections.ToListAsync(cancellationToken);

        var overdueReaders = (
            from profile in profiles
            join user in users on profile.UserId equals user.Id
            let readerBorrowings = borrowings.Where(x => x.ReaderId == user.Id).ToList()
            let overdueBorrowings = readerBorrowings.Where(x => x.IsActive && x.DueAtUtc < now).ToList()
            where overdueBorrowings.Any()
            select new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.Role,
                profile.LibraryCardNumber,
                profile.Status,
                OverdueBorrowings = overdueBorrowings.Count,
                DebtAmount = overdueBorrowings.Sum(x => CalculateFine(x.DueAtUtc, now))
            })
            .ToList();

        return Ok(overdueReaders);
    }

    private static decimal CalculateFine(DateTimeOffset dueAtUtc, DateTimeOffset now)
    {
        if (now <= dueAtUtc)
        {
            return 0;
        }

        var overdueDays = (int)Math.Ceiling((now - dueAtUtc).TotalDays);
        return overdueDays * DefaultFinePerOverdueDay;
    }

    private bool CanAccessReader(Guid readerId)
        => IsStaff() || CurrentUserId() == readerId;

    private bool IsStaff()
        => User.IsInRole(LibraryRoles.Admin) || User.IsInRole(LibraryRoles.Librarian);

    private Guid? CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
