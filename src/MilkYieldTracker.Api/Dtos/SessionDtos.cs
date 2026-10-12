using System.ComponentModel.DataAnnotations;
using MilkYieldTracker.Api.Models;

namespace MilkYieldTracker.Api.Dtos;

public record RecordSessionRequest(
    DateOnly Date,
    MilkingPeriod Period,
    [Range(0.1, 60.0, ErrorMessage = "Litres must be between 0.1 and 60 for one milking.")] decimal Litres);

public record SessionDto(int Id, int CowId, DateOnly Date, MilkingPeriod Period, decimal Litres)
{
    public static SessionDto From(MilkingSession s) => new(s.Id, s.CowId, s.Date, s.Period, s.Litres);
}
