using System.Security.Claims;
using CirculationService.Contracts;
using CirculationService.Data;
using CirculationService.Models;
using CirculationService.Options;
using CirculationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Contracts;

namespace CirculationService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BorrowingsController : ControllerBase
{
    private const string StaffRoles = LibraryRoles.Admin + "," + LibraryRoles.Librarian;

    private readonly CirculationDbContext _context;
    private readonly CatalogClient _catalogClient;
    private readonly ReaderClient _readerClient;
    private readonly IntegrationEventPublisher _publisher;
    private readonly BorrowingOptions _options;
    private readonly ILogger<BorrowingsController> _logger;

    public BorrowingsController(
        CirculationDbContext context,
        CatalogClient catalogClient,
        ReaderClient readerClient,
        IntegrationEventPublisher publisher,
        ILogger<BorrowingsController> logger,
        IOptions<BorrowingOptions> options)
    {
        _context = context;
        _catalogClient = catalogClient;
        _readerClient = readerClient;
        _publisher = publisher;
        _logger = logger;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BorrowingRecordResponse>>> GetAll(CancellationToken cancellationToken)
    {
        if (!IsStaff())
        {
            return Forbid();
        }

        var records = await _context.BorrowingRecords
            .OrderByDescending(x => x.BorrowedAtUtc)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(records);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BorrowingRecordResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var record = await _context.BorrowingRecords.FindAsync([id], cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        if (!CanAccessReader(record.ReaderId))
        {
            return Forbid();
        }

        return Ok(ToResponse(record));
    }

    [HttpGet("reader/{readerId:guid}")]
    public async Task<ActionResult<IEnumerable<BorrowingRecordResponse>>> GetByReader(Guid readerId, CancellationToken cancellationToken)
    {
        if (!CanAccessReader(readerId))
        {
            return Forbid();
        }

        var records = await _context.BorrowingRecords
            .Where(x => x.ReaderId == readerId)
            .OrderByDescending(x => x.BorrowedAtUtc)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(records);
    }

    [HttpGet("overdue")]
    public async Task<ActionResult<IEnumerable<BorrowingRecordResponse>>> GetOverdue(CancellationToken cancellationToken)
    {
        if (!IsStaff())
        {
            return Forbid();
        }

        var now = DateTimeOffset.UtcNow;
        var records = await _context.BorrowingRecords
            .Where(x => x.ReturnedAtUtc == null && (x.Status == BorrowStatus.Borrowed || x.Status == BorrowStatus.Overdue) && x.DueAtUtc < now)
            .OrderBy(x => x.DueAtUtc)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(records);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<CirculationSummaryResponse>> GetSummary([FromQuery] Guid? readerId, CancellationToken cancellationToken)
    {
        if (readerId.HasValue)
        {
            if (!CanAccessReader(readerId.Value))
            {
                return Forbid();
            }
        }
        else if (!IsStaff())
        {
            return Forbid();
        }

        var policy = await GetPolicyAsync(cancellationToken);
        var query = _context.BorrowingRecords.AsQueryable();
        if (readerId.HasValue)
        {
            query = query.Where(x => x.ReaderId == readerId.Value);
        }

        var records = await query.ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var activeRecords = records.Where(x => x.IsActive).ToList();

        return Ok(new CirculationSummaryResponse(
            TotalBorrowings: records.Count,
            ActiveBorrowings: activeRecords.Count,
            ReturnedBorrowings: records.Count(x => x.Status == BorrowStatus.Returned),
            OverdueBorrowings: activeRecords.Count(x => x.DueAtUtc < now),
            OutstandingDebt: activeRecords.Sum(x => x.DueAtUtc < now ? CalculateFine(x.DueAtUtc, now, policy.FinePerOverdueDay) : 0)
                + records.Where(x => !x.IsActive).Sum(x => x.OutstandingFine),
            TotalFineCollected: records.Sum(x => x.FinePaidAmount)));
    }

    [HttpPost]
    public async Task<ActionResult<BorrowResponse>> Borrow(BorrowRequest request, CancellationToken cancellationToken)
    {
        var policy = await GetPolicyAsync(cancellationToken);
        if (IsReader())
        {
            var currentUserId = CurrentUserId();
            if (!policy.AllowReaderSelfCheckout || currentUserId != request.ReaderId)
            {
                return Forbid();
            }
        }
        else if (!IsStaff())
        {
            return Forbid();
        }

        if (request.ReaderId == Guid.Empty)
        {
            return BadRequest(new { message = "ReaderId is required." });
        }

        if (request.BookId == Guid.Empty)
        {
            return BadRequest(new { message = "BookId is required." });
        }

        if (request.BorrowDays.HasValue && (request.BorrowDays <= 0 || request.BorrowDays > 365))
        {
            return BadRequest(new { message = "Borrow days must be between 1 and 365." });
        }

        var reader = await _readerClient.GetReaderAsync(request.ReaderId, cancellationToken);
        if (reader is null)
        {
            return NotFound(new { message = "Reader not found." });
        }

        if (!string.Equals(reader.Role, LibraryRoles.Reader, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only reader accounts can borrow books." });
        }

        if (!reader.IsActive || !string.Equals(reader.Status, "Active", StringComparison.OrdinalIgnoreCase) || reader.ExpiredAtUtc <= DateTimeOffset.UtcNow)
        {
            return BadRequest(new { message = "Reader account is inactive or expired." });
        }

        var book = await _catalogClient.GetBookAsync(request.BookId, cancellationToken);
        if (book is null)
        {
            return NotFound(new { message = "Book not found." });
        }

        if (!book.CanBorrow)
        {
            return BadRequest(new { message = "Book is currently unavailable." });
        }

        var activeBookBorrowings = await _context.BorrowingRecords.CountAsync(
            x => x.BookId == request.BookId && x.ReturnedAtUtc == null && (x.Status == BorrowStatus.Borrowed || x.Status == BorrowStatus.Overdue),
            cancellationToken);

        if (activeBookBorrowings >= book.MaxBorrowingsPerReader)
        {
            return BadRequest(new { message = "Book reached its borrowing threshold." });
        }

        var borrowed = await _catalogClient.BorrowBookAsync(request.BookId, cancellationToken);
        if (!borrowed)
        {
            return BadRequest(new { message = "Borrowing failed because the book was not available." });
        }

        var borrowedAt = DateTimeOffset.UtcNow;
        var dueAt = borrowedAt.AddDays(request.BorrowDays.GetValueOrDefault(policy.DefaultBorrowDays));

        var record = new BorrowingRecord
        {
            ReaderId = request.ReaderId,
            BookId = book.Id,
            BookTitle = book.Title,
            BorrowedAtUtc = borrowedAt,
            DueAtUtc = dueAt,
            Status = BorrowStatus.Borrowed
        };

        _context.BorrowingRecords.Add(record);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _publisher.PublishBorrowedAsync(record, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist borrowing record {BorrowingId}. Rolling back catalog state.", record.Id);

            _context.BorrowingRecords.Remove(record);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Failed to roll back borrowing record {BorrowingId}.", record.Id);
            }

            await _catalogClient.ReturnBookAsync(record.BookId, cancellationToken);
            throw;
        }

        return Ok(new BorrowResponse(record.Id, record.ReaderId, record.BookId, record.BookTitle, record.BorrowedAtUtc, record.DueAtUtc, record.Status.ToString()));
    }

    [HttpPost("{id:guid}/renew")]
    public async Task<ActionResult<BorrowingRecordResponse>> Renew(Guid id, RenewRequest request, CancellationToken cancellationToken)
    {
        var record = await _context.BorrowingRecords.FindAsync([id], cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        if (!CanAccessReader(record.ReaderId))
        {
            return Forbid();
        }

        if (!record.IsActive)
        {
            return BadRequest(new { message = "Only active borrowings can be renewed." });
        }

        if (record.DueAtUtc < DateTimeOffset.UtcNow)
        {
            return BadRequest(new { message = "Overdue borrowings cannot be renewed." });
        }

        var policy = await GetPolicyAsync(cancellationToken);
        var extraDays = request.ExtraDays.GetValueOrDefault(policy.DefaultBorrowDays);
        if (extraDays <= 0 || extraDays > policy.MaxRenewalDays)
        {
            return BadRequest(new { message = $"Renewal days must be between 1 and {policy.MaxRenewalDays}." });
        }

        record.DueAtUtc = record.DueAtUtc.AddDays(extraDays);
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(record));
    }

    [HttpPost("{id:guid}/return")]
    public async Task<ActionResult<BorrowingRecordResponse>> Return(Guid id, ReturnRequest request, CancellationToken cancellationToken)
    {
        var record = await _context.BorrowingRecords.FindAsync([id], cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        if (!CanAccessReader(record.ReaderId))
        {
            return Forbid();
        }

        if (record.ReturnedAtUtc.HasValue)
        {
            return BadRequest(new { message = "Borrowing record already returned." });
        }

        var policy = await GetPolicyAsync(cancellationToken);
        var returnedAt = request.ReturnedAtUtc ?? DateTimeOffset.UtcNow;
        var returned = await _catalogClient.ReturnBookAsync(record.BookId, cancellationToken);
        if (!returned)
        {
            return BadRequest(new { message = "Cannot update catalog availability." });
        }

        var previousReturnedAtUtc = record.ReturnedAtUtc;
        var previousFineAmount = record.FineAmount;
        var previousStatus = record.Status;

        record.ReturnedAtUtc = returnedAt;
        record.FineAmount = CalculateFine(record.DueAtUtc, returnedAt, policy.FinePerOverdueDay);
        record.Status = BorrowStatus.Returned;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _publisher.PublishReturnedAsync(record, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist return record {BorrowingId}. Rolling back catalog state.", record.Id);

            record.ReturnedAtUtc = previousReturnedAtUtc;
            record.FineAmount = previousFineAmount;
            record.Status = previousStatus;
            record.UpdatedAtUtc = DateTimeOffset.UtcNow;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Failed to restore borrowing record {BorrowingId} after return failure.", record.Id);
            }

            await _catalogClient.BorrowBookAsync(record.BookId, cancellationToken);
            throw;
        }

        return Ok(ToResponse(record));
    }

    [HttpGet("fines")]
    public async Task<ActionResult<object>> GetFineSummary([FromQuery] Guid? readerId, CancellationToken cancellationToken)
    {
        if (readerId.HasValue)
        {
            if (!CanAccessReader(readerId.Value))
            {
                return Forbid();
            }
        }
        else if (!IsStaff())
        {
            return Forbid();
        }

        var policy = await GetPolicyAsync(cancellationToken);
        var query = _context.BorrowingRecords.AsQueryable();

        if (readerId.HasValue)
        {
            query = query.Where(x => x.ReaderId == readerId.Value);
        }

        var records = await query.ToListAsync(cancellationToken);
        var currentDebt = records.Sum(x => x.IsActive
            ? CalculateFine(x.DueAtUtc, DateTimeOffset.UtcNow, policy.FinePerOverdueDay)
            : x.OutstandingFine);

        return Ok(new
        {
            ReaderId = readerId,
            TotalBorrowings = records.Count,
            ActiveBorrowings = records.Count(x => x.IsActive),
            OverdueBorrowings = records.Count(x => x.IsActive && x.DueAtUtc < DateTimeOffset.UtcNow),
            DebtAmount = currentDebt,
            PaidAmount = records.Sum(x => x.FinePaidAmount)
        });
    }

    [HttpPost("{id:guid}/fine-payment")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<BorrowingRecordResponse>> PayFine(Guid id, FinePaymentRequest request, CancellationToken cancellationToken)
    {
        var record = await _context.BorrowingRecords.FindAsync([id], cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Payment amount must be greater than zero." });
        }

        if (record.FineAmount <= 0)
        {
            return BadRequest(new { message = "This borrowing record has no fine." });
        }

        if (request.Amount > record.OutstandingFine)
        {
            return BadRequest(new { message = "Payment amount cannot exceed outstanding fine." });
        }

        record.FinePaidAmount += request.Amount;
        record.FinePaidAtUtc = DateTimeOffset.UtcNow;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.PublishFinePaidAsync(record, cancellationToken);

        return Ok(ToResponse(record));
    }

    private static decimal CalculateFine(DateTimeOffset dueAtUtc, DateTimeOffset returnedAtUtc, decimal finePerOverdueDay)
    {
        if (returnedAtUtc <= dueAtUtc)
        {
            return 0;
        }

        var overdueDays = (int)Math.Ceiling((returnedAtUtc - dueAtUtc).TotalDays);
        return overdueDays * finePerOverdueDay;
    }

    private static BorrowingRecordResponse ToResponse(BorrowingRecord record)
        => new(
            record.Id,
            record.ReaderId,
            record.BookId,
            record.BookTitle,
            record.BorrowedAtUtc,
            record.DueAtUtc,
            record.ReturnedAtUtc,
            EffectiveStatus(record).ToString(),
            record.FineAmount,
            record.FinePaidAmount,
            record.FinePaidAtUtc,
            record.OutstandingFine);

    private static BorrowStatus EffectiveStatus(BorrowingRecord record)
    {
        if (record.ReturnedAtUtc is null
            && (record.Status == BorrowStatus.Borrowed || record.Status == BorrowStatus.Overdue)
            && record.DueAtUtc < DateTimeOffset.UtcNow)
        {
            return BorrowStatus.Overdue;
        }

        return record.Status;
    }

    private async Task<BorrowingPolicy> GetPolicyAsync(CancellationToken cancellationToken)
    {
        var policy = await _context.BorrowingPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == BorrowingPolicy.SingletonId, cancellationToken);

        return policy ?? BorrowingPolicy.FromOptions(_options);
    }

    private bool IsStaff()
        => User.IsInRole(LibraryRoles.Admin) || User.IsInRole(LibraryRoles.Librarian);

    private bool IsReader()
        => User.IsInRole(LibraryRoles.Reader);

    private bool CanAccessReader(Guid readerId)
        => IsStaff() || (IsReader() && CurrentUserId() == readerId);

    private Guid? CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
