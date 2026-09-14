using Microsoft.AspNetCore.Components.Authorization;
using Lotv.Core.Services;
using Lotv.Web;
using Lotv.Web.Services;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services);

    // MSSqlServer sink — wired in code so a missing/empty connection string
    // never crashes the app. Config-driven resolution can't guard against null.
    if (!ctx.HostingEnvironment.IsDevelopment())
    {
        var connStr = ctx.Configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connStr))
            cfg.WriteTo.MSSqlServer(
                connectionString: connStr,
                tableName: "AppLogs",
                schemaName: "dbo",
                autoCreateSqlTable: true,
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning);
    }
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── HTTP client → LOTV API ────────────────────────────────────────────────────
// Runs server-side now (Blazor Server), same as every other outbound call in
// this app - ApiBaseUrl still comes from config (appsettings.{Environment}.json
// under the server's own config system now, not wwwroot), same key
// SignalRService / AuctionSignalRService already read.
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5275")
});

// ── Auth ──────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthService>();

// ── API + real-time ───────────────────────────────────────────────────────────
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<SignalRService>();
builder.Services.AddScoped<AuctionSignalRService>();

// ── Localization + currency ───────────────────────────────────────────────────
// LocalizationService was Singleton under WASM, where one app instance is one
// user anyway. On the server, a Singleton can't consume the Scoped
// IJSRuntime (each circuit/user gets its own) - DI validation catches this
// correctly. Scoped is also the behaviorally right lifetime now that a
// single server process serves multiple concurrent users.
builder.Services.AddScoped<LocalizationService>();
builder.Services.AddScoped<CurrencyService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
