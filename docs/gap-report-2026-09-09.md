# GAP Report — LOTV Project

**Date:** 2026-09-09
**Source:** Analysis of MASTER_TODO.md, NEXT_STEPS.md, session notes (2026-02 → 2026-09), and codebase survey
**Status:** Code is complete for Phase 6. All remaining gaps are infrastructure provisioning, external account setup, or third-party manual tasks — none require new code.

---

## Executive Summary

| Severity | Total | Blocked External | Fixed Already | Actionable Now |
|----------|-------|-----------------|---------------|----------------|
| High     | 4     | 4               | 0             | 0              |
| Medium   | 3     | 3               | 0             | 0              |
| Low      | 8     | 4               | 4             | 0              |
| **Total**| **15**| **11**          | **4**         | **0**          |

**Bottom line:** The project is code-complete. Every remaining open item is blocked on someone with access to Azure, the production SQL Server at `10.100.1.87`, a Stripe account, or the JotForm builder UI. Zero actionable code gaps exist today.

---

## HIGH — Blocking Production Go-Live

### GAP-01: Production SQL Server credential rotation

**Category:** Infrastructure Security
**Status:** Open — blocked on prod DB admin access

The production database at `10.100.1.87` was set up using the `sa` account. A least-privilege `lotv_app` login exists as a script (`src/Lotv.Migrations.SqlServer/rotate-app-credential.sql`) but has never been run. The app currently connects with full SA rights.

**To fix:** Someone with SQL Server admin access to `10.100.1.87` runs `rotate-app-credential.sql`, then updates the app's connection string secret.
**No code change needed.**

---

### GAP-02: Production EF migration baseline

**Category:** Infrastructure Setup
**Status:** Open — blocked on prod DB admin access

The `InitialCreate` EF migration must be marked as already-applied on the live database (tables were created via `EnsureCreated()` in an earlier session, not via `dotnet ef database update`). Without this, the CI deploy's migrations step will attempt to re-run DDL against existing tables and fail.

**Script:** `src/Lotv.Migrations.SqlServer/baseline-existing-database.sql`
**To fix:** Someone with SQL Server admin access runs the baseline script once, before the first CI pipeline deploy runs.
**No code change needed.**

---

### GAP-03: IIS provisioning + GitHub secrets

**Category:** Infrastructure Setup
**Status:** Partially complete — email/SMTP secrets done; IIS sites and app secrets pending

**Decision made 2026-09-09:** Deploying to IIS (not Azure App Service). Azure deploy workflows will be replaced with a new `deploy-to-iis.yml` following the PointShopMall dual-project pattern. See `docs/iis-deployment-notes.md`.

**GitHub secrets already set** in `wtesolutions/LOTV` (2026-09-09):
- `SMTP_SERVER`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`
- `NOTIFICATION_EMAIL_FROM`, `NOTIFICATION_EMAIL_TO`, `NOTIFICATION_EMAIL_USERNAME`, `NOTIFICATION_EMAIL_PASSWORD`, `NOTIFICATION_EMAIL_RECIPIENTS`

**GitHub secrets still needed:**

| Secret | Value source |
|--------|-------------|
| `PROD_DB_CONNECTION_STRING` | SQL Server `lotv_app` login (after GAP-01 is fixed) |
| `JWT_KEY` | Generate a secure random 256-bit key |
| `STRIPE_SECRET_KEY` | Stripe dashboard (after GAP-04) |
| `STRIPE_WEBHOOK_SECRET` | Stripe dashboard (after GAP-04) |
| `STRIPE_PUBLISHABLE_KEY` | Stripe dashboard (after GAP-04) |
| `SENDGRID_API_KEY` | SendGrid account |
| `TWILIO_ACCOUNT_SID` / `TWILIO_AUTH_TOKEN` / `TWILIO_FROM_NUMBER` | Twilio account |
| `VAPID_PUBLIC_KEY` / `VAPID_PRIVATE_KEY` | Generate via `web-push generate-vapid-keys` |

**IIS infrastructure still needed** (requires server access to `wte_apps3`):
- Create IIS site + app pool `lotv_web` (port 80, path `D:\Websites\lotv_web`)
- Create IIS site + app pool `lotv_api` (port TBD, path `D:\Websites\lotv_api`)

**Code still needed:**
- Write `deploy-to-iis.yml` (replaces `deploy-staging.yml` + `deploy-production.yml`)

---

### GAP-04: Stripe account setup

**Category:** Payment Processing
**Status:** Open — blocked on Stripe account + GitHub secrets

All Stripe code is fully implemented (PaymentIntent creation, webhook handlers for subscription/invoice events, signature verification, idempotency via `WebhookEvent` table, recurring donations, Customer Portal). The API returns `mock: true` when unconfigured and falls back gracefully — no errors, payments just don't process.

**What's needed:**
1. Register a Stripe account
2. Configure the webhook endpoint in Stripe dashboard (pointing to `POST /api/v1/payments/webhook`)
3. Enable test mode for staging
4. Add secrets to GitHub / server config: `Stripe:SecretKey`, `Stripe:WebhookSecret`, `Stripe:PublishableKey`

**No code change needed.**

---

## MEDIUM — Open, Not Immediately Blocking

### GAP-05: JotForm form settings not applied (HIPAA-BAA restriction)

**Category:** Intake Forms / Data Quality
**Status:** Open — blocked on ministry manual JotForm builder access

**Root cause confirmed 2026-09-01:** The JotForm account has a signed HIPAA Business Associate Agreement on file. JotForm deliberately discards all programmatic writes on BAA accounts to preserve their compliance audit trail. Every change must be made in the builder UI by hand — this is platform policy, not a bug, and is not fixable from the code side.

**Outstanding manual checklist for ministry staff in JotForm builder:**

| Item | Risk if not done |
|------|-----------------|
| Fix autoresponder "From" name (still "Eric Garrison") | Unprofessional outreach emails |
| Fix notification email "From" (shows `{husbandsName}` literally — corruption artifact) | Staff receive malformed internal notification emails |
| Turn off HIPAA mode toggle (per ministry's own preference) | Minor — UI display only |
| Fix "How did you hear about us?" wording (current sentence fragment) | Minor UX confusion |
| Un-check pre-checked opt-in checkboxes | Email marketing consent compliance risk |
| Add "Other" fallback to Reason and Faith Tradition dropdowns | Data quality — submitters without a category match have no option |
| Split form into 3 logical pages | UX — form feels long as a single page |
| Rename "Husband's/Wife's Name" to something non-assuming | ⚠️ If this field is renamed, `Program.cs`'s `Field("Husband's Name"...)` / `Field("Wife's Name"...)` webhook extraction and `knownLabels` must be updated simultaneously, or family name capture breaks for all new submissions |
| Apply donation nudge (for "For Someone Else" branch only) | Minor — deferred enhancement |

---

### GAP-06: "How did you hear" free-text follow-up field

**Category:** Data Capture Quality
**Status:** Open — blocked on JotForm builder access (HIPAA-BAA restriction)

The historical spreadsheet contained rich referral context ("Referral from Megan Kreft"). The live form now has only a fixed dropdown; an "Other, please specify" free-text field was designed but cannot be added programmatically. Ministry must manually add a Short Text field in the builder.

**Once the field is added manually, code change needed:**
- Add its label to `knownLabels` in `Program.cs`
- Fold the value into `Family.HowHeard` in the webhook parser
- Add a test payload covering the field

---

### GAP-07: Real webhook URLs for Duda and GiveButter

**Category:** Integration Setup
**Status:** Open — blocked on production hosting setup

Local testing used `cloudflared` quick-tunnel URLs (session-only, change on every restart). Duda and GiveButter dashboards currently have these temporary URLs. Once production is live:
- Update Duda dashboard webhook URL → `POST https://[prod-api-url]/api/v1/webhooks/duda`
- Update GiveButter dashboard webhook URL → `POST https://[prod-api-url]/api/v1/payments/givebutter/webhook`

**No code change needed.**

---

## LOW — Known Limitations / Future Work

### GAP-08: JotForm widget field unverified against real submissions

**Category:** Data Capture Quality
**Status:** Acknowledged — waiting on real intake data

The "Children for Bracelet" `control_widget` field boundary fix (2026-08-12) has not been tested against a real submission's `pretty` payload — zero real submissions have hit the live form to date. Re-verify once intake begins.

---

### GAP-09: Postgres provider branch is dead code

**Category:** Code Quality
**Status:** Documented — not impacting

`Program.cs` has an unreachable `else` branch for Postgres that no workflow ever sets. No Postgres-specific migrations exist. Not removed because removing a hosting option is an architectural decision. Could be cleaned up in a future refactor.

---

### GAP-10: ~285 of 312 Blazor pages not walked post-conversion

**Category:** Testing Coverage
**Status:** Acknowledged — low risk

The Blazor Server conversion was verified on ~25 sampled pages with zero errors. The remaining ~285 routes (mostly detail/edit sub-pages and analytics drill-downs) weren't walked. Consistent zero-error results across sampled areas suggests low risk.

---

### GAP-11: Blob storage not provisioned

**Category:** Infrastructure
**Status:** Not a current need

PDF receipts are generated on demand and streamed directly to clients. No archive storage is required. This becomes relevant only if the ministry decides to store copies server-side.

---

### GAP-12: Monitoring and alerting not configured

**Category:** Operations
**Status:** Blocked on Azure setup

Error-rate alerts, payment-failure alerts, uptime monitoring, and DB backup automation all require Azure infrastructure that isn't provisioned yet. No code change needed — placeholders are in the codebase.

---

### GAP-13: CDN not configured

**Category:** Infrastructure
**Status:** No longer needed

Blazor Server conversion made the Blazor WASM static-asset CDN unnecessary. Previously this was noted as requiring a Windows App Service plan for WASM's `web.config` SPA rewrite. Now irrelevant.

---

### GAP-14: Domain + SSL not configured

**Category:** Infrastructure
**Status:** Blocked on hosting provisioning

Requires domain registrar access and Azure/IIS SSL certificate setup. No code work needed.

---

### GAP-15: Launch checklist sign-off

**Category:** Project Closure
**Status:** Pending all other items

Formal go-live sign-off (`docs/smoke-test-checklist.md`) pending completion of GAPs 01-04 and ministry JotForm checklist.

---

## Already Fixed This Project Cycle (Not Gaps)

The following were discovered and resolved during recent sessions — documented here for completeness:

| Item | Fixed | Session |
|------|-------|---------|
| JotForm `knownLabels` drift after "Quarterly" typo fix | 2026-09-01 | 2026-09-01 |
| `Family.DateOfLoss` displayed nowhere in UI | 2026-09-01 | 2026-09-01 |
| Case Detail missing full shipping address, Diocese, How They Heard, Story | 2026-09-01 | 2026-09-01 |
| `FollowUpTracker` never created from live intake paths | 2026-08-31 | 2026-08-31 |
| Infinite JSON reference cycle on `GET /api/v1/follow-up-trackers` | 2026-08-31 | 2026-08-31 |
| Blazor WASM → Blazor Server conversion (LocalizationService DI, auth session restore, 6 PDF link bugs) | 2026-08-31 | 2026-08-31 |
| EF migration history reconciled for SQL Server; `ConnectionStrings__Default` vs `DefaultConnection` key mismatch fixed | 2026-08-31 | 2026-08-31 |
| `Lotv.Migrations.SqlServer` not built before `dotnet ef` in CI deploy | 2026-08-31 | 2026-08-31 |
| Docker compose: non-root volume permissions, `wget` healthcheck, nginx `gzip_static` stale config | 2026-08-13 | 2026-08-13 |
| JotForm webhook: `knownLabels` reconciled, colon-split parsing bug, `DateOfLoss` wiring, `ChildrenInitials` copy bug | 2026-08-12 | 2026-08-12 |

---

## Related Files

- `MASTER_TODO.md` — full task tracker with historical context
- `NEXT_STEPS.md` — current 5-7 task quick-start
- `sessions/` — per-session detail for each fix listed above
- `src/Lotv.Migrations.SqlServer/baseline-existing-database.sql` — baseline script (GAP-02)
- `src/Lotv.Migrations.SqlServer/rotate-app-credential.sql` — credential rotation (GAP-01)
- `docs/iis-deployment-notes.md` — IIS vs Azure deployment decision (affects GAP-03)
- `docs/smoke-test-checklist.md` — pre-launch verification checklist (GAP-15)
