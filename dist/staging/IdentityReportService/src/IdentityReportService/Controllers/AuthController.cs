using IdentityReportService.Contracts;
using IdentityReportService.Data;
using IdentityReportService.Models;
using IdentityReportService.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly JwtTokenService _tokenService;

    public AuthController(IdentityDbContext context, IPasswordHasher<AppUser> passwordHasher, JwtTokenService tokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return BadRequest(new { message = "Email already exists." });
        }

        var user = new AppUser
        {
            Email = email,
            FullName = request.FullName.Trim(),
            Role = LibraryRoles.Reader
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        var profile = new ReaderProfile
        {
            UserId = user.Id,
            LibraryCardNumber = $"CARD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            ExpiredAtUtc = DateTimeOffset.UtcNow.AddYears(1),
            Status = "Active"
        };
        _context.ReaderProfiles.Add(profile);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(_tokenService.CreateToken(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed || !user.IsActive)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        return Ok(_tokenService.CreateToken(user));
    }

    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<ActionResult<object>> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync([parsedUserId], cancellationToken);
        return user is null
            ? NotFound()
            : Ok(new { user.Id, user.Email, user.FullName, user.Role, user.IsActive });
    }
}
