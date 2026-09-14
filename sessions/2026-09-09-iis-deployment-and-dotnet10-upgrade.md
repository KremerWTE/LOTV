# Session Summary — IIS Deployment Pipeline + .NET 10 Upgrade

**Date:** 2026-09-09
**Branch:** pateep_dev_branch
**Phase:** Phase 6 — Deployment & Launch
**Session Type:** Operations / Development

---

## Session Objectives

**Primary Goals:**
1. Finalize hosting decision (IIS vs Azure) and document the PointShopMall dual-project IIS pattern
2. Upgrade all projects from .NET 9 to .NET 10
3. Write the production IIS deploy pipeline (`deploy-to-iis.yml`)

**Status:** ✅ Complete

---

## Key Accomplishments

### 1. IIS Confirmed as Deployment Target ✅

**Description:** Confirmed IIS on self-hosted runner `wte_apps3` as the deployment target, replacing the previously-planned Azure App Service approach. Researched PointShopMall (`D:\Git\WTE\WTE_PointShopMall`) as the definitive WTE reference for dual-project (API + Web) IIS deployment.

**Impact:** Resolves the last major architectural ambiguity for Phase 6. Azure workflows will be retired in favour of the new IIS pipeline.

**Details:**
- `docs/iis-deployment-notes.md` updated with full PointShopMall dual-project pipeline breakdown
- Two separate IIS sites confirmed: `lotv_web` (port 80) and `lotv_api` (port 8080), two separate app pools, two sequential deploy jobs
- Azure blob storage, CDN, Key Vault items struck from backlog as not needed
- Gap report (GAP-03) updated to reflect IIS decision and partial secret completion

### 2. Email/SMTP GitHub Secrets Set ✅

**Description:** Set 9 email/SMTP secrets in `wtesolutions/LOTV` via `gh secret set`.

**Impact:** Pipeline notification email is fully configured and will work as soon as `deploy-to-iis.yml` runs.

**Secrets set:**
- `SMTP_SERVER`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`
- `NOTIFICATION_EMAIL_FROM` (autobuild@wte.net), `NOTIFICATION_EMAIL_TO`
- `NOTIFICATION_EMAIL_USERNAME`, `NOTIFICATION_EMAIL_PASSWORD`, `NOTIFICATION_EMAIL_RECIPIENTS`

### 3. .NET 10 Upgrade ✅

**Description:** Upgraded all 6 projects from `net9.0` → `net10.0` and bumped all Microsoft package references to `10.0.0`.

**Impact:** Matches WTE's other production apps (Boneforte, PointShopMall both target .NET 10). Required before writing the IIS deploy workflow.

**Details:**
- All 6 projects updated: `Lotv.Api`, `Lotv.Core`, `Lotv.Web`, `Lotv.Migrations.SqlServer`, `Lotv.Tests`, `Lotv.E2E`
- `Npgsql.EntityFrameworkCore.PostgreSQL` bumped to `10.0.0`
- `Microsoft.AspNetCore.Components.Authorization` removed from `Lotv.Web` (framework-included in .NET 10, was causing NU1510 warning)
- CI workflows updated to `DOTNET_VERSION: '10.0.x'`; Playwright path updated from `net9.0` → `net10.0`
- **433/433 tests passing on .NET 10**

### 4. NuGet Vulnerability Audit ✅

**Description:** Ran `dotnet list package --vulnerable --include-transitive` and addressed all actionable items.

**Details:**
- `SQLitePCLRaw.lib.e_sqlite3` — **fixed** by pinning to 2.1.12 across Lotv.Api, Lotv.Migrations.SqlServer, Lotv.Tests
- `Microsoft.OpenApi` 2.x (CVE GHSA-v5pm-xwqc-g5wc) — **no patched 2.x version exists upstream**; pulled in by `Microsoft.AspNetCore.OpenApi 10.0.0`. Mitigation: restrict `/openapi/*` route in IIS prod config.
- `System.Security.Cryptography.Xml` 9.0.0 — **NuGet graph artifact**; at .NET 10 runtime the framework ships the patched version; standalone package is never loaded.

### 5. `deploy-to-iis.yml` Written ✅

**Description:** Full 6-job IIS production deploy pipeline, combining PointShopMall's dual-project pattern with Boneforte's secret injection and graceful `app_offline.htm` drain approach.

**Impact:** Replaces `deploy-staging.yml` and `deploy-production.yml`. Ready to run once IIS sites are provisioned and remaining app secrets are added.

**Pipeline shape:**
```
build-and-test  (wte_apps3)
    └── run-migrations  (wte_apps3)
            └── deploy-web  (wte_apps3)
                    └── deploy-api  (wte_apps3)
                            └── tag-and-release  (wte_apps3)
send-notification  (ubuntu-latest, always)
```

**Key design decisions:**
- Secrets injected into `appsettings.Production.json` in `build-and-test` (Boneforte pattern)
- EF Core migrations run as a dedicated job, before deploy, with network access to `10.100.1.87`
- `app_offline.htm` written before each pool stop for graceful ASP.NET Core drain
- Pool always restarted on `if: always()` — site never left offline on deploy failure
- API health check hits `localhost:8080/health` after pool start (advisory warning, not blocking)
- Artifact cleanup on `deploy-api` `always()` step
- Config key paths verified against source: `Stripe:*`, `Twilio:*`, `Push:VapidPublicKey/VapidPrivateKey`

### 6. Planning Documents Updated ✅

- `MASTER_TODO.md` — header updated to .NET 10; Phase 5 marked complete; Phase 6 infra section rewritten for IIS; Platform Upgrade section updated with completed items
- `NEXT_STEPS.md` — reprioritized for current state; all session completions recorded
- `README.md` — runtime updated to .NET 10; SDK prerequisite updated
- `PROJECT_STRUCTURE.md` — stack updated to .NET 10
- `docs/iis-deployment-notes.md` — PointShopMall pattern fully documented; item 10 (email secrets) marked done
- `docs/gap-report-2026-09-09.md` — GAP-03 updated (IIS confirmed; email secrets done; remaining secrets listed)

---

## Files Created/Modified

### Created
- `.github/workflows/deploy-to-iis.yml` — Full 6-job IIS production deploy pipeline
- `sessions/2026-09-09-iis-deployment-and-dotnet10-upgrade.md` — This file

### Modified
- `src/Lotv.Api/Lotv.Api.csproj` — net10.0; packages 10.0.0; transitive vuln overrides; Npgsql 10.0.0
- `src/Lotv.Core/Lotv.Core.csproj` — net10.0
- `src/Lotv.Web/Lotv.Web.csproj` — net10.0; packages 10.0.0; removed redundant Components.Authorization ref
- `src/Lotv.Migrations.SqlServer/Lotv.Migrations.SqlServer.csproj` — net10.0; packages 10.0.0; transitive vuln overrides
- `tests/Lotv.Tests/Lotv.Tests.csproj` — net10.0; packages 10.0.0; transitive vuln overrides
- `tests/Lotv.E2E/Lotv.E2E.csproj` — net10.0
- `.github/workflows/ci.yml` — .NET 10; Playwright path net10.0
- `.github/workflows/deploy-staging.yml` — .NET 10 (to be replaced by deploy-to-iis.yml)
- `.github/workflows/deploy-production.yml` — .NET 10 (to be replaced by deploy-to-iis.yml)
- `MASTER_TODO.md` — multiple sections updated
- `NEXT_STEPS.md` — reprioritized
- `README.md` — .NET 10
- `PROJECT_STRUCTURE.md` — .NET 10
- `docs/iis-deployment-notes.md` — PointShopMall pattern + email secrets done
- `docs/gap-report-2026-09-09.md` — GAP-03 updated

---

## Git Activity

### Commits Made
- `9413b09` — `chore(claude): install restrictive v1.4 directives and NEXT_STEPS.md` (session 2, carried forward)
- `88da30b` — `docs(sessions): add 2026-09-09 v1.4 directive onboarding session notes` (session 2, carried forward)
- `6d1bfd1` — `docs: add CI/CD reference, IIS notes, gap report, project structure, updated README` (session 2, carried forward)
- `bd38600` — `feat(platform): upgrade to .NET 10; document IIS deployment decision`
- `f172ac3` — `feat(ci): add deploy-to-iis.yml for IIS production deployment`

### Branch Status
- **Current Branch:** pateep_dev_branch ✅ Safe
- **Push Status:** ⏳ Pending (pushing at session close)

---

## Current Project State

- **Build:** ✅ PASS — 0 errors, 0 code warnings on net10.0
- **Tests:** ✅ 433/433 passing on .NET 10
- **Branch:** pateep_dev_branch — ✅ Safe
- **Live form:** JotForm 261395566857171 — unchanged; HIPAA-BAA restrictions apply; outstanding manual checklist with ministry
- **Production DB:** `10.100.1.87` — unchanged; `sa` credential still in use; `rotate-app-credential.sql` ready but not run; `baseline-existing-database.sql` ready but not run

---

## Open Items / Blockers

- [ ] **IIS sites provisioned on `wte_apps3`** — requires server admin access; `lotv_web` (port 80) and `lotv_api` (port 8080)
- [ ] **Remaining GitHub secrets** — `PROD_DB_CONNECTION_STRING`, `JWT_KEY`, `STRIPE_*`, `SENDGRID_API_KEY`, `TWILIO_*`, `VAPID_*` (requires Stripe/Twilio accounts + prod DB login)
- [ ] **SQL Server credential rotation** — run `rotate-app-credential.sql` as prod DB admin; update connection string secret
- [ ] **EF migration baseline** — run `baseline-existing-database.sql` on `10.100.1.87` before first pipeline deploy
- [ ] **JotForm manual checklist** — ministry must apply pending form settings changes via builder UI

---

## Next Steps

### Immediate (Next Session)
1. Provision IIS sites/pools on `wte_apps3` (requires server access — coordinate with ops)
2. Add remaining GitHub secrets to `wtesolutions/LOTV`
3. Run `baseline-existing-database.sql` + `rotate-app-credential.sql` on `10.100.1.87`
4. Trigger `deploy-to-iis.yml` first run and fix any environment-specific issues

---

## Verification Checklist

- [x] All code changes committed
- [x] Session summary created (this file)
- [x] NEXT_STEPS.md updated
- [x] MASTER_TODO.md updated
- [x] All tests passing (433/433)
- [ ] All commits pushed to `pateep_dev_branch` ← pushing now

---

**Session End** | **Branch:** pateep_dev_branch ✅ Safe
