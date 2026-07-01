using System.Security.Cryptography;
using System.Text;
using IdentityReportService.Data;
using IdentityReportService.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Contracts;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/internal/readers")]
[AllowAnonymous]
public class InternalReadersController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly InternalApiOptions _internalApiOptions;

    public InternalReadersController(
        IdentityDbContext context,
        IOptions<InternalApiOptions> internalApiOptions)
    {
        _context = context;
        _internalApiOptions = internalApiOptions.Value;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReaderLookupResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!IsInternalRequest())
        {
            return Unauthorized(new { message = "Internal API key is required." });
        }

        var data = await (
            from user in _context.Users
            join profile in _context.ReaderProfiles on user.Id equals profile.UserId
            where user.Id == id
            select new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.Role,
                profile.Status,
                profile.ExpiredAtUtc,
                user.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (data is null)
        {
            return NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        return Ok(new ReaderLookupResponse(
            data.Id,
            data.Email,
            data.FullName,
            data.Role,
            data.Status,
            data.ExpiredAtUtc,
            data.IsActive && data.Status == "Active" && data.ExpiredAtUtc > now));
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
