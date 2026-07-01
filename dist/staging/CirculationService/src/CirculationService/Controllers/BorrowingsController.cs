using CirculationService.Contracts;
using CirculationService.Data;
using CirculationService.Models;
using CirculationService.Options;
using CirculationService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Contracts;

namespace CirculationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BorrowingsController : ControllerBase
{
    private readonly CirculationDbContext _context;
    private readonly CatalogClient _catalogClient;
    private readonly IntegrationEventPublisher _publisher;
    private readonly BorrowingOptions _options;

    public BorrowingsController(
        CirculationDbContext context,
        CatalogClient catalogClient,
        IntegrationEventPublisher publisher,
        IOptions<BorrowingOptions> options)
    {
        _context = context;
        _catalogClient = catalogClient;
        _publisher = publisher;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BorrowingRecordResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var records = await _context.BorrowingRecords
            .OrderByDescending(x => x.BorrowedAtUtc)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(records);
    }

    [HttpGet("reader/{readerId:guid}")]
    public async Task<ActionResult<IEnumerable<BorrowingRecordResponse>>> GetByReader(Guid readerId, CancellationToken cancellationToken)
    {
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
        var now = DateTimeOffset.UtcNow;
        var records = await _context.BorrowingRecords
            .Where(x => (x.Status == BorrowStatus.Borrowed || x.Status == BorrowStatus.Overdue) && x.DueAtUtc < now)
            .OrderBy(x => x.DueAtUtc)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(records);
    }

    [HttpPost]
    public async Task<ActionResult<BorrowResponse>> Borrow(BorrowRequest request, CancellationToken cancellationToken)
    {
        var activeCount = await _context.BorrowingRecords.CountAsync(
            x => x.ReaderId == request.ReaderId && (x.Status == BorrowStatus.Borrowed || x.Status == BorrowStatus.Overdue),
            cancellationToken);

        if (activeCount >= _options.MaxActiveBorrowingsPerReader)
        {
            return BadRequest(new { message = "Reader reached maximum active borrow limit." });
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

        var borrowed = await _catalogClient.BorrowBookAsync(request.BookId, cancellationToken);
        if (!borrowed)
        {
            return BadRequest(new { message = "Borrowing failed because the book was not available." });
        }

        var borrowedAt = DateTimeOffset.UtcNow;
        var dueAt = borrowedAt.AddDays(request.BorrowDays.GetValueOrDefault(_options.DefaultBorrowDays));

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
        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.PublishBorrowedAsync(record, cancellationToken);

        return Ok(new BorrowResponse(record.Id, record.ReaderId, record.BookId, record.BookTitle, record.BorrowedAtUtc, record.DueAtUtc, record.Status.ToString()));
    }

    [HttpPost("{id:guid}/return")]
    public async Task<ActionResult<BorrowingRecordResponse>> Return(Guid id, ReturnRequest request, CancellationToken cancellationToken)
    {
        var record = await _context.BorrowingRecords.FindAsync([id], cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        if (record.Status == BorrowStatus.Returned)
        {
            return BadRequest(new { message = "Borrowing record already returned." });
        }

        var returnedAt = request.ReturnedAtUtc ?? DateTimeOffset.UtcNow;
        record.ReturnedAtUtc = returnedAt;
        record.FineAmount = CalculateFine(record.DueAtUtc, returnedAt);
        record.Status = returnedAt > record.DueAtUtc ? BorrowStatus.Overdue : BorrowStatus.Returned;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var returned = await _catalogClient.ReturnBookAsync(record.BookId, cancellationToken);
        if (!returned)
        {
            return BadRequest(new { message = "Cannot update catalog availability." });
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.PublishReturnedAsync(record, cancellationToken);

        return Ok(ToResponse(record));
    }

    [HttpGet("fines")]
    public async Task<ActionResult<object>> GetFineSummary([FromQuery] Guid? readerId, CancellationToken cancellationToken)
    {
        var query = _context.BorrowingRecords.AsQueryable();

        if (readerId.HasValue)
        {
            query = query.Where(x => x.ReaderId == readerId.Value);
        }

        var records = await query.ToListAsync(cancellationToken);
        var currentDebt = records.Sum(x => x.Status == BorrowStatus.Borrowed || x.Status == BorrowStatus.Overdue
            ? CalculateFine(x.DueAtUtc, DateTimeOffset.UtcNow)
            : x.FineAmount);

        return Ok(new
        {
            ReaderId = readerId,
            TotalBorrowings = records.Count,
            ActiveBorrowings = records.Count(x => x.Status == BorrowStatus.Borrowed || x.Status == BorrowStatus.Overdue),
            OverdueBorrowings = records.Count(x => x.Status == BorrowStatus.Overdue || ((x.Status == BorrowStatus.Borrowed || x.Status == BorrowStatus.Overdue) && x.DueAtUtc < DateTimeOffset.UtcNow)),
            DebtAmount = currentDebt
        });
    }

    private static decimal CalculateFine(DateTimeOffset dueAtUtc, DateTimeOffset returnedAtUtc)
    {
        if (returnedAtUtc <= dueAtUtc)
        {
            return 0;
        }

        var overdueDays = (int)Math.Ceiling((returnedAtUtc - dueAtUtc).TotalDays);
        return overdueDays * 2000m;
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
            record.Status.ToString(),
            record.FineAmount);
}
