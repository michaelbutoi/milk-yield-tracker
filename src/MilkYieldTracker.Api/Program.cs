using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MilkYieldTracker.Api.Data;
using MilkYieldTracker.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    // Show "Morning" / "Dropped" in JSON instead of 1 / 0, so responses read like English.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

// The integration tests register their own (SQLite) database in the "Testing" environment.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<MilkDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("MilkDb")));
}

// The rule's settings come from appsettings.json ("YieldDrop" section).
builder.Services.Configure<YieldDropOptions>(builder.Configuration.GetSection(YieldDropOptions.SectionName));
builder.Services.AddSingleton<YieldDropDetector>();
builder.Services.AddScoped<YieldCheckService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Development convenience: create/update the database and optionally load made-up demo data.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MilkDbContext>();
    await db.Database.MigrateAsync();

    if (app.Configuration.GetValue<bool>("Seed:DemoData"))
    {
        await DemoDataSeeder.SeedAsync(db);
    }
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

// Lets the test project start the whole app in memory.
public partial class Program;
