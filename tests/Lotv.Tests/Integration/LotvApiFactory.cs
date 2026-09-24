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

        // Override DB connection to isolated temp file — remove ALL EF registrations
        // for LotvDbContext before re-adding with SQLite, so EF doesn't see two providers.
        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<LotvDbContext>)
                         || d.ServiceType == typeof(DbContext)
                         || (d.ServiceType.IsGenericType &&
                             d.ServiceType.GetGenericArguments().Contains(typeof(LotvDbContext)) &&
                             d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration")))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<LotvDbContext>(o =>
                o.UseSqlite($"Data Source={_dbPath}"));
        });

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Deterministic 512-bit test secret — never use in production
                ["Jwt:Key"]           = "LOTVTestSecretKeyThatIsLongEnoughForHS256Testing2026!",
                ["Jwt:Issuer"]        = "lotv-test",
                ["Jwt:Audience"]      = "lotv-test",
                ["Testing:SkipSeed"]  = "true",
                // The suite creates accounts of every role through /auth/register; it is admin-only everywhere else
                ["Auth:AllowOpenRegistration"] = "true",
                // Force SQLite branch in Program.cs so the DbContext override below works
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}"
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
