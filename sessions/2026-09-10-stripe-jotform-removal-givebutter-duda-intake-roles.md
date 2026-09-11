# Session Notes — 2026-09-10: Stripe/JotForm Removal, GiveButter Donations, Duda Intake Form, Admin Nav Consolidation, Board/Director Roles, Live Smoke Test

## Summary

A long session that started as routine git-sync and CI maintenance, then pivoted hard after discovering JotForm had never received a single real submission (the account's HIPAA-BAA restriction blocks even webhook registration). The client's response wasn't "fix JotForm" — it was "we don't need Stripe or JotForm at all; everything for donation is through GiveButter." That reframed the rest of the session: full removal of Stripe from the app, a new standalone Duda-embeddable intake form built from scratch, GiveButter's real JS widget wired in everywhere Stripe used to be, a client-requested admin-sidebar consolidation, new Director/Board roles with volunteer tenure levels, and — after the client asked what else needed fixing — a real live smoke test of the running app that caught two genuine bugs no amount of code review would have surfaced on its own.

Full risk-register detail in `docs/LOTV-PM-Plan.md` (R-26 through R-31). Full checklist detail in `MASTER_TODO.md`'s 2026-09-10 session section.

---

## Git Sync, CI Fix, and the JotForm Pipeline Investigation

Started by reconciling `kremer-dev` divergence between `origin` (personal fork `KremerWTE/LOTV`) and `wtesolutions/main` — merged and pushed clean. Then diagnosed a pasted Dependabot ENOSPC CI error; added a disk-cleanup step to `ci.yml`, but on closer investigation the real failure was in GitHub's own internal "Dependabot Updates" job, not anything fixable from the repo — said so honestly rather than claiming the fix worked.

Asked to confirm JotForm was actually flowing into the Kanban board and database. It wasn't — zero submissions, ever. Traced this through a cloudflared quick-tunnel test (webhook registration itself returned 401) to the same HIPAA-BAA root cause documented in the 2026-09-01 session: this JotForm account has a signed Business Associate Agreement on file, and JotForm deliberately discards *all* programmatic writes on BAA accounts, including webhook subscriptions, not just schema edits as previously understood. Also attempted to point the pipeline at the real production SQL Server (`10.100.1.87`) for a live test — VPN was up, Bash's `/dev/tcp` couldn't reach it (PowerShell's `Test-NetConnection` could; a sandbox-isolation artifact, not a real network problem) — and when the client was asked for credentials to proceed, they said "stop." No credentials were obtained, no production DB connection was made.

## The Pivot: No JotForm, No Stripe

Client: *"no i am not looking for the jotform i want to build a intake form that is not jotform."* Then, after some back-and-forth on GiveButter integration research: *"we dont need to have stripe or jotform."* Confirmed via clarifying question that this meant full removal across the whole app, not just the payment endpoints — *"everything for donoration is through givebutter."*

### Duda Intake Form

Built `docs/duda-embed/prayer-care-intake.html` — a fully self-contained HTML/CSS/JS file for Duda's Embed Code widget. Mirrors JotForm's real conditional logic (pulled live from the form's own `conditions` API data before it was decommissioned): "For Me" hides the donation ask, "For Someone Else" shows it. Posts directly to a new `POST /api/v1/public/apply` handler in `Lotv.Api` — no JotForm, no webhook, no third-party HIPAA-BAA account anywhere in the path.

Iterated through several rounds of client feedback:
- Husband's/Wife's Name and Email made mandatory, phone optional (*"make the husband and wife's name and email be mandatory and phone number optional"*) — matched against the ministry's actual live Gravity-Forms intake at lotvministry.org.
- "Children for Bracelet" rebuilt as a repeatable-row table (Order/Initial/Bead Type/Living-or-Deceased) to exactly match JotForm's Configurable List widget structure.
- GiveButter widget widened to fill its container (*"take teh give butter and widen it to the full yellow box width"*) — this took two attempts. First attempt broke the widget entirely (collapsed to just a "Powered by Givebutter" footer) because forcing `max-width:none` clashed with how GiveButter measures itself on mount. Root-caused via `el.shadowRoot` inspection: `<givebutter-widget>` is a Web Component with an **open shadow root** containing a nested `<givebutter-giving-form>` that hardcodes `max-width:440px` on *itself* as both an HTML attribute and internal style — fixed by directly overriding that inner element's own `max-width` at runtime. Client confirmed ("yes that is good"), but the *saved* version turned out to only fix the inner element, not the outer host's own width (*"it was right but now it is not"*) — fixed by also forcing `host.style.display = "block"; host.style.width = "100%"` on the outer element, verified via fresh reload + JS measurement (host width 558 of a 600px container). Documented as fragile-by-design in code comments — depends on GiveButter's undocumented internal DOM structure and could silently break on a widget-library update.

### Full Stripe Removal

Deleted: `/api/v1/payments/intent`, `/api/v1/payments/webhook`, Stripe customer creation in `/give`, the billing-portal endpoint, `SyncStripeRecurringAsync` and its 3 call sites (pause/cancel/resume now update local DB status directly instead), the Payment Reconciliation report page, the Stripe-only webhook-replay endpoint, the `donorsWithStripeCustomer` diagnostic stat, `IPaymentService` (an unused/unimplemented interface), the `Stripe.net` package reference, and both `JotFormWebhookTests.cs`/`PaymentWebhookTests.cs` test files. `Give.razor` was completely rewritten to embed the real GiveButter widget instead of a Stripe Elements card form, using the same widen-widget JS pattern developed for the intake form.

One residual gap left in place, not urgent: `Donor.StripeCustomerId`/`Donation.StripePaymentIntentId` fields remain in the schema (always null now).

## Admin Nav Consolidation

Client: *"there are so many tabs we need to streamline it."* Surveyed the sidebar: 120 total links, 57 always-visible. Chose to "finish the hub pattern" (extending an approach the app already used elsewhere) over inventing something new. When asked whether the Cases cluster — which contains high-frequency daily-use tools — should be exempted, client said fold it in too (*"Hub Cases too"*).

Built three new tabbed hub pages that embed the existing routable components as child components (each retains its own `@page` route, so direct links/bookmarks keep working):
- `CasesHub.razor` — 11 tabs (All Cases, Kanban, Historical, Mother's Day, Bereavement Follow-Up, Unassigned Queue, My Queue, Overdue, Bulk Update, Analytics, At Risk)
- `ReportsHub.razor` — role-filtered tabs (donor-visible items gated behind `_canSeeDonations`, admin-only items behind `_isAdmin`)
- `SystemAdminHub.razor` — 10 tabs

A "Show merged/legacy links" checkbox in `AdminLayout.razor` toggles the individual links back on, preserving full backward-compatible reachability.

## Board & Director Roles, Volunteer Levels

Client: *"have it also split the view based on volunteer level and director and board."* Clarified via `AskUserQuestion`:
- Director/Board scope → *"Director = near-admin, Board = reports only."*
- Volunteer level scope → *"Tiers within the existing Volunteer role"* (i.e., inside the separate volunteer self-service portal, not a new admin role).

`Director` and `Board` added to `UserRole`. Director extended into the `ChapterAdmin`/`Staff`/`Volunteer` authorization policies (near-admin access). Board is deliberately narrower: a dedicated `/board/portal` page (public layout, gated on `Auth.UserRole != "Board"`) backed by a **genuinely separate** `/api/v1/board/*` route group with 4 aggregate-only endpoints (`/summary`, `/money-flow`, `/resources`, `/timeline`). This was a deliberate PII-avoidance decision: the existing `/api/v1/dashboard/*` group, despite looking like a report, actually returns raw PII-bearing donor lists to the client for browser-side aggregation — reusing it for Board would have created a PII leak even though the *UI* would have looked aggregate-only. `Volunteer.Level` (New/Standard/Senior/Lead) was added and surfaced in `VolunteerPortal.razor` — an onboarding checklist for New volunteers, a recognition/mentoring panel for Senior/Lead.

Verified live via Playwright, end to end: signed in as a test account with the `tech` login, changed its role to Board via `/admin/users`, confirmed the Board portal rendered real data with zero console errors and zero PII exposure, then restored the `tech` account back to `HQAdmin` afterward. Director's `AdminLayout` integration is implemented and build-verified but wasn't independently Playwright-walked this session — only Board was.

One EF migration hiccup along the way: the dev SQLite DB had originally been created via `EnsureCreatedAsync()` rather than migrations, so `dotnet ef database update` failed with `'table "AspNetRoles" already exists'`. Reconciled manually — `ALTER TABLE Volunteers ADD COLUMN Level INTEGER NOT NULL DEFAULT 0` via Python's `sqlite3` module, then inserted a row into `__EFMigrationsHistory` to mark the migration applied — preserving existing dev/test data rather than deleting the DB.

## "What Else Needs to Be Fixed?" — Real Gaps Found by Reading the Code, Not Guessing

When asked what else needed attention before the intake form could go live, checked what actually happens once `/apply` submits in a real deployment, rather than assuming the happy path:

- **`NotificationService` was a stub.** Its own doc comment said *"Replace with SendGrid implementation for production."* Every `SendEmailAsync` call just logged and returned success — the intake form's confirmation and staff-notification emails would have silently no-op'd in any real deployment. Replaced with a real `System.Net.Mail.SmtpClient` implementation, config-driven via a new `Smtp` appsettings section (falls back to logging when unconfigured, so local dev/tests are unaffected).
- **`Jwt:Key`/`ConnectionStrings:DefaultConnection` empty in Production config** — by design (secrets, not committed), but nothing in the deploy pipeline ever set them as real Application Settings on the App Service either.
- **`Database:Provider` was unset** in `appsettings.Production.json`/`Staging.json` — would have silently fallen through to the untested Postgres branch. Fixed (not a secret, safe to commit).
- **Neither deploy workflow configured the App Service's own runtime settings after deploying code** — zip-deploy shipped the app but never told it how to connect to anything. Added `az webapp config appsettings set` steps to both `deploy-production.yml` and `deploy-staging.yml`.

When the client then asked specifically about the team-email flow (*"will send an email to those who submit a request to get a response"* / later confirmed *"that is the right flow"*), found the team notification only went to one hardcoded address — widened it to `Notifications:IntakeTeamEmails` (comma/semicolon list), wired to the previously-unused `NOTIFICATION_EMAIL_RECIPIENTS` GitHub secret.

## Live Smoke Test — Two Real Bugs Found

Asked to run the documented smoke-test checklist (`docs/smoke-test-checklist.md`) rather than just reasoning about readiness. Started both API and Web locally and walked every section with real HTTP calls and a real browser:

- **Process-hygiene catch first**: the API instance still running from earlier in the session (left over from Playwright testing) was serving a 3-day-old build from Sept 7 — predating this entire session's work. Killed it, rebuilt, restarted from the actual current code before trusting any test result. Worth remembering: a stale background `dotnet run` process can silently mask whether your latest changes are even running.
- **Auth, request lifecycle, volunteers/donors, dashboard stats** all passed cleanly against the fresh build. Auto-assignment correctly assigned a clean submission to a volunteer; duplicate detection correctly held a repeat-email submission out of auto-assignment for staff review.
- **Real bug found: every case-activity-log entry and note showed the acting staff member's raw GUID instead of their name.** `ActorName`/`AuthorName` were literally set to `ctx.UserId` at 11 call sites in `Program.cs`, because `IChapterContextService` never exposed a display name at all — only `UserId`. This is a core dashboard feature (the case audit trail) that's been silently broken. Fixed by adding `IChapterContextService.UserName` (reads the JWT's given/surname claims, no extra DB round-trip needed) and swapping all 11 sites.
- **Real bug found: `POST /requests/{id}/notes` threw an unhandled 500** (a raw SQLite `NOT NULL` constraint exception, stack trace and all) when `content` was missing, instead of a clean validation error. Added the missing check.
- Both fixes verified live against a fresh rebuild afterward. All test data created during the smoke test (families, requests, volunteers, donors, users) was cleaned out of the dev DB each time. 415/415 unit tests passing throughout.

Also confirmed along the way, as good news: a GiveButter webhook (`/api/v1/givebutter/webhooks`) already exists and auto-syncs transactions into Donor/Donation records — it just needs a public URL to register with GiveButter, same blocker as everything else.

## What's Left

Everything below is unchanged in kind from before this session — the app itself is in materially better shape, but the deployment blocker is the same one that's been open since Phase 6 started:

- **No Azure App Service exists yet.** The deploy pipeline is code-complete (including today's Application Settings wiring) but has never been run against a real resource. Fastest path to something publicly testable: `az webapp up` (single command, free `F1` tier) rather than the full portal-provisioning flow.
- Missing secrets: `DB_CONNECTION_STRING`, `JWT_SIGNING_KEY` (new), `AZURE_CLIENT_ID`/`TENANT_ID`/`SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`(`_PROD`), `AZURE_WEBAPP_API_NAME`(`_PROD`), `AZURE_WEBAPP_WEB_NAME`(`_PROD`). The existing `SMTP_*`/`NOTIFICATION_EMAIL_*` secrets have never been verified as live/working credentials.
- `API_BASE_URL` in the Duda HTML file and `AllowedOrigins`/`vars.DUDA_SITE_ORIGIN` need real values once the Duda domain is known.
- The `sa` SQL Server credential rotation script (`rotate-app-credential.sql`) is still unrun against the real `10.100.1.87` database.
- `Donor.StripeCustomerId`/`Donation.StripePaymentIntentId` schema remnants — low-priority cleanup.
- Route-walk coverage: today's new hub pages + Board portal are spot-checked clean, but the ~285-route gap from the 2026-08-31 Extended Route Sweep is otherwise unchanged.

All work this session committed and pushed to `kremer-dev` on both `origin` (`KremerWTE/LOTV`) and `wtesolutions/LOTV`, across four commits.
