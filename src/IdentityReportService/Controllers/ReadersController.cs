using IdentityReportService.Contracts;
using IdentityReportService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/readers")]
[Authorize(Roles = $"{LibraryRoles.Admin},{LibraryRoles.Librarian}")]
public class ReadersController : ControllerBase
{
    private readonly IdentityDbContext _context;

    public ReadersController(IdentityDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReaderResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var readers = await (
            from user in _context.Users
            join profile in _context.ReaderProfiles on user.Id equals profile.UserId
            orderby user.FullName
            select new ReaderResponse(
                user.Id,
                user.Email,
                user.FullName,
                user.Role,
                profile.LibraryCardNumber,
                profile.ExpiredAtUtc,
                profile.Status))
            .ToListAsync(cancellationToken);

        return Ok(readers);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReaderDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var detail = await BuildDetailAsync(id, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, ReaderStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        var status = request.Status.Trim();
        if (string.IsNullOrWhiteSpace(status))
        {
            return BadRequest(new { message = "Status is required." });
        }

        var user = await _context.Users.FindAsync([id], cancellationToken);
        var profile = await _context.ReaderProfiles.SingleOrDefaultAsync(x => x.UserId == id, cancellationToken);
        if (user is null || profile is null)
        {
            return NotFound();
        }

        user.IsActive = status.Equals("Active", StringComparison.OrdinalIgnoreCase);
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        profile.Status = status;
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/expiry")]
    public async Task<IActionResult> UpdateExpiry(Guid id, ReaderExpiryUpdateRequest request, CancellationToken cancellationToken)
    {
        var profile = await _context.ReaderProfiles.SingleOrDefaultAsync(x => x.UserId == id, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        if (request.ExpiredAtUtc <= DateTimeOffset.UtcNow)
        {
            return BadRequest(new { message = "Expiry date must be in the future." });
        }

        profile.ExpiredAtUtc = request.ExpiredAtUtc;
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync([id], cancellationToken);
        var profile = await _context.ReaderProfiles.SingleOrDefaultAsync(x => x.UserId == id, cancellationToken);
        if (user is null || profile is null)
        {
            return NotFound();
        }

        var hasActiveBorrowings = await _context.BorrowingProjections.AnyAsync(
            x => x.ReaderId == id && (x.Status == BorrowStatus.Borrowed || x.Status == BorrowStatus.Overdue),
            cancellationToken);
        if (hasActiveBorrowings)
        {
            return BadRequest(new { message = "Cannot delete reader with active borrowings." });
        }

        _context.ReaderProfiles.Remove(profile);
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<ReaderDetailResponse?> BuildDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync([id], cancellationToken);
        var profile = await _context.ReaderProfiles.SingleOrDefaultAsync(x => x.UserId == id, cancellationToken);
        if (user is null || profile is null)
        {
            return null;
        }

        var borrowings = await _context.BorrowingProjections
            .Where(x => x.ReaderId == id)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var activeBorrowings = borrowings.Where(x => x.IsActive).ToList();

        return new ReaderDetailResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            profile.LibraryCardNumber,
            profile.ExpiredAtUtc,
            profile.Status,
            borrowings.Count,
            activeBorrowings.Count,
            activeBorrowings.Count(x => x.DueAtUtc < now),
            activeBorrowings.Sum(x => x.DueAtUtc < now ? Math.Max(0, (int)Math.Ceiling((now - x.DueAtUtc).TotalDays)) * 2000m : 0));
    }
}
