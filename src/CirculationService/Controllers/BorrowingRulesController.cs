using CirculationService.Contracts;
using CirculationService.Data;
using CirculationService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace CirculationService.Controllers;

[ApiController]
[Route("api/circulation-rules")]
[Authorize(Roles = LibraryRoles.Admin + "," + LibraryRoles.Librarian)]
public class BorrowingRulesController : ControllerBase
{
    private readonly CirculationDbContext _context;

    public BorrowingRulesController(CirculationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<BorrowingPolicyResponse>> Get(CancellationToken cancellationToken)
    {
        var policy = await GetOrCreatePolicyAsync(cancellationToken);
        return Ok(ToResponse(policy));
    }

    [HttpPut]
    [Authorize(Roles = LibraryRoles.Admin)]
    public async Task<ActionResult<BorrowingPolicyResponse>> Update(
        BorrowingPolicyUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var policy = await GetOrCreatePolicyAsync(cancellationToken);
        policy.MaxActiveBorrowingsPerReader = request.MaxActiveBorrowingsPerReader;
        policy.DefaultBorrowDays = request.DefaultBorrowDays;
        policy.MaxRenewalDays = request.MaxRenewalDays;
        policy.FinePerOverdueDay = request.FinePerOverdueDay;
        policy.AllowReaderSelfCheckout = request.AllowReaderSelfCheckout;
        policy.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(policy));
    }

    private async Task<BorrowingPolicy> GetOrCreatePolicyAsync(CancellationToken cancellationToken)
    {
        var policy = await _context.BorrowingPolicies
            .SingleOrDefaultAsync(x => x.Id == BorrowingPolicy.SingletonId, cancellationToken);

        if (policy is not null)
        {
            return policy;
        }

        policy = new BorrowingPolicy { Id = BorrowingPolicy.SingletonId };
        _context.BorrowingPolicies.Add(policy);
        await _context.SaveChangesAsync(cancellationToken);
        return policy;
    }

    private static BorrowingPolicyResponse ToResponse(BorrowingPolicy policy)
        => new(
            policy.MaxActiveBorrowingsPerReader,
            policy.DefaultBorrowDays,
            policy.MaxRenewalDays,
            policy.FinePerOverdueDay,
            policy.AllowReaderSelfCheckout,
            policy.UpdatedAtUtc);

    private static string? Validate(BorrowingPolicyUpdateRequest request)
    {
        if (request.MaxActiveBorrowingsPerReader <= 0 || request.MaxActiveBorrowingsPerReader > 50)
        {
            return "Max active borrowings must be between 1 and 50.";
        }

        if (request.DefaultBorrowDays <= 0 || request.DefaultBorrowDays > 365)
        {
            return "Default borrow days must be between 1 and 365.";
        }

        if (request.MaxRenewalDays <= 0 || request.MaxRenewalDays > 365)
        {
            return "Max renewal days must be between 1 and 365.";
        }

        if (request.FinePerOverdueDay < 0 || request.FinePerOverdueDay > 1_000_000)
        {
            return "Fine per overdue day must be between 0 and 1,000,000.";
        }

        return null;
    }
}
