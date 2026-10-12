namespace MilkYieldTracker.Api.Models;

public enum MilkingPeriod
{
    Morning = 1,
    Evening = 2
}

/// <summary>The litres one cow gave at one milking (a cow is milked once per period per day).</summary>
public class MilkingSession
{
    public int Id { get; set; }

    public int CowId { get; set; }
    public Cow Cow { get; set; } = null!;

    public DateOnly Date { get; set; }
    public MilkingPeriod Period { get; set; }

    public decimal Litres { get; set; }
}
