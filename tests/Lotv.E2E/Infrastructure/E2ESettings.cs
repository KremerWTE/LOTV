namespace Lotv.E2E.Infrastructure;

/// <summary>
/// Configuration for the E2E test suite.
/// Set E2E_BASE_URL environment variable to override the default local dev URL.
/// Set E2E_HEADLESS=false to watch the browser during local debugging.
/// Set E2E_SLOW_MO=500 (milliseconds) to slow down actions for debugging.
/// </summary>
public static class E2ESettings
{
    /// <summary>Base URL of the Lotv.Web frontend (Blazor WASM).</summary>
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5001";

    /// <summary>Base URL of the Lotv.Api backend.</summary>
    public static string ApiUrl =>
        Environment.GetEnvironmentVariable("E2E_API_URL") ?? "http://localhost:5000";

    /// <summary>Run Chromium headless (true in CI, false for local visual debugging).</summary>
    public static bool Headless =>
        Environment.GetEnvironmentVariable("E2E_HEADLESS") is not "false";

    /// <summary>Milliseconds to slow each Playwright action (0 = off). Useful for debugging.</summary>
    public static float SlowMo =>
        float.TryParse(Environment.GetEnvironmentVariable("E2E_SLOW_MO"), out var v) ? v : 0;

    /// <summary>Default navigation timeout in milliseconds.</summary>
    public static float Timeout => 15_000;

    // ── Seed credentials (match DevSeedData / test accounts) ─────────────────
    public static string AdminUsername => "mary.roberts";
    public static string AdminPassword => "DevPassword1!";
    public static string StaffUsername => "claire.hoffman";
    public static string StaffPassword => "DevPassword1!";
}
