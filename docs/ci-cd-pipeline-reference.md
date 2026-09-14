# CI/CD Pipeline Reference — LOTV

**Date:** 2026-09-09
**Author:** Generated from code analysis
**Status:** Current as of commit `88da30b` (pateep_dev_branch)

---

## Overview

LOTV has three GitHub Actions workflows. Two are fully built but blocked pending Azure infrastructure provisioning. One (CI) is active on every PR.

---

## 1. `ci.yml` — Build & Test (PR Gate)

**Triggers:** PRs targeting `main` or `kremer-dev`; direct pushes to `main`

**Runner:** `ubuntu-latest` (GitHub-hosted)

**Pipeline stages:**

| Step | What it does | Why it's there |
|------|-------------|----------------|
| Free disk space | Removes Android SDK, GHC, Swift, CodeQL (~8GB) | Ubuntu runners hit `ENOSPC` without this — real bug fixed |
| Restore → Build | `dotnet restore` + `dotnet build --configuration Release` | Standard build gate |
| **Publish (API + Web)** | `dotnet publish` both projects | Catches `RZ9985` Razor errors that `dotnet build` misses — the class of bug where two components share a name across namespaces only fails at publish time |
| Install Playwright browsers | `playwright.ps1 install --with-deps chromium` | Required for E2E test suite |
| Start API + Web | Both apps started in background; polls `/health` until ready | Prerequisite for E2E tests |
| Unit/integration tests | 433 tests with Coverlet XML coverage; `.runsettings` excludes EF migrations from coverage count | Without `.runsettings`, generated EF SQL (~90k lines at 0% coverage) buried the real ~86% figure |
| E2E tests | Playwright/Chromium against live API+Web | No coverage collected — browser-driver metrics aren't meaningful |
| Publish test results | `dorny/test-reporter@v3` posts TRX to PR | Inline test results in PR |
| Coverage summary | `CodeCoverageSummary` badge + sticky PR comment | Coverage thresholds: 60% warn / 80% pass |

**Secrets required:** None (SQLite in-memory + test JWT key hardcoded in workflow)

**Currently runnable:** ✅ Yes

---

## 2. `deploy-staging.yml` — Publish & Deploy → Staging

**Triggers:** Manual (`workflow_dispatch`) only — auto-trigger disabled until Azure secrets are set

**Runner:** `ubuntu-latest` (GitHub-hosted)

**Pipeline stages:**

| Step | What it does |
|------|-------------|
| Test gate | Full test suite — deploy blocked if any test fails |
| Build `Lotv.Migrations.SqlServer` | Explicit build BEFORE `dotnet ef` runs — required because of a circular dependency workaround (no `ProjectReference` back to `Lotv.Api`; a post-build copy target copies the DLL, but only if it exists first) |
| EF migrations | `dotnet ef database update` against staging SQL Server (`ASPNETCORE_ENVIRONMENT=Staging`, `Database__Provider=SqlServer`) |
| Publish API + Web | `dotnet publish` → `publish/api` and `publish/web` folders |
| Azure login | Federated identity via `azure/login@v3` |
| Deploy API + Web | `azure/webapps-deploy@v3` zip-deploy to App Service |
| Failure notification | Echoes failed commit SHA (Slack/PagerDuty placeholder) |

**GitHub Secrets required (on `wtesolutions/LOTV`):**

| Secret | Purpose |
|--------|---------|
| `AZURE_CLIENT_ID` | Federated identity client |
| `AZURE_TENANT_ID` | Azure tenant |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription |
| `AZURE_WEBAPP_API_NAME` | App Service name for staging API |
| `AZURE_WEBAPP_WEB_NAME` | App Service name for staging Web |
| `DB_CONNECTION_STRING` | SQL Server connection string (staging) |

**Currently runnable:** ❌ Blocked — secrets not set, Azure resources not provisioned

---

## 3. `deploy-production.yml` — Publish & Deploy → Production

**Triggers:** Push of a semver tag (`v1.0.0`, `v1.2.3`, etc.) — only deliberate manual tagging triggers a prod deploy

**Runner:** `ubuntu-latest` (GitHub-hosted)

**Pipeline stages:** Identical to staging, plus:

| Step | What it does |
|------|-------------|
| Extract version | Tag name → `VERSION` output variable |
| Create GitHub Release | `softprops/action-gh-release@v3` auto-generates release notes on success |
| Failure notification | "on-call / PagerDuty" placeholder |

**Uses `production` GitHub environment** — can be configured to require manual approval before deploy steps execute.

**GitHub Secrets required (on `wtesolutions/LOTV`):** Same as staging plus:

| Secret | Purpose |
|--------|---------|
| `AZURE_WEBAPP_API_NAME_PROD` | App Service name for production API |
| `AZURE_WEBAPP_WEB_NAME_PROD` | App Service name for production Web |

**Currently runnable:** ❌ Blocked — same as staging

---

## What's Still Needed to Go Live

1. **Provision Azure App Service resources** — two App Service plans (one for API, one for Web); Web should use a Linux plan (Blazor Server is a standard Kestrel app now, not WASM static hosting)
2. **Add GitHub secrets** to `wtesolutions/LOTV` repository settings (6 secrets listed above)
3. **Run the baseline migration script** (`src/Lotv.Migrations.SqlServer/baseline-existing-database.sql`) once against the live `10.100.1.87` database before the first CI deploy runs EF migrations against it
4. **Rotate the `sa` credential** and switch to the `lotv_app` least-privilege login (script at `src/Lotv.Migrations.SqlServer/rotate-app-credential.sql`)
5. **Enable the staging auto-trigger** in `deploy-staging.yml` by restoring `push: branches: [main]` once secrets are set

---

## IIS Deployment: Adjustment Notes

> See `docs/iis-deployment-notes.md` for the full IIS migration analysis.

The current Azure App Service approach may need to be replaced with IIS deployment matching WTE's existing pattern (used in the Boneforte project). Key differences and required changes are documented separately.

---

## Related Files

- `.github/workflows/ci.yml`
- `.github/workflows/deploy-staging.yml`
- `.github/workflows/deploy-production.yml`
- `src/Lotv.Migrations.SqlServer/` — SQL Server migrations project
- `src/Lotv.Migrations.SqlServer/baseline-existing-database.sql` — one-time baseline script
- `src/Lotv.Migrations.SqlServer/rotate-app-credential.sql` — credential rotation script
- `docs/environment-config.md` — full secrets reference
- `docs/smoke-test-checklist.md` — post-deploy smoke test checklist
