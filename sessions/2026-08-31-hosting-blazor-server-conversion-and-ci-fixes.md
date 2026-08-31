# Session Notes — 2026-08-31 (cont'd): Hosting Decision, Blazor Server Conversion, CI/CD Fixes

## Summary

Continuation of the 2026-08-31 session (see `2026-08-31-spreadsheet-audit-and-followup-tracker-autocreation.md` for the earlier spreadsheet-audit half). This half covers: reconciling EF migrations for SQL Server, deciding and wiring Azure App Service hosting, converting `Lotv.Web` from Blazor WebAssembly to Blazor Server, and a long chain of CI/CD fixes surfaced by actually running the pipeline against the real `wtesolutions/LOTV` repo instead of just reasoning about it.

---

## EF Migrations Reconciled for SQL Server

The existing `Migrations/` folder was scaffolded against SQLite, baking `type: "TEXT"` into every column — wrong for SQL Server. Created `src/Lotv.Migrations.SqlServer`, a separate project holding a freshly-scaffolded `InitialCreate` migration against `UseSqlServer` (confirmed correct `nvarchar(max)`/`int` types). `Program.cs`'s `UseSqlServer(...)` now points at it via `MigrationsAssembly`.

Getting the assembly actually *loadable* at runtime took three real CI iterations (a circular `ProjectReference` isn't possible — the migrations project needs `Lotv.Api` for the `DbContext`, so `Lotv.Api` can't reference it back):
1. First attempt: a post-build MSBuild copy target in `Lotv.Api.csproj`. Failed in CI — build ordering isn't guaranteed, and `dotnet test`'s solution-wide build doesn't touch a project nothing references.
2. Second: added an explicit `dotnet build` step for the migrations project before the migrations step in both deploy workflows. Failed — that project's packages had never been restored either (same root cause), and `--no-restore` had nothing to build against.
3. Third: dropped `--no-restore`. Fixed.

Also found and fixed a real, unrelated bug in the process: `deploy-staging.yml`/`deploy-production.yml` set `ConnectionStrings__Default`, but `Program.cs` reads `GetConnectionString("DefaultConnection")` — the migration step was silently running with no connection string regardless of provider. Neither workflow set `Database__Provider=SqlServer` either.

Prepared two scripts for a human with prod DB access to run once: `baseline-existing-database.sql` (marks the new `InitialCreate` as already-applied against the live `10.100.1.87` DB, which already has this schema from the earlier `EnsureCreated()` workaround) and `rotate-app-credential.sql` (creates a scoped `lotv_app` login ahead of retiring the shared `sa` credential).

## Hosting Decided: Azure App Service, No Docker

User picked Azure App Service. Then, mid-session, explicitly said "do not use docker" — removed all Docker build/push steps from both deploy workflows, replaced with `dotnet publish` + `azure/webapps-deploy`'s zip-deploy `package` input.

## `Lotv.Web`: Blazor WebAssembly → Blazor Server

Direct instruction: "use blazor for everything and make it use the same tech as other in the github repo." Converted `Lotv.Web` from standalone WASM to Blazor Server (`Microsoft.NET.Sdk.Web`, `AddInteractiveServerComponents`) — now a normal Kestrel app exactly like `Lotv.Api`. No more nginx, no IIS `web.config` SPA-routing workaround, no separate Windows App Service plan requirement.

Before starting, grepped the whole codebase for synchronous JS interop (`IJSInProcessRuntime`) — found none. That was the single biggest WASM→Server risk, and its absence meant the conversion was structurally low-risk.

**Bugs found and fixed, in order, each one found by actually running the app rather than by code review alone:**

1. **`LocalizationService` was `Singleton`** — harmless under WASM (one app instance = one user), invalid once ASP.NET Core's DI validator checked it against a real multi-user server: it consumes the Scoped `IJSRuntime`. Changed to `Scoped`.
2. **Auth session-restore broke on any direct/reload navigation to a protected route.** Root cause: Blazor Server's static prerender pass runs before the interactive circuit (and its JS interop) exists, so it always saw "anonymous" and redirected to `/login` — confirmed via a bare `curl` with zero session getting a real `302`. Fixed by disabling prerendering (`InteractiveServerRenderMode(prerender: false)`) and gating `Routes.razor`'s `<Router>` behind the session-restore's own completion.
3. **6 PDF/CSV/QR download links across 5 pages** (`DonorReceipts.razor` ×3, `DonorPortal.razor`, `DonationConfirm.razor`, `Admin/EventDetail.razor`, `Admin/RetreatDashboard.razor`) used relative `/api/...` hrefs that resolve against the Web app's own origin when clicked, not the API's. Confirmed via `curl`: same path, 404 against Web's origin, 200 against the API's. **Confirmed pre-existing, not caused by the conversion** — checked the deleted `nginx.conf` and it never had an `/api/` proxy rule either, so this was equally broken under the old Docker/WASM setup (nginx's SPA fallback would've quietly served `index.html` instead of a PDF). Fixed with a new `ApiService.BaseUrl` property.

**Verification, not just code review:**
- Full unit/integration suite: 433/433 passing throughout.
- Live browser walkthrough (Chrome extension): login, Dashboard, Kanban (including a real drag-and-drop move, confirmed via dispatched `DragEvent`s after the automation tool's synthetic mouse-drag proved unreliable), Case Detail, Case Analytics (heat map + a Leaflet map — the highest JS-interop-risk page in the app), Follow-Up Trackers, Historical Cases, Families by State, CSV export (confirmed the JS interop call fires with real server-built data), Bulk Case Update, and ~25 more admin pages sampled from areas not previously touched (Donors, Volunteers, Payment Reconciliation including running its report, Grants, Retreats, Campaigns, Settings, Users, Donor Pledges, a second Leaflet map instance, etc.). Zero new console errors anywhere.
- Corrected an earlier estimate: this app has **312 distinct routes**, not ~100. ~35 have been individually walked; the rest haven't.

## CI/CD: Finding and Fixing What the Conversion Broke

The Blazor Server conversion broke the E2E test suite, but this wasn't caught until `CI — Build & Test` was actually run — `Lotv.Tests` (433 tests) never exercises `Lotv.Web` at all.

Diagnosed and fixed through **repeated real CI runs against `wtesolutions/LOTV`**, not local reasoning alone:

1. All 7 `MobileResponsivenessTests` timed out waiting for `#app:not(:empty)` — a selector `App.razor`'s rewrite had removed entirely. Fixed by restoring a structural `<div id="app">` wrapper around `<Routes>` (empty until the interactive circuit renders into it, so the selector is meaningful again) and syncing `E2ETestBase`'s own `WaitForBlazorAsync` (which had been waiting for a WASM-only loading-spinner class to detach — with no spinner markup and prerendering off, that resolved instantly instead of waiting for anything).
2. That fix unmasked 3 further failures that had been silently unreachable since before this session: `AccessibilityTests.HomePage_HasExactlyOneH1`/`HelpPage_HasExactlyOneH1`, `ApplyFlowTests.ApplyPage_FormFields_ArePresent`, `AuthFlowTests.LoginPage_HasExpectedElements` — real content-race and (for 3 mobile-viewport tests specifically) a real, pre-existing CSS bug: a hardcoded, non-responsive `grid-template-columns:1fr 1fr` inline style on 3 forms' name-field rows overflowed a 390px mobile viewport. Confirmed via the last known-green pre-conversion CI run (2026-07-20, commit `1b849f8`) that this predates the session entirely. Fixed with `repeat(auto-fit,minmax(140px,1fr))` across `Give.razor`, `Apply.razor`, `VolunteerSignup.razor`.
3. Verified via **5 real triggered CI runs** against `wtesolutions/LOTV` (not just local `dotnet test`), including one opened purely to get a green confirmation on `kremer-dev` before the user's own promotion process ran it — closed without merging once confirmed.

**Separately, root-caused a stale "5%" code-coverage health flag**: ~90,000 lines of auto-generated EF Core migration code (never meant to be unit-tested) were counted in `Lotv.Api`'s coverage denominator, all permanently at 0%. Excluded via `tests/Lotv.Tests/coverlet.runsettings`; real coverage is 46.7%, not 5.3%. Doesn't affect CI pass/fail (cosmetic-only threshold).

## A Process Note: Two Remotes

Discovered mid-session that `origin` in this working copy is a personal fork (`KremerWTE/LOTV`); the actual org repo with GitHub environments/deployment history is `wtesolutions/LOTV`. All pushes this session went to both. User's standing instruction going forward: **only push to `kremer-dev`** (both remotes) — they promote through `stage`/`main` themselves via their own PR process.

## Items Worked, 5 Through 8 (from the prior standing open-items list)

- **#5** (JotForm "Children for Bracelet" widget rendering): substantially de-risked without needing a live submission — the widget's sub-field names don't collide with any `knownLabels` entry, so the existing label-boundary fix can't misfire regardless of exact rendering. **Genuinely closing this** (confirming display format, not just correctness) requires submitting a real test entry to the live production JotForm form — flagged to the user rather than done unilaterally, since that touches an external service real grieving families use.
- **#6** (broader QA): see the Blazor Server section above — this is where the auth-restore bug and the 6 broken links were found.
- **#7** (Phase 6 infra checklist): Stripe is fully implemented in code already (PaymentIntents, webhooks, subscriptions, customer portal) — the checklist items are purely administrative (account + keys). Blob storage, Redis, CDN, and Key Vault are **not currently code gaps** — nothing in the app needs any of them yet (receipts stream directly to the client, nothing is cached, no secrets-manager SDK is referenced anywhere).
- **#8** (Npgsql/Postgres provider): confirmed dead/unreachable in the current deployment path (neither CI nor either deploy workflow ever sets `Database:Provider` to anything but `SqlServer`) and would hit the same migrations-vs-provider bug fixed for SQL Server if ever triggered. Documented rather than removed — a bigger call than pure cleanup.

## Current Project State

- **Branch**: `kremer-dev` (pushed to both `origin` and `wtesolutions`)
- Build: clean. Tests: 433/433 unit/integration, 60/60 E2E (confirmed via real CI, not just local runs).
- Still open, blocked on things outside the assistant's access: Azure App Service provisioning + secrets, JotForm builder access (HIPAA/sender-name/page-split/donation-nudge checklist), running the two prepared SQL scripts against the live prod DB, and a live JotForm test submission if the user wants #5 fully closed rather than just de-risked.
