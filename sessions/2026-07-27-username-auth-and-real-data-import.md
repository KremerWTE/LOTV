# Session Notes — 2026-07-27: Username-Based Auth, Kanban Cleanup, Real Spreadsheet Import to Production SQL Server

## Summary

A long multi-part session spanning several distinct asks: (1) migrate staff sign-in from email to username since most staff don't have real emails on file, plus a forgot/reset-password flow; (2) clean up and cross-link the Kanban/Queue/My-Work-Queue pages; (3) import the ministry's real historical "Prayer Care Package Request Database.xlsx" spreadsheet — case history, Mother's Day mailing list, and Stephen's Ministry bereavement follow-up tracker — into the actual production database; (4) discovered mid-session that "the real deployed database" is a SQL Server instance, not the app's existing SQLite/Postgres setup, and added SQL Server support to connect to it. All real family PII was kept out of git at every step.

---

## Username-Based Sign-In + Password Recovery

- `/auth/login` now takes `Username` instead of `Email` (`Program.cs`, `AuthService.cs`) — `FindByNameAsync` instead of `FindByEmailAsync`
- Seeded 10 new staff accounts with bare usernames (`firstname.lastname`, no email): Whitney Whitmore, Cynthia DeStefano, Chris Kremer, Admin, Tech (all HQAdmin), plus 5 ChapterStaff accounts (Jamie-Lee Lavelle, Maegan Dobner, Stephanie Caccamo, Sammi Weaver, Stephanie Mercado Carrillo)
- Renamed the two original demo accounts `admin@lotv-demo.org` / `chicago@lotv-demo.org` → `mary.roberts` / `claire.hoffman` for consistency (updated E2E test config + README to match)
- New `POST /auth/forgot-password` + `POST /auth/reset-password` endpoints, new public pages `ForgotPassword.razor` / `ResetPassword.razor`
- New `PUT /users/{id}/email` — admin-settable recovery email (since sign-in no longer uses email) with UI in `UserManagement.razor` (Username + Recovery Email columns, editable in the edit drawer)
- `NotificationService` now logs the full email body (dev stub) so reset links are visible in console during local testing
- `SeedData.SeedAsync` split into `SeedMockDataAsync` (gated on `!Chapters.Any()`) and `SeedLoginAccountsAsync` (always runs) — needed once a database could hold real imported data instead of the fictional demo dataset, since login accounts still need to exist either way
- Fixed a real bug found via manual curl testing: malformed/old-shaped login requests threw an unhandled `ArgumentNullException` → 500 with a stack-trace leak; added `IsNullOrWhiteSpace` guards on all three auth endpoints
- Fixed a real gap: `appsettings.Development.json`'s `AllowedOrigins` only listed port 5205, not the 5001/5000 the E2E README documents — added both

## Kanban Board Cleanup

- Cross-linked Kanban Board, Unassigned Queue, and My Work Queue pages (each now links to the other two)
- Unassigned cases get a visual flag: amber left border + amber "Unassigned" label on the card, plus a "(N unassigned)" count in the column header
- Removed dead code (`GetBadge`, unused) and de-duplicated the `_byStatus` rebuild logic into a single `RegroupByStatus()` helper
- **Capped the Fulfilled column to the last 3 months** (by `UpdatedAt`) once real data made it grow unbounded (341 cards) — sorted most-recent-first, with an explicit "(N older hidden)" note in the header so it's clear older cases aren't gone, just filtered from this view (still visible via List View / Historical Cases)

## Real Data Import — "Prayer Care Package Request Database.xlsx"

The ministry's real spreadsheet (13 sheets) was reviewed; 4 sheets were actually imported (the rest were redundant mailing-label/thank-you exports of the same underlying data):

| Source sheet(s) | Destination | Rows |
|---|---|---|
| `2026`, `2025 (end)`, `2024 (end)` | `Family` + `PackageRequest` | 1,046 (342 active 2026 pipeline + 704 historical) |
| `MDFD Mailing 2026` | new `MailingListEntry` table | 425 |
| `SM (2026)` | new `FollowUpTracker` + `FollowUpMilestone` tables | 47 trackers / 188 milestones |

New model fields: `Family.DateOfLoss`, `Family.IsHistorical` (excludes prior-year closed-out cases from the active Kanban/Queue pipeline by default — `GET /api/v1/requests?historical=true` to see them).

New pages: `/admin/historical` (read-only browse + search of prior-year cases, reuses the existing `CaseDetail.razor` for detail views) and `/admin/mothers-day` (annual mailing-list manager: flag-for-review, mark-sent).

Built a reusable one-time import tool at `tools/LegacyImport` (new console project) that reads a JSON export of the spreadsheet (produced by a Python script kept in the session scratchpad, **not committed** — it processes real PII) and loads it via EF Core. Idempotent — wipes and re-inserts its own prior output on re-run, so it's safe to fix and re-run.

**Security note**: the spreadsheet itself, the extraction JSON, and every local SQLite DB used for testing were kept out of git (`*.xlsx` added to `.gitignore`; local DBs used `/tmp` paths). No real name, address, or email ever touched a committed file.

## Real Production Database — SQL Server at 10.100.1.87

Mid-session, established that "the real deployed database" is a SQL Server 2019 instance (not the app's existing SQLite-for-dev/Postgres-for-prod setup). Created a `LOTV` database there and imported all of the above into it.

- Added `Microsoft.EntityFrameworkCore.SqlServer` package + `Database:Provider = "SqlServer"` config flag (`Program.cs`) — independent of `ASPNETCORE_ENVIRONMENT`, so it can be targeted without touching the existing Postgres-based staging/prod config
- **Important gotcha discovered**: the existing EF migration history was scaffolded against SQLite, and SQLite's migration generator bakes literal column-type strings (`TEXT`, `INTEGER`) directly into each migration's `Up()` method. Regenerating a migration script against the SqlServer provider just replays those same hard-coded SQLite types — it does **not** re-derive correct SQL Server types. `dotnet ef database update` / `Database.Migrate()` is therefore unusable against this database for now.
  - Workaround used throughout: `Database.EnsureCreated()` (derives DDL fresh from the live C# model, correct per active provider) for the initial schema, and a **hand-written T-SQL script** for the one incremental table addition (`FollowUpTracker`/`FollowUpMilestone`) added after the database already had data (`EnsureCreated` only initializes a *completely empty* database — it won't add tables incrementally).
  - Real fix for later: regenerate the migration history from scratch with `Database:Provider=SqlServer` as the *design-time* provider, or maintain provider-specific migration sets.
- No credentials were ever written to a committed file — connection strings and the `sa` password were passed as environment variables only, for each `dotnet run` / `sqlcmd` invocation

## Verified

- 423/423 unit/integration tests pass throughout every change in this session
- 60/60 E2E tests pass after the username migration (one unrelated WASM cold-start flake reproduced clean on rerun)
- Kanban Board, Historical Cases, Mother's Day Mailing, and case detail pages all manually verified via Playwright against the live SQL Server database with zero console errors
- Row counts verified directly via `sqlcmd` against `10.100.1.87`: 1,046 Requests/Families, 425 MailingListEntries, 47 FollowUpTrackers, 188 FollowUpMilestones

---

## Current Project State

- **Phase 4 (Frontend)**: COMPLETE — 2 new pages (`Historical.razor`, `MothersDay.razor`), Kanban/Queue/MyQueue cross-linked
- **Phase 6 (Deployment)**: database hosting decision now made — SQL Server, not the originally-open Azure SQL/AWS RDS/Supabase/Railway choice — see MASTER_TODO Phase 6 update
- **Branch**: `kremer-dev`
- New backlog item: SQL Server migration history needs to be regenerated/reconciled — see Suggested Follow-ups

---

## Suggested Follow-ups (Not Done This Session)

- Regenerate EF migration history so `dotnet ef database update` works cleanly against SQL Server (either re-scaffold all migrations with `Database:Provider=SqlServer` as the design-time default, or accept SQLite-for-dev/manual-DDL-for-SqlServer as the permanent pattern and document it clearly)
- Rotate the `sa` password used during this session and consider a dedicated least-privilege SQL login for the app instead of `sa`
- Build a UI page for the new bereavement follow-up tracker data (`FollowUpTracker`/`FollowUpMilestone`) — currently imported but has no admin page, unlike Historical Cases and Mother's Day Mailing
- Decide whether the ~700 historical (2024/2025) cases and the 342 "active" 2026 cases should be re-triaged — most 2026 cases already show "Fulfilled" per the spreadsheet's own tracking notes, so the live Kanban board realistically has only 1 open case; confirm this matches actual ministry operations before relying on it operationally
- Consider building the reusable "Import from Excel" admin feature (the alternative option not chosen this session) if more spreadsheets like this need to be ingested in the future
