# IIS Deployment Notes — LOTV

**Date:** 2026-09-09
**Status:** Analysis only — NOT yet implemented
**References:**
- Boneforte (`D:\Git\WTE\Boneforte\.github\workflows\deploy-to-iis.yml`) — monolith IIS pattern, secret injection, rollback
- PointShopMall (`D:\Git\WTE\WTE_PointShopMall\.github\workflows\dotnet-ci.yml`) — **dual-project (API + Web) IIS pattern**

---

## Background

The current LOTV deploy workflows target Azure App Service. WTE's existing production pattern (Boneforte) deploys to IIS on a self-hosted Windows runner (`wte_apps3`). This document captures what would need to change to align LOTV with that pattern.

**Decision required:** Confirm whether LOTV should deploy to IIS (like Boneforte/PointShopMall) or Azure App Service before implementing anything in this document.

---

## How PointShopMall Does It (API + Web — Most Relevant for LOTV)

PointShopMall is the definitive WTE reference for deploying a dual-project (API + Web) .NET app to IIS. LOTV has the same structure.

### Pipeline Shape (5 jobs)

```
build-and-test (wte_apps3)
    └── deploy-web (wte_apps3, needs: build-and-test)
            └── deploy-api (wte_apps3, needs: deploy-web)  ← sequential
                    └── tag-and-release (ubuntu-latest, needs: deploy-web + deploy-api)
                            └── send-notification (ubuntu-latest, always)
```

**Key design decisions:**
- Both projects are published in `build-and-test` and staged to `D:\Websites\DeploymentArtifacts\WTE_PointShopMall\{run_id}\web` and `\api`
- All three deploy jobs run on the **same self-hosted runner** (`wte_apps3`), so the staging folder is shared without any artifact upload/download overhead
- `deploy-api` has `needs: deploy-web` — Web deploys first, then API
- Artifact cleanup runs at the end of `deploy-api` (not before, since all three jobs share the folder)

### IIS Site Configuration

| | Web | API |
|--|-----|-----|
| **IIS Site Name** | `pointshopmall_web` | `pointshopmall_api` |
| **App Pool** | `pointshopmall_web` | `pointshopmall_api` |
| **Port** | 80 | 8089 |
| **Host Header** | `www.pointshop.com` | *(none — port-only binding)* |
| **Website Path** | `D:\Websites\pointshopmall_web` | `D:\Websites\pointshopmall_api` |

### Per-Job Details

#### Job 1: build-and-test
1. `dotnet restore / build / test`
2. **Build number injection** — stamps `Build.Number` into both `appsettings.json` files as `1.0.{run_number}.{yyyyMMdd}-{shortSha}` via PowerShell
3. `dotnet publish Lotv.Web → DeploymentArtifacts\{run_id}\web`
4. `dotnet publish Lotv.Api → DeploymentArtifacts\{run_id}\api`

#### Job 2: deploy-web
1. Auto-create IIS site + pool if not present (idempotent)
2. Stop pool (90s timeout, force-kill `w3wp.exe` at 60s if needed)
3. Backup current website (keep last 1)
4. **Preserve logos** via `robocopy` → `D:\Websites\SharedUploads\stores\logos` before overwrite
5. Copy publish output to `D:\Websites\pointshopmall_web`
6. **Restore logos** from SharedUploads
7. Start pool
8. Health check (verify pool state = `Started`)

#### Job 3: deploy-api (runs after deploy-web)
1. Auto-create IIS site + pool if not present
2. Stop pool
3. Backup current
4. **Preserve Lucene index** → `D:\Websites\SharedUploads\lucene\{pool_name}`
5. Copy publish output to `D:\Websites\pointshopmall_api`
6. Log cleanup (delete files older than 7 days)
7. **Restore Lucene index**; delete SharedUploads staging copy
8. Start pool
9. Health check
10. **Cleanup ALL artifacts** from `DeploymentArtifacts\{run_id}\` (Web + API together)

#### Job 4: tag-and-release
- Creates GitHub Release tagged `prod-{run_number}`

#### Job 5: send-notification (always)
- Email with Web and API deploy results

### What PointShopMall Does NOT Have (but LOTV Needs)
- **No secret injection** — PointShopMall handles secrets differently; LOTV must add the Boneforte-style PowerShell JSON patch for `appsettings.Production.json`
- **No `app_offline.htm`** — uses simpler stop/copy/start; acceptable but Boneforte's pattern is safer for graceful drain
- **No EF migrations step** — PointShopMall is not an EF app; LOTV must add this before the deploy jobs run

---

## How Boneforte Does It (Monolith — Secret Injection + Rollback Reference)

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

### 5. Two-project architecture (API + Web) — RESOLVED by PointShopMall
PointShopMall answers this: **two separate IIS sites, two separate app pools, on different ports, deployed by two sequential jobs.**

LOTV target configuration:

| | Web | API |
|--|-----|-----|
| **IIS Site Name** | `lotv_web` | `lotv_api` |
| **App Pool** | `lotv_web` | `lotv_api` |
| **Port** | 80 | 8080 (or 5275 — confirm with ops) |
| **Host Header** | `lotv.wte.net` (TBD) | *(none — port-only binding)* |
| **Website Path** | `D:\Websites\lotv_web` | `D:\Websites\lotv_api` |
| **Entry DLL** | `Lotv.Web.dll` | `Lotv.Api.dll` |

Pipeline shape mirrors PointShopMall: `build-and-test → deploy-web → deploy-api` (sequential).

LOTV has no Lucene index or logo storage to preserve — skip those robocopy steps. If the ministry uploads files (future feature), add a SharedUploads preserve/restore step then.

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

### 10. Email notification ✅ DONE
Boneforte uses `autobuild@wte.net` via SocketLabs SMTP for pipeline notifications. LOTV uses the same. All secrets are set in `wtesolutions/LOTV` (set 2026-09-09):
- `SMTP_SERVER` = `smtp.socketlabs.com`
- `SMTP_PORT` = `587`
- `SMTP_USERNAME` / `SMTP_PASSWORD`
- `NOTIFICATION_EMAIL_FROM` = `autobuild@wte.net`
- `NOTIFICATION_EMAIL_TO` = `pateep@wte.net,kremer@wte.net`
- `NOTIFICATION_EMAIL_USERNAME` / `NOTIFICATION_EMAIL_PASSWORD`
- `NOTIFICATION_EMAIL_RECIPIENTS` = `pateep@wte.net,raphael@wte.net`

### 11. web.config
ASP.NET Core on IIS requires a `web.config` with the ASP.NET Core Module handler. `dotnet publish` generates this automatically — no custom `web.config` should be needed unless the app pool runs under a different identity or requires custom URL rewrite rules. Verify after first publish.

### 12. `appsettings.Production.json` in .gitignore
LOTV's `.gitignore` currently excludes `appsettings.*.local.json` but NOT `appsettings.Production.json`. The Boneforte pattern preserves the server's own `appsettings.Production.json` during deploy (never overwrites it from the build). This means the file can live on the server without being in git — the secret injection step in the pipeline writes the secrets into the published copy before it gets deployed. No `.gitignore` change needed; just ensure `appsettings.Production.json` in the repo is a placeholder with no real values (it already is).

---

## Summary of Changes Needed (Not Yet Implemented)

| # | Change | Source | Effort |
|---|--------|--------|--------|
| 1 | Confirm runner label for LOTV IIS server | Both | Trivial |
| 2 | Set path/name env vars (two sets: Web + API) | PointShopMall | Trivial |
| 3 | Adapt secret injection script for LOTV's secret set | Boneforte | Small |
| 4 | ~~Decide API+Web IIS topology~~ **Resolved: two separate sites** | PointShopMall | Done |
| 5 | Add EF migrations step before deploy jobs | LOTV-specific | Small |
| 6 | Build number injection into both `appsettings.json` files | PointShopMall | Small |
| 7 | Upgrade LOTV to .NET 10 first | Both | Medium |
| 8 | Create `deploy-to-iis.yml` (3-job: build → deploy-web → deploy-api) | PointShopMall | Medium |
| 9 | Create `rollback.yml` (adapt Boneforte — one per site or combined) | Boneforte | Small |
| 10 | Add email notification job | Both | Small |
| — | ✅ Email/SMTP secrets set in `wtesolutions/LOTV` | Done 2026-09-09 | — |
| 11 | Create/verify two IIS sites + app pools on target server | Infra | Infra |
| 12 | Add GitHub secrets to `wtesolutions/LOTV` | — | Infra |

---

## Files to Replace / Remove Once IIS Is Adopted

- `.github/workflows/deploy-staging.yml` → replace with IIS staging variant (or repurpose for a staging IIS slot)
- `.github/workflows/deploy-production.yml` → replace with `deploy-to-iis.yml`
- `docs/environment-config.md` → update secrets list to reflect IIS pattern (no Azure secrets)
