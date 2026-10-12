using Microsoft.Extensions.Options;
using MilkYieldTracker.Api.Services;

namespace MilkYieldTracker.Tests;

/// <summary>Tests for the yield-drop rule on its own (no database, no web server).</summary>
public class YieldDropDetectorTests
{
    private static readonly DateOnly Day = new(2026, 10, 12);

    private static YieldDropDetector CreateDetector(decimal thresholdPercent = 15m) =>
        new(Options.Create(new YieldDropOptions { DropThresholdPercent = thresholdPercent }));

    /// <summary>The previous <paramref name="days"/> days, each with the same litres and a full set of milkings.</summary>
    private static Dictionary<DateOnly, DailyYield> History(decimal litresPerDay, int days = 7) =>
        Enumerable.Range(1, days).ToDictionary(
            back => Day.AddDays(-back),
            _ => new DailyYield(litresPerDay, SessionCount: 2));

    [Fact]
    public void Flags_a_drop_larger_than_the_threshold()
    {
        var days = History(20m);
        days[Day] = new DailyYield(16m, 2); // 20% below her usual 20 L

        var result = CreateDetector().Evaluate(days, Day);

        Assert.Equal(YieldStatus.Dropped, result.Status);
        Assert.Equal(20.0m, result.DropPercent);
        Assert.Equal(20.00m, result.BaselineAverageLitres);
        Assert.Equal(7, result.BaselineDaysUsed);
    }

    [Fact]
    public void Does_not_flag_a_drop_smaller_than_the_threshold()
    {
        var days = History(20m);
        days[Day] = new DailyYield(18m, 2); // 10% down

        var result = CreateDetector().Evaluate(days, Day);

        Assert.Equal(YieldStatus.Normal, result.Status);
        Assert.Equal(10.0m, result.DropPercent);
    }

    [Fact]
    public void A_drop_of_exactly_the_threshold_is_not_flagged()
    {
        var days = History(20m);
        days[Day] = new DailyYield(17m, 2); // exactly 15% down; the rule says "more than"

        var result = CreateDetector().Evaluate(days, Day);

        Assert.Equal(YieldStatus.Normal, result.Status);
    }

    [Fact]
    public void A_higher_yield_than_usual_is_normal()
    {
        var days = History(20m);
        days[Day] = new DailyYield(24m, 2);

        var result = CreateDetector().Evaluate(days, Day);

        Assert.Equal(YieldStatus.Normal, result.Status);
        Assert.True(result.DropPercent < 0);
    }

    [Fact]
    public void Uses_the_threshold_from_the_settings()
    {
        var days = History(20m);
        days[Day] = new DailyYield(16m, 2); // 20% down

        Assert.Equal(YieldStatus.Normal, CreateDetector(thresholdPercent: 30m).Evaluate(days, Day).Status);
        Assert.Equal(YieldStatus.Dropped, CreateDetector(thresholdPercent: 10m).Evaluate(days, Day).Status);
    }

    [Fact]
    public void Reports_when_there_is_no_data_for_the_day()
    {
        var result = CreateDetector().Evaluate(History(20m), Day);

        Assert.Equal(YieldStatus.NoDataForDate, result.Status);
        Assert.Null(result.DropPercent);
    }

    [Fact]
    public void A_half_recorded_day_is_not_compared()
    {
        var days = History(20m);
        days[Day] = new DailyYield(9m, SessionCount: 1); // evening milking not recorded yet

        var result = CreateDetector().Evaluate(days, Day);

        Assert.Equal(YieldStatus.IncompleteDay, result.Status);
        Assert.Null(result.DropPercent);
    }

    [Fact]
    public void Needs_enough_history_before_judging_a_cow()
    {
        var days = History(20m, days: 2); // only 2 earlier days; the minimum is 3
        days[Day] = new DailyYield(10m, 2);

        var result = CreateDetector().Evaluate(days, Day);

        Assert.Equal(YieldStatus.InsufficientBaseline, result.Status);
        Assert.Equal(2, result.BaselineDaysUsed);
    }

    [Fact]
    public void Missing_days_are_skipped_not_counted_as_zero()
    {
        var days = History(20m, days: 3); // days 1-3 recorded, days 4-7 missing
        days[Day] = new DailyYield(16m, 2);

        var result = CreateDetector().Evaluate(days, Day);

        Assert.Equal(YieldStatus.Dropped, result.Status);
        Assert.Equal(3, result.BaselineDaysUsed);
        Assert.Equal(20.00m, result.BaselineAverageLitres); // would be far lower if gaps counted as zero
    }

    [Fact]
    public void Half_recorded_days_inside_the_baseline_are_ignored()
    {
        var days = History(20m);
        days[Day.AddDays(-6)] = new DailyYield(10m, SessionCount: 1);
        days[Day.AddDays(-7)] = new DailyYield(10m, SessionCount: 1);
        days[Day] = new DailyYield(20m, 2);

        var result = CreateDetector().Evaluate(days, Day);

        Assert.Equal(YieldStatus.Normal, result.Status);
        Assert.Equal(5, result.BaselineDaysUsed);
        Assert.Equal(20.00m, result.BaselineAverageLitres);
    }

    [Fact]
    public void Days_older_than_the_window_are_not_part_of_the_baseline()
    {
        var days = History(20m); // days 1-7 at 20 L
        days[Day.AddDays(-30)] = new DailyYield(100m, 2); // far outside the 7-day window
        days[Day] = new DailyYield(20m, 2);

        var result = CreateDetector().Evaluate(days, Day);

        Assert.Equal(7, result.BaselineDaysUsed);
        Assert.Equal(20.00m, result.BaselineAverageLitres);
    }

    [Fact]
    public void A_zero_baseline_does_not_divide_by_zero()
    {
        var days = History(0m);
        days[Day] = new DailyYield(0m, 2);

        var result = CreateDetector().Evaluate(days, Day);

        Assert.Equal(YieldStatus.Normal, result.Status);
        Assert.Equal(0m, result.DropPercent);
    }
}
