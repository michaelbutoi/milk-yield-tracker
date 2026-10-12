using Microsoft.EntityFrameworkCore;
using MilkYieldTracker.Api.Models;

namespace MilkYieldTracker.Api.Data;

/// <summary>
/// Loads a small, MADE-UP herd so the API has something to show on first run.
/// None of this is real farm data. Runs only in Development, only when the database has no cows,
/// and only when "Seed:DemoData" is true.
/// </summary>
public static class DemoDataSeeder
{
    public static async Task SeedAsync(MilkDbContext db)
    {
        if (await db.Cows.AnyAsync())
        {
            return;
        }

        // TodayFactor is how today's total compares with the cow's usual day (1.0 = usual).
        var herd = new (string Tag, string Name, decimal UsualLitresPerDay, decimal TodayFactor, int DaysOfData)[]
        {
            ("0101", "Daisy",  24m, 0.70m, 21), // about 30% down: should be flagged
            ("0102", "Bella",  21m, 0.90m, 21), // about 10% down: noticeable, but under the 15% rule
            ("0103", "Rosie",  26m, 1.00m, 21), // normal
            ("0104", "Molly",  19m, 1.00m, 21), // normal
            ("0105", "Lulu",   20m, 1.00m, 2),  // newly added: not enough history for a baseline
            ("0106", "Hettie", 22m, 0.80m, 21), // about 20% down: should be flagged
        };

        var random = new Random(42); // fixed seed so the demo data is the same every time
        var today = DateOnly.FromDateTime(DateTime.Now);

        foreach (var entry in herd)
        {
            var cow = new Cow { TagNumber = entry.Tag, Name = entry.Name };

            for (var daysBack = entry.DaysOfData - 1; daysBack >= 0; daysBack--)
            {
                // Ordinary days vary by up to +/-4% around the usual yield.
                var factor = daysBack == 0
                    ? entry.TodayFactor
                    : 1m + (decimal)(random.NextDouble() * 0.08 - 0.04);

                var dayTotal = entry.UsualLitresPerDay * factor;
                var date = today.AddDays(-daysBack);

                cow.Sessions.Add(new MilkingSession
                {
                    Date = date,
                    Period = MilkingPeriod.Morning,
                    Litres = Math.Round(dayTotal * 0.55m, 1)
                });
                cow.Sessions.Add(new MilkingSession
                {
                    Date = date,
                    Period = MilkingPeriod.Evening,
                    Litres = Math.Round(dayTotal * 0.45m, 1)
                });
            }

            db.Cows.Add(cow);
        }

        await db.SaveChangesAsync();
    }
}
