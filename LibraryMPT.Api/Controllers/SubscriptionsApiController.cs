using LibraryMPT.Data;
using LibraryMPT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LibraryMPT.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/subscriptions")]
public sealed class SubscriptionsApiController : ControllerBase
{
    private readonly LibraryContext _context;

    public SubscriptionsApiController(LibraryContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<Subscription>>> GetAll()
    {
        var subscriptions = await _context.Subscriptions
            .FromSqlRaw("""
                SELECT
                    s.SubscriptionID,
                    s.FacultyID,
                    s.Name,
                    s.StartDate,
                    s.EndDate,
                    s.DurationDays,
                    s.Status,
                    s.RequestedByUserID
                FROM Subscriptions s
                ORDER BY s.FacultyID, s.Name
            """)
            .AsNoTracking()
            .ToListAsync();

        var facultyIds = subscriptions
            .Where(s => s.FacultyID.HasValue)
            .Select(s => s.FacultyID!.Value)
            .Distinct()
            .ToList();

        var faculties = facultyIds.Any()
            ? await _context.Faculty
                .FromSqlRaw("SELECT * FROM Faculty WHERE FacultyID IN ({0})", string.Join(",", facultyIds))
                .AsNoTracking()
                .ToListAsync()
            : new List<Faculty>();

        foreach (var sub in subscriptions.Where(s => s.FacultyID.HasValue))
        {
            sub.Faculty = faculties.FirstOrDefault(f => f.FacultyID == sub.FacultyID!.Value);
        }

        return Ok(subscriptions);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Subscription>> GetById(int id)
    {
        var subscription = await _context.Subscriptions
            .FromSqlRaw("SELECT * FROM Subscriptions WHERE SubscriptionID = @id",
                new SqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (subscription == null)
        {
            return NotFound();
        }

        if (subscription.FacultyID > 0)
        {
            subscription.Faculty = await _context.Faculty
                .FromSqlRaw("SELECT * FROM Faculty WHERE FacultyID = @id",
                    new SqlParameter("@id", subscription.FacultyID))
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        return Ok(subscription);
    }

    [HttpPost("template")]
    public async Task<IActionResult> CreateTemplate([FromBody] Subscription subscription)
    {
        await _context.Database.ExecuteSqlRawAsync("""
            INSERT INTO Subscriptions (FacultyID, Name, StartDate, EndDate, DurationDays)
            VALUES (@facultyId, @name, @startDate, @endDate, @durationDays)
        """,
            new SqlParameter("@facultyId", DBNull.Value),
            new SqlParameter("@name", subscription.Name.Trim()),
            new SqlParameter("@startDate", DBNull.Value),
            new SqlParameter("@endDate", DBNull.Value),
            new SqlParameter("@durationDays", (object?)subscription.DurationDays ?? DBNull.Value)
        );

        return Ok();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM Subscriptions WHERE SubscriptionID = @id",
            new SqlParameter("@id", id)
        );

        return Ok();
    }
}

