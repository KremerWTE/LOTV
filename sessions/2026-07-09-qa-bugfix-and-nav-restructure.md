# Session Notes — 2026-07-09: QA Bug-Fix Pass, Nav Restructure, Duda/GiveButter Local Wiring

## Summary

Started from a status check ("what is the status of the site") and a request to link the Duda and GiveButter platforms into the LOTV app. Along the way, running the app for the first time in this session surfaced a fatal crash and several correctness bugs that had shipped silently in prior batches. Fixed all of them, then restructured the admin sidebar to match `docs/mockup-admin-dashboard.html`. All builds passed 0 errors / 0 warnings after every change.

---

## Duda + GiveButter Integration

- Confirmed both integrations already had working server-side code from earlier batches:
  - Duda: `POST /api/v1/webhooks/duda` (Program.cs) — parses Duda form submissions into `RetreatRegistration` rows, optional HMAC verification via `Duda:WebhookSecret`
  - GiveButter: `POST /api/v1/payments/givebutter/webhook` + `POST /api/v1/givebutter/sync` — webhook + manual sync, signature verification, retreat-campaign linking
- Blocker: neither had a public URL to receive webhooks (Phase 6 hosting still not chosen)
- Unblocked locally: installed `cloudflared` (winget), ran the API locally, and exposed it via a cloudflared quick tunnel to get a temporary public HTTPS URL
- Set up `dotnet user-secrets` for `Lotv.Api` (previously not configured) so real API keys never need to touch the git-tracked `appsettings.Development.json`
- Verified both webhook endpoints reachable through the tunnel
- **Still needed for production use**: a durable (non-quick-tunnel) public URL once real hosting is chosen — quick-tunnel URLs are session-only and change on every restart

---

## Bugs Found & Fixed

All of these were found by actually running the app end-to-end (not just building it) — the build had been green the whole time.

### 1. Fatal duplicate-route crash (app-breaking)
Two pairs of pages shared identical `@page` routes:
- `ImpactDashboard.razor` and `ImpactReport.razor` both claimed `/admin/impact`
- `Migrations.razor` and `MigrationStatus.razor` both claimed `/admin/migrations`

Blazor WASM throws `InvalidOperationException: ambiguous routes` when building its route table, which crashed the router on **every single page load**, not just those routes. Fixed by rerouting the second page in each pair to `/admin/impact-report` and `/admin/migration-status` (chosen because `CommandPalette.razor` already referenced `/admin/impact-report` as a distinct entry, confirming the intended route).

### 2. Dashboard stuck on infinite "Loading dashboard…" spinner
`SignalRService.cs` and `AuctionSignalRService.cs` both defaulted to `https://localhost:7100` when `config["ApiBaseUrl"]` was unset — which it always was, since no `appsettings.json` in `Lotv.Web` ever set that key. The real API runs on `http://localhost:5275` (matches the hardcoded `HttpClient.BaseAddress` in `Program.cs`). The failed SignalR connection attempt threw an unhandled exception inside `OnInitializedAsync`, which silently aborted the rest of dashboard initialization before the actual data-loading calls ran. Fixed both fallback URLs.

### 3. Every user sees a GUID instead of their name
`JwtTokenService.CreateAccessToken` never included the user's first/last name as claims — only `Sub`, `Email`, `role`, `NameIdentifier` (the user ID), and `chapterId`. Client-side, `AuthService.UserName` tried the `nameidentifier` claim first (always present, since it's just the ID) before ever falling back to email. Net effect: **every logged-in user, in every environment, saw their raw GUID** in the top-right corner instead of "Mary Roberts". Fixed by adding `ClaimTypes.GivenName`/`Surname` to the JWT and rewriting `UserName` to prefer those.

### 4. ~57 HTML entity codes rendering as literal text
Pages used numeric HTML entities like `&#128269;` (🔍) inside `placeholder="..."` attributes and C# `@()` expressions expecting them to render as icons. Blazor sets attribute values and text-expression output via DOM APIs, not an HTML parser, so entity codes are **never decoded** in those two contexts — only entities written directly as plain markup text (e.g. `<div>&#10003;</div>`) get decoded, because the Razor compiler parses that at compile time. Found and fixed via repo-wide sweep: 22 files had genuine bugs; bulk-converted every numeric entity across 208 touched files to its literal Unicode character (safe no-op for the ones that were already fine). One followup fix needed: a `&#13;&#10;` pair inside a `<textarea placeholder>` in `MarketingEmail.razor` initially converted to a raw embedded newline — left as-is since it's valid Razor markup (not a C# string literal) and renders correctly as a real line break.

### 5. Sidebar navigation didn't match the design mockup
User flagged the live admin dashboard didn't look like `docs/mockup-admin-dashboard.html`. The mockup groups a small, curated nav under Overview / Donations / Programs / Reports / Admin. The live sidebar had grown organically over 8+ batches into a flat ~75-visible-link (101 total href) list under only 4 section labels (Overview / Donations / People / Admin), with reporting pages dumped into the same bucket as system settings.

Restructured without deleting any page:
- Renamed "People" → "Programs" (matches mockup)
- Split the 38-link "Admin" section into a new "Reports" section (18 analytics/reporting pages: Impact Dashboard/Report, Health Score, Resource Forecast, Export, Reconciliation, Audit Log, Staff Performance, Chapter Analytics/Health, Campaign ROI, Weekly Digest, Goal Tracker, Report History, Financial Overview, Resource Flow, Budget Summary, Scheduled Reports) and a trimmed 21-link "Admin" section (true system/config: Settings, Users, API Keys, Platform Health, Migrations, Webhooks, Chapters, Grants, Campaigns, etc.)
- Verified via href-set diff before/after that all 101 pre-existing links survived the reshuffle, plus 1 previously-orphaned page (`/admin/impact-report` — had a route and a `CommandPalette` entry but no sidebar link) gained a proper nav entry

---

## Local Dev Environment Notes

- The tracked `src/Lotv.Api/lotv-dev.db` was stale relative to the current EF model (missing `ExchangeRates` table) — `EnsureCreated()` in dev mode is a no-op against an existing file, so it never self-heals. Backed up, deleted, let it regenerate, then restored the original tracked version before committing (this session's schema/seed churn wasn't the point of the work and shouldn't be committed as a side effect).
- Installed `cloudflared` via winget for the quick-tunnel testing described above.

---

## Build Stats

- Every change verified with a full `dotnet build` on both `Lotv.Api` and `Lotv.Web` — 0 errors / 0 warnings throughout
- Verified visually via Playwright browser automation (not just build success) — logged in, screenshotted before/after states, confirmed dashboard renders real data instead of an infinite spinner

---

## Current Project State

- **Total admin pages**: 293 (unchanged — no pages added or removed, only reorganized/fixed)
- **Phase 4 (Frontend)**: COMPLETE
- **Phase 5 (Testing)**: COMPLETE (423 tests — not re-run this session; no test-covered logic changed except JWT claims, which should get a test)
- **Phase 6 (Deployment)**: IN PROGRESS — still blocked on cloud account decisions; Duda/GiveButter wiring now proven locally, just needs a durable public URL
- **Branch**: `kremer-dev`

---

## Suggested Follow-ups (Not Done This Session)

- Add a unit/integration test asserting the JWT contains `GivenName`/`Surname` claims and `UserName` resolves correctly, so this class of bug can't silently regress
- Consider a lint/CI check that greps for `&#\d+;` inside `.razor` attribute values or `@()` expressions to catch this entity bug before merge
- Consider a build-time or CI check for duplicate `@page` routes (a quick script could catch this instead of relying on someone loading the page)
