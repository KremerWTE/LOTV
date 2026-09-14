# Next Steps — LOTV

**Updated:** 2026-09-14 (session 6) | **Branch:** pateep_dev_branch | **Phase:** Phase 6 — Deployment & Launch

---

## 🎯 Current Focus: Phase 6 — Deployment & Launch

**Status:** App starts cleanly locally against prod SQL Server. 415/415 tests passing. 10 commits ready to push through PR chain. Remaining secrets + webhook URLs still needed.

**Next Tasks (Priority Order):**
1. **PR pateep_dev_branch → stage** (CI: build+test+notify) — in progress this session
2. **PR stage → main** (IIS deploy) — after CI passes
3. **Set remaining GitHub secrets** — `SENDGRID_API_KEY`, `TWILIO_*` (3), `VAPID_PUBLIC_KEY`, `VAPID_PRIVATE_KEY`
4. Run baseline migration script on prod DB (`baseline-existing-database.sql`)
5. Add stable webhook URLs for Duda and GiveButter
6. Verify `lotv.wte.net` + `lotv_api.wte.net` after deploy

**Completed This Session (2026-09-14 session 6):**
- ✅ Serilog MSSqlServer sink options path fixed (was crashing API on startup in production)
- ✅ Serilog added to `Lotv.Web` — Dev: console/Debug; Production: console+file+MSSqlServer
- ✅ `PROD_DB_CONNECTION_STRING` injected into Web `appsettings.Production.json` at deploy time
- ✅ Dev port conflicts resolved — API: `5100`, Web: `5101`
- ✅ API `Database:Provider=SqlServer` added to `appsettings.Development.json`
- ✅ Web `UseHttpsRedirection` skipped in Development (was logging WRN on http profile)
- ✅ Test factory fixed — strips all EF registrations before adding SQLite (was crashing with "two providers" error)
- ✅ Dev seed crash fixed — `EnsureCreatedAsync` replaced with `MigrateAsync` for SQL Server; startup wrapped in try/catch so DB errors log a warning instead of crashing
- ✅ Dev start/stop scripts written (`scripts/dev/StartApp.ps1` / `StopApp.ps1`)
- ✅ GAP report generated (`docs/gap-report-2026-09-14.md`) — 6 open items, all infra/external
- ✅ App starts cleanly locally against prod SQL Server (`10.100.1.87`)

---

## 📊 Previously Completed: Onboarding + Directives (Session 2)

**Achievement:** Directive v1.4 installed, CI/CD reference doc, IIS deployment notes (Boneforte), GAP report, PROJECT_STRUCTURE.md, README.md rewrite.

---

## 🔗 Quick Links

**Planning:**
- Full TODO: `MASTER_TODO.md`
- Last Session: `sessions/2026-09-01-jotform-design-review-and-hipaa-baa-rootcause.md`

**Directives:**
- Startup: `.claude/SESSION_STARTUP_DIRECTIVE.md`
- Critical Rules: `.claude/CRITICAL_RULES_CONSOLIDATED.md`
- File Org: `.claude/FILE_ORGANIZATION_DIRECTIVE.md`
- Git: `.claude/git-workflow-directive.md`

---

## ⚠️ Critical Rules

**Protected Branches:** NO commits to: main, master, dev, *beta*, *stage*, *prod*, *deploy*
**Current Branch:** pateep_dev_branch (✅ Safe)
**Commits:** Format: `type(scope): description` | NO "Co-Authored-By: Claude"
**JotForm:** HIPAA-BAA account — builder UI only; zero programmatic writes
**Prod DB:** `10.100.1.87` — no direct AI session access to live data

---

**For full context, load:** MASTER_TODO.md + latest session summary
**For startup protocol, see:** .claude/SESSION_STARTUP_DIRECTIVE.md
