using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MilkYieldTracker.Api.Data;
using MilkYieldTracker.Api.Models;

namespace MilkYieldTracker.Api.Services;

public record CowYieldCheck(int CowId, string TagNumber, string? Name, YieldCheckResult Result);

/// <summary>
/// Glue between the database and the pure rule in <see cref="YieldDropDetector"/>:
/// it loads the milkings, adds them up per day, and hands them to the detector.
/// </summary>
public class YieldCheckService(MilkDbContext db, YieldDropDetector detector, IOptions<YieldDropOptions> options)
{
    private readonly YieldDropOptions _options = options.Value;

    public async Task<CowYieldCheck> CheckCowAsync(Cow cow, DateOnly date, CancellationToken ct = default)
    {
        var from = date.AddDays(-_options.BaselineDays);

        var sessions = await db.MilkingSessions
            .Where(s => s.CowId == cow.Id && s.Date >= from && s.Date <= date)
            .Select(s => new SessionRow(s.CowId, s.Date, s.Litres))
            .ToListAsync(ct);

        return new CowYieldCheck(cow.Id, cow.TagNumber, cow.Name, detector.Evaluate(ToDailyTotals(sessions), date));
    }

    public async Task<List<CowYieldCheck>> CheckAllActiveAsync(DateOnly date, CancellationToken ct = default)
    {
        var from = date.AddDays(-_options.BaselineDays);

        var cows = await db.Cows
            .Where(c => c.IsActive)
            .OrderBy(c => c.TagNumber)
            .ToListAsync(ct);

        // One query for the whole herd, then split per cow in memory.
        var sessions = await db.MilkingSessions
            .Where(s => s.Cow.IsActive && s.Date >= from && s.Date <= date)
            .Select(s => new SessionRow(s.CowId, s.Date, s.Litres))
            .ToListAsync(ct);

        var sessionsByCow = sessions.ToLookup(s => s.CowId);

        return cows
            .Select(cow => new CowYieldCheck(
                cow.Id,
                cow.TagNumber,
                cow.Name,
                detector.Evaluate(ToDailyTotals(sessionsByCow[cow.Id]), date)))
            .ToList();
    }

    private record SessionRow(int CowId, DateOnly Date, decimal Litres);

    // Adds the milkings of each day together: litres summed, milkings counted.
    // Done in memory (not in SQL) so it behaves the same on every database.
    private static Dictionary<DateOnly, DailyYield> ToDailyTotals(IEnumerable<SessionRow> sessions) =>
        sessions
            .GroupBy(s => s.Date)
            .ToDictionary(g => g.Key, g => new DailyYield(g.Sum(s => s.Litres), g.Count()));
}
