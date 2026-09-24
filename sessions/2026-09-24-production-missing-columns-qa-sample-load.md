# Session Summary — Production QA sample load fails: missing database columns

**Date:** 2026-09-24
**Branch:** kremer-dev
**Phase:** Phase 6 — Deployment & Launch (QA readiness)
**Session Type:** Bugfix

---

## Session Objectives

Get the QA sample data into production so the team can QA the site. The Load button on System Admin → QA Sample Data did nothing.

## Key Accomplishments

### Root cause found ✅
- Production ran an older build (page text said "12 families / 3 volunteers"), which swallowed the failure. After the diagnostics build was deployed the page reported: `The load failed (SqlException): Invalid column name 'Level'.`
- `Volunteers.Level` and `Requests.ProcessStage` were added by migration `20260910133545_AddVolunteerLevel`. SQL Server startup uses `EnsureCreated`, which never alters an existing table, so a database created before that date never got the columns. Any query touching volunteers (not just the sample load) fails the same way; startup steps such as `VolunteerWorkload.RecomputeAllAsync` were failing silently (caught and logged).

### Fix ✅
- New `src/Lotv.Api/Data/MissingColumnBootstrap.cs`: on SQL Server, compares the EF model to `INFORMATION_SCHEMA.COLUMNS` and adds any missing column (nullable, or NOT NULL with a default). Additive only; skips missing tables, primary keys and computed columns; idempotent; logs a warning per column added.
- Registered in `Program.cs` right after `EnsureCreated`, wrapped in try/catch so it can never take the API down.
- Chosen over one-off bootstraps because other columns may be missing too and per-column deploys would each cost a round trip.

## Verification

- Temporary LocalDB test (deleted afterwards): created the current schema, dropped `Volunteers.Level` and `Requests.ProcessStage`, ran the bootstrap (added both, second run added nothing), then loaded and removed the 39-family sample data successfully.
- End-to-end on the real API (Development host, SQL Server LocalDB, `Volunteers.Level` and `Requests.ProcessStage` dropped first): startup logged `Added missing column Requests.ProcessStage` / `Volunteers.Level`; `POST /api/v1/qa-sample-data` returned 200 "Loaded 39 sample families, 39 requests and 5 volunteers"; `GET /volunteers` 200; `DELETE` removed everything. With no chapter in the database the endpoint returns a clear 409 "There is no chapter to attach sample data to." (production must have one).
- Merged `wtesolutions/main` (7 PR merge commits, #61–#67, no file changes) into kremer-dev.
- Bug found and fixed while testing: the store identifier must use the model's own schema (null), not `"dbo"`, or every column lookup returns null.
- `dotnet build Lotv.slnx`: 0 warnings, 0 errors. 615/615 unit + integration tests pass.
- Not covered by a permanent test: the repo has no SQL Server test infrastructure (CI has no LocalDB).

## Confirm Assignment failing (same root cause) ✅

- Reported: "Confirm Assignment" fails. `PUT /requests/{id}/assign` loads `PackageRequest` and `Volunteer`, which select `ProcessStage` and `Level`, so on a database missing them it returned a bare 500 and the UI said only "Assignment failed. Please try again." The column bootstrap above fixes the cause.
- Made the reason visible: the assign endpoint now catches a save failure and returns `{ error }` with the real message (staff-only screen); `ApiService.LastAssignError` carries it; Queue, Kanban, CaseAssign and CaseDetail show it.
- Verified on the real API + LocalDB: healthy schema 200; columns stripped while running -> 500 "The assignment could not be saved (SqlException): Invalid column name 'ProcessStage'."; after restart the bootstrap added both columns and assign returned 200 (stage Assigned, status InProgress).
- Found, not changed: `GET /dashboard/stats` open-case count (`Program.cs` ~line 1762) has an operator-precedence bug (`A && New || InProgress`), so the chapter filter applies only to New; it also counts only New + InProgress. The sidebar "Cases" badge reads it once at layout load.

## Case page fixes (same day) ✅

- **Sidebar Cases badge** now counts what the Cases page calls Open (New, In Progress, Awaiting Shipment), excludes requests held for duplicate review, and applies the chapter filter to all of them (was `chapter && New || InProgress`). It refreshes on every navigation instead of once at load. Test: `SidebarOpenCasesCount_MatchesTheCasesPage_...` (616 tests pass).
- **Case details Save Changes** ignored the result of assign / priority / due date / tracking saves and could report success (or fail silently) when the volunteer change failed. Each step is now checked; the first failure stops the save, names the step (with the server's reason for assign) and keeps the edits on screen.
- **Request-form answers on the case page:** the Family panel now shows the prayer-wall (privacy) preference and "Other answers from the request form" (`Family.ContactNotes`: grief-support answer, custom questions staff add in the form editor, newsletter / prayer-night opt-ins), which were saved but never displayed.
- Known intake gaps, not changed: `PackageType` from the form is collapsed to a category and `"Comfort"` (capitalised default) doesn't match the lowercase `"comfort"` mapping so it becomes Other; when both parents give an email or phone only the first is kept.

## Open Items

- [ ] PR kremer-dev → stage → main; then check the API log for "Added missing column" lines and click Load on production.
- [ ] If another `Invalid column name` appears, the bootstrap did not cover it — send the message.
- [ ] Startup errors previously swallowed on production (workload recompute, workflow repair) should now succeed; worth a look at the log after deploy.

---

**Session End** | **Branch:** kremer-dev ✅ Safe
