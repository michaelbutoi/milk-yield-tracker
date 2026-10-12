using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MilkYieldTracker.Api.Data;

namespace MilkYieldTracker.Tests;

/// <summary>
/// Starts the real API in memory for tests, but with a throwaway in-memory SQLite database
/// instead of SQL Server, so the tests need no database server and cannot touch real data.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    // An in-memory SQLite database lives only as long as its connection, so keep one open.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // Program.cs skips registering SQL Server in this environment

        builder.ConfigureServices(services =>
        {
            _connection.Open();
            services.AddDbContext<MilkDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<MilkDbContext>().Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
