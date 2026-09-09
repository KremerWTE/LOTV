# Next Steps — LOTV

**Updated:** 2026-09-09 (session 3) | **Branch:** pateep_dev_branch | **Phase:** Phase 6 — Deployment & Launch

---

## 🎯 Current Focus: Phase 6 — Deployment & Launch

**Status:** IIS confirmed as deploy target. Email/SMTP secrets set. .NET 10 upgrade in progress.

**Next Tasks (Priority Order):**
1. ~~**Upgrade to .NET 10**~~ ✅ Done — 433/433 tests passing on net10.0
2. ~~**Write `deploy-to-iis.yml`**~~ ✅ Done — 6-job pipeline written; see `.github/workflows/deploy-to-iis.yml`
3. **Provision IIS sites on `wte_apps3`** — `lotv_web` (port 80) + `lotv_api` (port TBD); requires server access
4. Rotate `sa` SQL Server credential → create `lotv_app` least-privilege login — script at `src/Lotv.Migrations.SqlServer/rotate-app-credential.sql`
5. Run baseline migration script on prod DB (`baseline-existing-database.sql`) before CI deploy touches it
6. Add remaining GitHub secrets to `wtesolutions/LOTV`: `PROD_DB_CONNECTION_STRING`, `JWT_KEY`, Stripe, SendGrid, Twilio, VAPID keys
7. Configure Stripe account + webhook endpoint
8. Add stable webhook URLs for Duda and GiveButter

**Completed This Session:**
- ✅ IIS confirmed as deploy target (not Azure); Azure workflows to be replaced
- ✅ Email/SMTP secrets set in `wtesolutions/LOTV` (9 secrets)
- ✅ IIS deployment notes updated with PointShopMall dual-project pattern
- ✅ Gap report updated to reflect IIS decision + partial secret completion
- ✅ .NET 10 upgrade complete — all 6 projects, 433/433 tests passing
- ✅ NuGet vulnerability audit — SQLitePCLRaw fixed; 2 upstream-unresolvable CVEs documented
- ✅ `deploy-to-iis.yml` written — 6-job pipeline (build+test → migrations → deploy-web → deploy-api → tag → notify)

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
