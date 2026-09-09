# IIS Deployment Notes — LOTV

**Date:** 2026-09-09
**Status:** Analysis only — NOT yet implemented
**Reference:** Boneforte project (`D:\Git\WTE\Boneforte\.github\workflows\deploy-to-iis.yml`)

---

## Background

The current LOTV deploy workflows target Azure App Service. WTE's existing production pattern (Boneforte) deploys to IIS on a self-hosted Windows runner (`wte_apps3`). This document captures what would need to change to align LOTV with that pattern.

**Decision required:** Confirm whether LOTV should deploy to IIS (like Boneforte) or Azure App Service before implementing anything in this document.

---

## How Boneforte Does It

### Runner
- **Self-hosted Windows runner:** `wte_apps3` (the actual Windows Server running IIS)
- All deploy steps run directly on the machine that serves the site — no remote deployment tools needed
- IIS PowerShell modules (`WebAdministration`, `IISAdministration`) available natively

### Deploy Flow (4 jobs)

```
build-and-test
    └── deploy-to-iis (environment: production)
            └── post-deployment-verification (smoke tests)
                    └── email-notification (always)
```

#### Job 1: build-and-test
1. Checkout + Setup .NET 10
2. `dotnet restore` + `dotnet build --configuration Release`
3. `dotnet test` (unit tests only, `Category!=Integration`)
4. `dotnet publish` → `./publish/`
5. **Inject secrets into `appsettings.Production.json`** via PowerShell — reads the JSON, sets each secret path by dotted key, writes back. Does NOT use environment variables at runtime.
6. Copy `./publish/` → `D:\Websites\DeploymentArtifacts\{AppName}\{RunId}\` (shared staging area on the runner disk)

#### Job 2: deploy-to-iis
1. Verify IIS app pool and site exist, staging artifact folder is populated
2. **Take site offline:** write `app_offline.htm` → wait 3s → stop app pool → force-kill any lingering `w3wp.exe` → extra 8s grace period for AV/backup agents
3. Create timestamped backup of current `D:\Websites\BoneForte\` (keeps last 5, deletes older)
4. **Copy files** with retry (12 attempts, 5s delay) — skips `appsettings.json`, `appsettings.Production.json`, `app_offline.htm`, `connectionstrings.config` (preserves server-local credential files)
5. Verifies critical files (`BoneForte.Web.dll`, `web.config`, `appsettings.json`) exist
6. Removes `app_offline.htm`
7. Start app pool (always runs, even on failure)
8. Cleanup staging artifact folder

#### Job 3: post-deployment-verification
- 30s warmup wait
- HTTP smoke tests (homepage, `/health`, login page) — advisory only, don't fail the deploy
- Results posted to GitHub Step Summary

#### Job 4: email-notification
- Runs on `ubuntu-latest` (SMTP via `curl --ssl-reqd`)
- Fetches the most recently merged PR body via GitHub API for change notes
- Sends pass/fail email to `NOTIFICATION_EMAIL_RECIPIENTS`

---

## Adjustments Required for LOTV

### 1. Runner label
**Boneforte:** `runs-on: [wte_apps3]`
**LOTV change needed:** Confirm which self-hosted runner label to use for the LOTV IIS server. May be `wte_apps3` (same machine) or a different runner. Update `runs-on` in all deploy jobs.

### 2. Path variables
**Boneforte:**
```yaml
WEBSITE_PATH: 'D:\Websites\BoneForte'
IIS_SITE_NAME: BoneForte
IIS_APP_POOL_NAME: BoneForte
PRODUCTION_URL: 'https://BoneForte.wte.net'
PROJECT_PATH: 'src/BoneForte.Web/BoneForte.Web.csproj'
```
**LOTV change needed:**
```yaml
WEBSITE_PATH: 'D:\Websites\LOTV'          # confirm actual path
IIS_SITE_NAME: LOTV                        # confirm IIS site name
IIS_APP_POOL_NAME: LOTV                    # confirm app pool name
PRODUCTION_URL: 'https://lotv.wte.net'    # confirm URL
PROJECT_PATH: 'src/Lotv.Web/Lotv.Web.csproj'
```

### 3. Secret injection into appsettings.Production.json
Boneforte patches secrets directly into `appsettings.Production.json` after publish. LOTV needs the same approach. The secrets required for LOTV differ from Boneforte's. Required mapping:

| GitHub Secret | `appsettings.Production.json` path |
|--------------|----------------------------------|
| `PROD_DB_CONNECTION_STRING` | `ConnectionStrings.DefaultConnection` |
| `JWT_KEY` | `Jwt.Key` |
| `STRIPE_SECRET_KEY` | `Stripe.SecretKey` |
| `STRIPE_WEBHOOK_SECRET` | `Stripe.WebhookSecret` |
| `STRIPE_PUBLISHABLE_KEY` | `Stripe.PublishableKey` |
| `SENDGRID_API_KEY` (or chosen email provider) | email provider path |
| `TWILIO_ACCOUNT_SID` / `TWILIO_AUTH_TOKEN` / `TWILIO_FROM_NUMBER` | SMS settings |
| `VAPID_PUBLIC_KEY` / `VAPID_PRIVATE_KEY` | push notification settings |

The PowerShell injection script from Boneforte can be adapted directly — it's generic JSON patching.

### 4. Critical files list
**Boneforte critical files:** `BoneForte.Web.dll`, `web.config`, `appsettings.json`
**LOTV change needed:** `Lotv.Web.dll`, `web.config`, `appsettings.json`

**Important:** The LOTV deploy also ships `Lotv.Api.dll` (separate API project). Decision needed: deploy API and Web as a single IIS site with sub-applications, or as two separate App Pools/sites. Boneforte is a monolith (one project); LOTV has two separately deployable projects.

### 5. Two-project architecture (API + Web)
LOTV differs from Boneforte in having a separate API (`Lotv.Api`) and Web (`Lotv.Web`). Options:
- **Option A:** Two separate IIS sites / app pools — one for the API (e.g., `LOTV-API` at `api.lotv.wte.net`), one for the Web (e.g., `LOTV-Web` at `lotv.wte.net`). Cleanest separation; requires two deploy jobs mirroring the Boneforte pattern.
- **Option B:** Single IIS site with the API as an IIS sub-application under the Web site root. More complex IIS config; avoids CORS between API and Web since both are on the same origin.
- **Recommendation:** Option A (two separate sites), matching Boneforte's single-site-per-pool pattern, scaled to two pools.

### 6. EF migrations step
Boneforte does not have an EF migration step in its pipeline. LOTV needs one, inherited from the current Azure workflow:
```yaml
- name: Build SQL Server migrations assembly
  run: dotnet build src/Lotv.Migrations.SqlServer/... --configuration Release

- name: Run database migrations
  run: dotnet ef database update --project src/Lotv.Api ...
  env:
    Database__Provider: SqlServer
    ConnectionStrings__DefaultConnection: ${{ secrets.PROD_DB_CONNECTION_STRING }}
```
This step must run on the self-hosted runner (which has network access to `10.100.1.87`).

### 7. .NET version
**Boneforte:** .NET 10 (`DOTNET_VERSION: '10.0.x'`)
**LOTV current:** .NET 9 (`<TargetFramework>net9.0</TargetFramework>`)
**LOTV change needed:** Upgrade to .NET 10 before migrating the deploy workflow (see MASTER_TODO.md backlog). The IIS migration is a good forcing function to do this upgrade first.

### 8. Trigger
**Boneforte:** `push: branches: [main]` + `workflow_dispatch`
**LOTV change needed:** Same — auto-deploy on merge to `main`. Remove the current Azure `workflow_dispatch`-only gate once IIS is configured.

### 9. Rollback workflow
Boneforte has a dedicated `rollback.yml` that restores from timestamped backups. LOTV should adopt the same pattern. The Boneforte rollback workflow can be copied almost verbatim with path/name substitutions.

### 10. Email notification
Boneforte uses `autobuild@wte.net` via SocketLabs SMTP for pipeline notifications. LOTV should use the same sender. Secrets needed:
- `NOTIFICATION_EMAIL_FROM`
- `NOTIFICATION_EMAIL_RECIPIENTS`
- `NOTIFICATION_EMAIL_USERNAME`
- `NOTIFICATION_EMAIL_PASSWORD`
- `SMTP_SERVER` (defaults to `smtp.socketlabs.com`)
- `SMTP_PORT` (defaults to `587`)

### 11. web.config
ASP.NET Core on IIS requires a `web.config` with the ASP.NET Core Module handler. `dotnet publish` generates this automatically — no custom `web.config` should be needed unless the app pool runs under a different identity or requires custom URL rewrite rules. Verify after first publish.

### 12. `appsettings.Production.json` in .gitignore
LOTV's `.gitignore` currently excludes `appsettings.*.local.json` but NOT `appsettings.Production.json`. The Boneforte pattern preserves the server's own `appsettings.Production.json` during deploy (never overwrites it from the build). This means the file can live on the server without being in git — the secret injection step in the pipeline writes the secrets into the published copy before it gets deployed. No `.gitignore` change needed; just ensure `appsettings.Production.json` in the repo is a placeholder with no real values (it already is).

---

## Summary of Changes Needed (Not Yet Implemented)

| # | Change | Effort |
|---|--------|--------|
| 1 | Confirm runner label for LOTV IIS server | Trivial |
| 2 | Set path/name env vars (WEBSITE_PATH, IIS_SITE_NAME, etc.) | Trivial |
| 3 | Adapt secret injection script for LOTV's secret set | Small |
| 4 | Decide API+Web IIS topology (two sites vs. sub-app) | Decision |
| 5 | Add EF migrations step to IIS deploy workflow | Small |
| 6 | Upgrade LOTV to .NET 10 first | Medium |
| 7 | Create LOTV-specific `deploy-to-iis.yml` (adapting Boneforte) | Medium |
| 8 | Create LOTV `rollback.yml` (adapt Boneforte directly) | Small |
| 9 | Add email notification job | Small |
| 10 | Create/verify IIS site + app pool on target server | Infra |
| 11 | Add GitHub secrets to `wtesolutions/LOTV` | Infra |

---

## Files to Replace / Remove Once IIS Is Adopted

- `.github/workflows/deploy-staging.yml` → replace with IIS staging variant (or repurpose for a staging IIS slot)
- `.github/workflows/deploy-production.yml` → replace with `deploy-to-iis.yml`
- `docs/environment-config.md` → update secrets list to reflect IIS pattern (no Azure secrets)
