using Microsoft.AspNetCore.Components.Authorization;
using Lotv.Core.Services;
using Lotv.Web;
using Lotv.Web.Services;

var builder = WebApplication.CreateBuilder(args);

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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
