using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MilkYieldTracker.Api.Data;
using MilkYieldTracker.Api.Dtos;
using MilkYieldTracker.Api.Models;

namespace MilkYieldTracker.Api.Controllers;

[ApiController]
[Route("api/cows/{cowId:int}/sessions")]
public class SessionsController(MilkDbContext db) : ControllerBase
{
    // GET /api/cows/5/sessions?from=2026-10-01&to=2026-10-12   (defaults to the last 14 days)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SessionDto>>> GetForCow(
        int cowId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        if (!await db.Cows.AnyAsync(c => c.Id == cowId))
        {
            return NotFound();
        }

        var end = to ?? DateOnly.FromDateTime(DateTime.Now);
        var start = from ?? end.AddDays(-14);

        var sessions = await db.MilkingSessions
            .Where(s => s.CowId == cowId && s.Date >= start && s.Date <= end)
            .ToListAsync();

        // Period is stored as text, so ordering by it in SQL would sort alphabetically
        // (Evening before Morning). Sorting here uses the enum order: Morning, then Evening.
        return Ok(sessions
            .OrderBy(s => s.Date)
            .ThenBy(s => s.Period)
            .Select(SessionDto.From));
    }

    // POST /api/cows/5/sessions
    [HttpPost]
    public async Task<ActionResult<SessionDto>> Record(int cowId, RecordSessionRequest request)
    {
        var cow = await db.Cows.FindAsync(cowId);
        if (cow is null)
        {
            return NotFound();
        }

        if (!cow.IsActive)
        {
            return Problem(
                title: "Cow is inactive",
                detail: "Milkings cannot be recorded for a cow marked inactive.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.Date > DateOnly.FromDateTime(DateTime.Now))
        {
            return Problem(
                title: "Date is in the future",
                detail: "A milking cannot be recorded for a date that has not happened yet.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!Enum.IsDefined(request.Period))
        {
            return Problem(
                title: "Unknown milking period",
                detail: "Period must be 'Morning' or 'Evening'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var alreadyRecorded = await db.MilkingSessions.AnyAsync(s =>
            s.CowId == cowId && s.Date == request.Date && s.Period == request.Period);

        if (alreadyRecorded)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Milking already recorded",
                Detail = $"Cow {cow.TagNumber} already has a {request.Period} milking for {request.Date:yyyy-MM-dd}.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var session = new MilkingSession
        {
            CowId = cowId,
            Date = request.Date,
            Period = request.Period,
            Litres = request.Litres
        };

        db.MilkingSessions.Add(session);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetForCow), new { cowId }, SessionDto.From(session));
    }
}
