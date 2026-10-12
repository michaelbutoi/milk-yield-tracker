using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MilkYieldTracker.Api.Services;

namespace MilkYieldTracker.Api.Controllers;

public record YieldDropReport(
    DateOnly Date,
    decimal ThresholdPercent,
    int CowsChecked,
    IReadOnlyList<CowYieldCheck> FlaggedCows);

[ApiController]
[Route("api/alerts")]
public class AlertsController(YieldCheckService yieldChecks, IOptions<YieldDropOptions> options) : ControllerBase
{
    // GET /api/alerts/yield-drops?date=2026-10-12   (date defaults to today)
    // The herd-wide answer to "which cows should I look at?"
    [HttpGet("yield-drops")]
    public async Task<ActionResult<YieldDropReport>> YieldDrops([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var day = date ?? DateOnly.FromDateTime(DateTime.Now);
        var checks = await yieldChecks.CheckAllActiveAsync(day, ct);

        var flagged = checks
            .Where(c => c.Result.Status == YieldStatus.Dropped)
            .OrderByDescending(c => c.Result.DropPercent)
            .ToList();

        return Ok(new YieldDropReport(day, options.Value.DropThresholdPercent, checks.Count, flagged));
    }
}
