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
}
