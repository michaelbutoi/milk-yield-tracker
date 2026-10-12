namespace MilkYieldTracker.Api.Models;

/// <summary>One animal in the herd, identified by her ear-tag number.</summary>
public class Cow
{
    public int Id { get; set; }

    /// <summary>Ear-tag number as written on the farm. Must be unique.</summary>
    public string TagNumber { get; set; } = string.Empty;

    public string? Name { get; set; }

    /// <summary>False once a cow has been culled or sold; inactive cows are skipped by the yield check.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<MilkingSession> Sessions { get; set; } = new();
}
