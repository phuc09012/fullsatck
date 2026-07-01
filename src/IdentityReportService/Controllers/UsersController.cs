using IdentityReportService.Contracts;
using IdentityReportService.Data;
using IdentityReportService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = LibraryRoles.Admin)]
public class UsersController : ControllerBase
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        LibraryRoles.Admin,
        LibraryRoles.Librarian,
        LibraryRoles.Reader
    };

    private readonly IdentityDbContext _context;
    private readonly IPasswordHasher<AppUser> _passwordHasher;

    public UsersController(IdentityDbContext context, IPasswordHasher<AppUser> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .OrderBy(x => x.Role)
            .ThenBy(x => x.FullName)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(UserCreateRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateCreate(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return Conflict(new { message = "Email already exists." });
        }

        var role = NormalizeRole(request.Role);
        var user = new AppUser
        {
            Email = email,
            FullName = request.FullName.Trim(),
            Role = role,
            IsActive = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _context.Users.Add(user);
        if (role == LibraryRoles.Reader)
        {
            _context.ReaderProfiles.Add(CreateReaderProfile(user.Id));
        }

        await _context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToResponse(user));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync([id], cancellationToken);
        return user is null ? NotFound() : Ok(ToResponse(user));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UserStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync([id], cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var profile = await _context.ReaderProfiles.SingleOrDefaultAsync(x => x.UserId == id, cancellationToken);
        if (profile is not null)
        {
            profile.Status = request.IsActive ? "Active" : "Locked";
            profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<UserResponse>> UpdateRole(Guid id, UserRoleUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedRoles.Contains(request.Role.Trim()))
        {
            return BadRequest(new { message = "Role must be Admin, Librarian, or Reader." });
        }

        var user = await _context.Users.FindAsync([id], cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var role = NormalizeRole(request.Role);
        user.Role = role;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var profile = await _context.ReaderProfiles.SingleOrDefaultAsync(x => x.UserId == id, cancellationToken);
        if (role == LibraryRoles.Reader && profile is null)
        {
            _context.ReaderProfiles.Add(CreateReaderProfile(user.Id));
        }
        else if (role != LibraryRoles.Reader && profile is not null)
        {
            var hasActiveBorrowings = await _context.BorrowingProjections.AnyAsync(
                x => x.ReaderId == id && x.ReturnedAtUtc == null && (x.Status == BorrowStatus.Borrowed || x.Status == BorrowStatus.Overdue),
                cancellationToken);
            if (hasActiveBorrowings)
            {
                return BadRequest(new { message = "Cannot change role while reader has active borrowings." });
            }

            _context.ReaderProfiles.Remove(profile);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(user));
    }

    private static UserResponse ToResponse(AppUser user)
        => new(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.IsActive,
            user.CreatedAtUtc);

    private static ReaderProfile CreateReaderProfile(Guid userId)
        => new()
        {
            UserId = userId,
            LibraryCardNumber = $"CARD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            ExpiredAtUtc = DateTimeOffset.UtcNow.AddYears(1),
            Status = "Active"
        };

    private static string NormalizeRole(string role)
    {
        role = role.Trim();
        if (role.Equals(LibraryRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return LibraryRoles.Admin;
        }

        if (role.Equals(LibraryRoles.Librarian, StringComparison.OrdinalIgnoreCase))
        {
            return LibraryRoles.Librarian;
        }

        return LibraryRoles.Reader;
    }

    private static string? ValidateCreate(UserCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return "Email is required.";
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return "Full name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return "Password must be at least 6 characters.";
        }

        if (string.IsNullOrWhiteSpace(request.Role) || !AllowedRoles.Contains(request.Role.Trim()))
        {
            return "Role must be Admin, Librarian, or Reader.";
        }

        return null;
    }
}
