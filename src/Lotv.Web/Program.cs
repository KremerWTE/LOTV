using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Lotv.Core.Services;
using Lotv.Web;
using Lotv.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── HTTP client → LOTV API ────────────────────────────────────────────────────
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:5275")
});

// ── Auth ──────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthService>();

// ── API + real-time ───────────────────────────────────────────────────────────
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<SignalRService>();
builder.Services.AddScoped<AuctionSignalRService>();

// ── Localization ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<LocalizationService>();


var host = builder.Build();

// Restore session from sessionStorage before first render
var auth = host.Services.GetRequiredService<AuthService>();
await auth.TryRestoreSessionAsync();

// Restore preferred culture
var loc = host.Services.GetRequiredService<LocalizationService>();
await loc.InitializeAsync();

await host.RunAsync();
