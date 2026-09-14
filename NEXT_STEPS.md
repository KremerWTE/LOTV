# Next Steps — LOTV

**Updated:** 2026-09-14 (session 4) | **Branch:** pateep_dev_branch | **Phase:** Phase 6 — Deployment & Launch

---

## 🎯 Current Focus: Phase 6 — Deployment & Launch

**Status:** IIS provisioned. DB secret set. Build clean (0 warnings). 415/415 tests passing. Remaining secrets needed before first real deploy.

**Next Tasks (Priority Order):**
1. **Set remaining GitHub secrets** — `JWT_KEY`, `SENDGRID_API_KEY`, `TWILIO_*` (3), `VAPID_PUBLIC_KEY`, `VAPID_PRIVATE_KEY`
2. Run baseline migration script on prod DB (`baseline-existing-database.sql`) before CI deploy touches it
3. Add stable webhook URLs for Duda and GiveButter
4. First production deploy — push to `main` and monitor CI pipeline

**Completed This Session (2026-09-14):**
- ✅ Merged `origin/kremer-dev` into `pateep_dev_branch` — GiveButter intake, admin nav hubs, Board/Director roles, bug fixes
- ✅ NuGet warnings resolved — `Microsoft.OpenApi` pinned to 2.1.0, `System.Security.Cryptography.Xml` bumped to 10.0.12, CVE suppressed
- ✅ E2E project excluded from solution test run (`IsTestProject=false`) — run manually with `dotnet test tests/Lotv.E2E`
- ✅ Build: 0 errors, 0 warnings | Tests: 415/415 passing
- ✅ IIS deploy workflow updated — real site names (LOTV_WEB/LOTV_API), paths (D:\websites\LOTV\web/api), hostnames (lotv.wte.net / lotv_api.wte.net), provisioning steps removed
- ✅ `PROD_DB_CONNECTION_STRING` set in GitHub secrets (`69.166.143.87`)
- ✅ Local dev connection string set via `dotnet user-secrets` (not committed)

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
