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
- Bug found and fixed while testing: the store identifier must use the model's own schema (null), not `"dbo"`, or every column lookup returns null.
- `dotnet build Lotv.slnx`: 0 warnings, 0 errors. 615/615 unit + integration tests pass.
- Not covered by a permanent test: the repo has no SQL Server test infrastructure (CI has no LocalDB).

## Open Items

- [ ] PR kremer-dev → stage → main; then check the API log for "Added missing column" lines and click Load on production.
- [ ] If another `Invalid column name` appears, the bootstrap did not cover it — send the message.
- [ ] Startup errors previously swallowed on production (workload recompute, workflow repair) should now succeed; worth a look at the log after deploy.

---

**Session End** | **Branch:** kremer-dev ✅ Safe
