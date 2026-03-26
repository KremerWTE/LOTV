using Lotv.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// Shared test server factory. Replaces the production SQLite DB with a
/// per-instance temp file so each test class gets a clean, isolated schema.
/// JWT config is overridden to use a deterministic test key.
/// </summary>
public class LotvApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"lotv-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Override DB connection to isolated temp file
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<LotvDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<LotvDbContext>(o =>
                o.UseSqlite($"Data Source={_dbPath}"));
        });

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Deterministic 512-bit test secret — never use in production
                ["Jwt:Key"]          = "LOTVTestSecretKeyThatIsLongEnoughForHS256Testing2026!",
                ["Jwt:Issuer"]       = "lotv-test",
                ["Jwt:Audience"]     = "lotv-test",
                ["Testing:SkipSeed"] = "true"   // prevent mock seed data from running in tests
            });
        });
    }

    public async Task InitializeAsync()
    {
        // Create DB schema before any test runs
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        // SQLite file may still be held by the background service for a moment;
        // suppress the IOException and let the OS clean it up from temp.
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); }
        catch (IOException) { }
    }
}
