# Next Steps — LOTV

**Updated:** 2026-09-09 (session 2) | **Branch:** pateep_dev_branch | **Phase:** Phase 6 — Deployment & Launch

---

## 🎯 Current Focus: Phase 6 — Deployment & Launch

**Status:** CI/CD pipeline built and validated; Azure App Service hosting decided; blocked on Azure account access and prod DB credential rotation.

**Next Tasks (Priority Order):**
1. **Decide: IIS vs Azure App Service** — see `docs/iis-deployment-notes.md`; IIS matches WTE's existing Boneforte pattern; Azure is what's currently built
2. **Upgrade to .NET 10** — prereq for IIS migration path; update all 6 projects + CI workflow `DOTNET_VERSION`; run `dotnet list package --vulnerable` after
3. Rotate `sa` SQL Server credential → create `lotv_app` least-privilege login (requires prod DB admin access) — script at `src/Lotv.Migrations.SqlServer/rotate-app-credential.sql`
4. Run baseline migration script on prod DB (`baseline-existing-database.sql`) before CI deploy touches it
5. Provision hosting (IIS site/pool or Azure App Service) + add GitHub secrets to `wtesolutions/LOTV`
6. Configure Stripe account + webhook endpoint + store keys in secrets manager
7. Add stable webhook URLs for Duda and GiveButter (replace session-only cloudflared URLs)

**Completed in Current Phase:**
- ✅ Azure App Service hosting decided (code-based, no Docker in deploy path)
- ✅ CI/CD pipelines built and validated (ci.yml, deploy-staging.yml, deploy-production.yml)
- ✅ SQL Server migrations project created + connection string bug fixed
- ✅ EF migration history reconciled for SQL Server

---

## 📊 Recent Completion: Onboarding + Directives

**Achievement:** CI/CD reference doc, IIS deployment notes (vs Boneforte pattern), GAP report, PROJECT_STRUCTURE.md, README.md rewrite, .NET 10 upgrade TODO added.

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
