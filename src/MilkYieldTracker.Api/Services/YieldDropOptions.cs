namespace MilkYieldTracker.Api.Services;

/// <summary>
/// Settings for the yield-drop rule. They live in appsettings.json under "YieldDrop",
/// so the rule can be tuned without touching code.
/// </summary>
public class YieldDropOptions
{
    public const string SectionName = "YieldDrop";

    /// <summary>A cow is flagged when her daily total is MORE than this percentage below her own baseline.</summary>
    public decimal DropThresholdPercent { get; set; } = 15m;

    /// <summary>How many calendar days before the checked day make up the baseline window.</summary>
    public int BaselineDays { get; set; } = 7;

    /// <summary>Minimum number of complete days inside the window before a baseline is trusted.</summary>
    public int MinBaselineDays { get; set; } = 3;

    /// <summary>Milkings per day on this farm. A day only counts as complete when all of them are recorded.</summary>
    public int SessionsPerDay { get; set; } = 2;
}
