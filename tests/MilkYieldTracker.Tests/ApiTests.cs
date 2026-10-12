using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MilkYieldTracker.Api.Controllers;
using MilkYieldTracker.Api.Dtos;
using MilkYieldTracker.Api.Models;
using MilkYieldTracker.Api.Services;

namespace MilkYieldTracker.Tests;

/// <summary>End-to-end tests: real HTTP requests into the real API, backed by a throwaway database.</summary>
public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // Each test uses its own tag so tests do not interfere with each other.
    private static string NewTag() => "T" + Guid.NewGuid().ToString("N")[..11];

    private async Task<CowDto> CreateCowAsync(string? tag = null)
    {
        var response = await _client.PostAsJsonAsync("/api/cows", new { tagNumber = tag ?? NewTag(), name = "Test cow" }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CowDto>(Json))!;
    }

    private Task<HttpResponseMessage> PostSessionAsync(int cowId, DateOnly date, string period, decimal litres) =>
        _client.PostAsJsonAsync($"/api/cows/{cowId}/sessions", new { date, period, litres }, Json);

    /// <summary>Records a full day (morning + evening) of milkings, splitting the total evenly.</summary>
    private async Task RecordFullDayAsync(int cowId, DateOnly date, decimal totalLitres)
    {
        var half = totalLitres / 2;
        Assert.Equal(HttpStatusCode.Created, (await PostSessionAsync(cowId, date, "Morning", half)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostSessionAsync(cowId, date, "Evening", half)).StatusCode);
    }

    private async Task RecordBaselineWeekAsync(int cowId, decimal litresPerDay)
    {
        for (var back = 1; back <= 7; back++)
        {
            await RecordFullDayAsync(cowId, Today.AddDays(-back), litresPerDay);
        }
    }

    private async Task<YieldDropReport> GetReportAsync() =>
        (await _client.GetFromJsonAsync<YieldDropReport>($"/api/alerts/yield-drops?date={Today:yyyy-MM-dd}", Json))!;

    [Fact]
    public async Task Creating_a_cow_with_a_tag_already_in_use_is_rejected()
    {
        var tag = NewTag();
        await CreateCowAsync(tag);

        var duplicate = await _client.PostAsJsonAsync("/api/cows", new { tagNumber = tag }, Json);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Creating_a_cow_without_a_tag_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/cows", new { tagNumber = "" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100)]
    public async Task Litres_outside_a_believable_range_are_rejected(decimal litres)
    {
        var cow = await CreateCowAsync();

        var response = await PostSessionAsync(cow.Id, Today, "Morning", litres);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_milking_in_the_future_is_rejected()
    {
        var cow = await CreateCowAsync();

        var response = await PostSessionAsync(cow.Id, Today.AddDays(1), "Morning", 12m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_same_milking_cannot_be_recorded_twice()
    {
        var cow = await CreateCowAsync();
        await PostSessionAsync(cow.Id, Today, "Morning", 12m);

        var again = await PostSessionAsync(cow.Id, Today, "Morning", 13m);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Recording_for_a_cow_that_does_not_exist_returns_not_found()
    {
        var response = await PostSessionAsync(cowId: 999_999, Today, "Morning", 12m);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Recorded_milkings_can_be_read_back_in_date_order()
    {
        var cow = await CreateCowAsync();
        await PostSessionAsync(cow.Id, Today, "Evening", 9m);
        await PostSessionAsync(cow.Id, Today, "Morning", 11m);
        await PostSessionAsync(cow.Id, Today.AddDays(-1), "Morning", 10m);

        var sessions = await _client.GetFromJsonAsync<List<SessionDto>>($"/api/cows/{cow.Id}/sessions", Json);

        Assert.NotNull(sessions);
        Assert.Equal(3, sessions.Count);
        Assert.Equal(Today.AddDays(-1), sessions[0].Date);
        Assert.Equal(MilkingPeriod.Morning, sessions[1].Period);
        Assert.Equal(MilkingPeriod.Evening, sessions[2].Period);
    }

    [Fact]
    public async Task The_alert_report_flags_only_the_cow_whose_yield_dropped_too_far()
    {
        var droppedCow = await CreateCowAsync();
        var steadyCow = await CreateCowAsync();
        var smallDipCow = await CreateCowAsync();
        var halfDayCow = await CreateCowAsync();

        foreach (var cow in new[] { droppedCow, steadyCow, smallDipCow, halfDayCow })
        {
            await RecordBaselineWeekAsync(cow.Id, litresPerDay: 20m);
        }

        await RecordFullDayAsync(droppedCow.Id, Today, 16m);   // 20% down: flagged
        await RecordFullDayAsync(steadyCow.Id, Today, 20m);    // as usual
        await RecordFullDayAsync(smallDipCow.Id, Today, 18m);  // 10% down: under the 15% rule
        await PostSessionAsync(halfDayCow.Id, Today, "Morning", 8m); // evening not milked/recorded yet

        var report = await GetReportAsync();

        var flagged = Assert.Single(report.FlaggedCows, c => c.CowId == droppedCow.Id);
        Assert.Equal(YieldStatus.Dropped, flagged.Result.Status);
        Assert.Equal(20.0m, flagged.Result.DropPercent);

        Assert.DoesNotContain(report.FlaggedCows, c => c.CowId == steadyCow.Id);
        Assert.DoesNotContain(report.FlaggedCows, c => c.CowId == smallDipCow.Id);
        Assert.DoesNotContain(report.FlaggedCows, c => c.CowId == halfDayCow.Id);
        Assert.Equal(15m, report.ThresholdPercent);
    }

    [Fact]
    public async Task The_single_cow_check_explains_why_a_cow_was_judged_the_way_she_was()
    {
        var cow = await CreateCowAsync();
        await RecordBaselineWeekAsync(cow.Id, 20m);
        await RecordFullDayAsync(cow.Id, Today, 16m);

        var check = await _client.GetFromJsonAsync<CowYieldCheck>(
            $"/api/cows/{cow.Id}/yield-check?date={Today:yyyy-MM-dd}", Json);

        Assert.NotNull(check);
        Assert.Equal(YieldStatus.Dropped, check.Result.Status);
        Assert.Equal(16m, check.Result.LitresOnDate);
        Assert.Equal(20.00m, check.Result.BaselineAverageLitres);
        Assert.Equal(7, check.Result.BaselineDaysUsed);
    }

    [Fact]
    public async Task An_inactive_cow_is_left_out_of_the_alerts_and_cannot_be_milked()
    {
        var cow = await CreateCowAsync();
        await RecordBaselineWeekAsync(cow.Id, 20m);
        await RecordFullDayAsync(cow.Id, Today, 10m); // would be flagged if she were active

        var update = await _client.PutAsJsonAsync($"/api/cows/{cow.Id}", new { name = "Sold", isActive = false }, Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var report = await GetReportAsync();
        Assert.DoesNotContain(report.FlaggedCows, c => c.CowId == cow.Id);

        var newMilking = await PostSessionAsync(cow.Id, Today.AddDays(-20), "Morning", 12m);
        Assert.Equal(HttpStatusCode.BadRequest, newMilking.StatusCode);
    }
}
