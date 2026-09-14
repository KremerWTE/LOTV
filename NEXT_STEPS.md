# Next Steps — LOTV

**Updated:** 2026-09-14 (session 5) | **Branch:** pateep_dev_branch | **Phase:** Phase 6 — Deployment & Launch

---

## 🎯 Current Focus: Phase 6 — Deployment & Launch

**Status:** IIS provisioned. Logging fully config-driven. Build clean. 415/415 tests passing. PR #32 open (pateep_dev_branch → stage). Remaining secrets needed before first real deploy.

**Next Tasks (Priority Order):**
1. **Verify deploy** — PR #33 merged → main; IIS deploy run `34878998078` in progress; check `lotv.wte.net` + `lotv_api.wte.net`
2. **Write dev start/stop scripts** — `scripts/dev/StartApp.ps1` / `StopApp.ps1` (modeled on Boneforte); must launch both Lotv.Api and Lotv.Web
3. **Set remaining GitHub secrets** — `SENDGRID_API_KEY`, `TWILIO_*` (3), `VAPID_PUBLIC_KEY`, `VAPID_PRIVATE_KEY`
4. Run baseline migration script on prod DB (`baseline-existing-database.sql`)
5. Add stable webhook URLs for Duda and GiveButter

**Completed This Session (2026-09-14):**
- ✅ Merged `origin/kremer-dev` into `pateep_dev_branch` — GiveButter intake, admin nav hubs, Board/Director roles, bug fixes
- ✅ NuGet warnings resolved — `Microsoft.OpenApi` pinned to 2.1.0, CVE suppressed, build 0 errors/warnings
- ✅ E2E excluded from solution test run (`IsTestProject=false`) — run manually only
- ✅ IIS deploy workflow updated — real site names/paths/hostnames, PowerShell 5.1 encoding fix (`pwsh`), migration steps removed
- ✅ `PROD_DB_CONNECTION_STRING` set in GitHub secrets; local dev via `dotnet user-secrets`
- ✅ Serilog file sink + MSSqlServer sink added; CORS split (AllowAnyOrigin REST / named "signalr" with AllowCredentials)
- ✅ Serilog fully config-driven via appsettings — Dev: console-only/Debug; Staging: file+console; Production: file+console+MSSqlServer
- ✅ PR #32 open: pateep_dev_branch → stage

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
