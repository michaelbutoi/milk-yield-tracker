using Microsoft.Extensions.Options;

namespace MilkYieldTracker.Api.Services;

/// <summary>Total litres for one cow on one day, and how many milkings make up that total.</summary>
public readonly record struct DailyYield(decimal Litres, int SessionCount);

public enum YieldStatus
{
    /// <summary>Within the allowed range of the cow's own baseline.</summary>
    Normal,

    /// <summary>More than the threshold below the cow's own baseline.</summary>
    Dropped,

    /// <summary>Nothing recorded for the day being checked.</summary>
    NoDataForDate,

    /// <summary>Some milkings recorded, but not all of them yet. Comparing a half day would give a false alarm.</summary>
    IncompleteDay,

    /// <summary>Too few complete days before this one to build a fair baseline (e.g. a newly bought cow).</summary>
    InsufficientBaseline
}

public record YieldCheckResult(
    DateOnly Date,
    YieldStatus Status,
    decimal? LitresOnDate,
    decimal? BaselineAverageLitres,
    int BaselineDaysUsed,
    decimal? DropPercent);

/// <summary>
/// The yield-drop rule, kept free of any database code so it is easy to read and to test.
///
/// Rule: compare a cow's total litres for a day with the average of her own complete days
/// in the preceding window (7 days by default). If today is more than the threshold
/// (15% by default) below that average, flag her.
/// </summary>
public class YieldDropDetector(IOptions<YieldDropOptions> options)
{
    private readonly YieldDropOptions _options = options.Value;

    public YieldCheckResult Evaluate(IReadOnlyDictionary<DateOnly, DailyYield> daysByDate, DateOnly date)
    {
        if (!daysByDate.TryGetValue(date, out var today))
        {
            return new YieldCheckResult(date, YieldStatus.NoDataForDate, null, null, 0, null);
        }

        if (today.SessionCount < _options.SessionsPerDay)
        {
            return new YieldCheckResult(date, YieldStatus.IncompleteDay, today.Litres, null, 0, null);
        }

        // Baseline = the cow's own complete days from the window before the checked day.
        // Days with no records (or half-recorded days) are skipped, not counted as zero.
        var baseline = Enumerable.Range(1, _options.BaselineDays)
            .Select(daysBack => date.AddDays(-daysBack))
            .Where(d => daysByDate.TryGetValue(d, out var day) && day.SessionCount >= _options.SessionsPerDay)
            .Select(d => daysByDate[d].Litres)
            .ToList();

        if (baseline.Count < _options.MinBaselineDays)
        {
            return new YieldCheckResult(date, YieldStatus.InsufficientBaseline, today.Litres, null, baseline.Count, null);
        }

        var average = baseline.Average();
        var dropPercent = average == 0 ? 0m : (average - today.Litres) / average * 100m;

        // Strictly "more than": a drop of exactly the threshold is not flagged.
        var status = dropPercent > _options.DropThresholdPercent ? YieldStatus.Dropped : YieldStatus.Normal;

        return new YieldCheckResult(
            date,
            status,
            today.Litres,
            Math.Round(average, 2),
            baseline.Count,
            Math.Round(dropPercent, 1));
    }
}
