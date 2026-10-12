using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MilkYieldTracker.Api.Data;
using MilkYieldTracker.Api.Dtos;
using MilkYieldTracker.Api.Models;
using MilkYieldTracker.Api.Services;

namespace MilkYieldTracker.Api.Controllers;

[ApiController]
[Route("api/cows")]
public class CowsController(MilkDbContext db, YieldCheckService yieldChecks) : ControllerBase
{
    // GET /api/cows?includeInactive=true
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CowDto>>> GetAll([FromQuery] bool includeInactive = false)
    {
        var query = db.Cows.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var cows = await query.OrderBy(c => c.TagNumber).ToListAsync();
        return Ok(cows.Select(CowDto.From));
    }

    // GET /api/cows/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CowDto>> GetById(int id)
    {
        var cow = await db.Cows.FindAsync(id);
        return cow is null ? NotFound() : Ok(CowDto.From(cow));
    }

    // POST /api/cows
    [HttpPost]
    public async Task<ActionResult<CowDto>> Create(CreateCowRequest request)
    {
        var tag = request.TagNumber.Trim();

        if (await db.Cows.AnyAsync(c => c.TagNumber == tag))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Tag number already in use",
                Detail = $"A cow with tag '{tag}' already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var cow = new Cow { TagNumber = tag, Name = request.Name?.Trim() };
        db.Cows.Add(cow);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = cow.Id }, CowDto.From(cow));
    }

    // PUT /api/cows/5  (rename, or mark a cow inactive once she is sold or culled)
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CowDto>> Update(int id, UpdateCowRequest request)
    {
        var cow = await db.Cows.FindAsync(id);
        if (cow is null)
        {
            return NotFound();
        }

        cow.Name = request.Name?.Trim();
        cow.IsActive = request.IsActive;
        await db.SaveChangesAsync();

        return Ok(CowDto.From(cow));
    }

    // GET /api/cows/5/yield-check?date=2026-10-12   (date defaults to today)
    [HttpGet("{id:int}/yield-check")]
    public async Task<ActionResult<CowYieldCheck>> YieldCheck(int id, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        var cow = await db.Cows.FindAsync([id], ct);
        if (cow is null)
        {
            return NotFound();
        }

        var day = date ?? DateOnly.FromDateTime(DateTime.Now);
        return Ok(await yieldChecks.CheckCowAsync(cow, day, ct));
    }
}
