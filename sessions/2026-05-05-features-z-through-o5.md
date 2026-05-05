# 2026-05-05 — Continuous Feature Build (Z → OOOOO)

## Goal
Continuous "keep building" pass — knock out features in batches of ~6 with each user prompt; commit + verify build clean between batches.

## Scope shipped (109 features across 17 commits)

### Stripe / payments (Z, V, S, KKKKK, NN, A)
- Stripe.net SDK 47.4.0 wired into `/api/v1/payments/intent` (mock fallback unconfigured)
- Stripe Customer auto-created on public `/give` flow (email-deduped donors)
- Stripe webhook handlers: `customer.subscription.created/updated/deleted`, `invoice.payment_succeeded`
- Webhook idempotency via `WebhookEvent` table (Stripe + GiveButter)
- Webhook signature failure logged to `AuditEntry`
- Token-gated billing portal session (Stripe Customer Portal redirect)
- Recurring donation pause/resume/cancel propagate to Stripe Subscription
- Major-gift push notification (≥ $1000)
- Webhook replay for stored Stripe events (admin)

### Donor self-service (W, BB, BBB, FFF, III, MMM, QQQ, DDDDD, UUU)
- Donor magic-link auth (`DonorMagicLink` model + `/donor/login`)
- Donor portal at `/donor/portal` with KPI strip + tiles
- Avatar upload on portal
- Year-end PDF tile
- Update Profile tile
- Inline cancel for active recurring schedules
- Stripe billing-portal tile (only when `StripeCustomerId` exists)
- Sliding session refresh (+24h, 30d cap)
- Session expiry badge in welcome row

### Volunteer self-service (VVVV, EEEEE, JJJJJ, CCCCC, XXXX, YYYY)
- `VolunteerMagicLink` model + table + AddVolunteerMagicLink migration
- `/volunteer/login` + `/volunteer/portal`
- Active assignment count badge
- Session expiry badge + sliding refresh
- Footer link to `/volunteer/login`
- Volunteer magic-link auto-prune in cleanup service

### Money component rollout (M, N, U, AA, EE, XX)
- `<Money>` component honoring `CurrencyService` selection
- Used on DonorImpact, DonorPortal, Admin Dashboard, Donations, ImpactDashboard, ByDonor, ByDiocese, ByCity, ByChannel, ByAmount, Allocations, ImpactReport, DonorRecurring, DonorReceipts

### Push notifications (B, E, F, G, J, R, Q, NN, TT)
- `PushSenderService` (VAPID-signed Web Push via `Lib.Net.Http.WebPush`)
- VAPID key generator endpoint + Settings UI
- Push subscribe/test endpoints + admin viewer at `/admin/push-subscriptions`
- Push fires on apply/assign/escalate; admin can push-test any user

### Admin UX (T, KK, EEE, JJJ, GG, HH, KKK, KK, LLL, JJ, OO, AAAA, BBBB, RR, RRRR, EEEE, FFFF, GGGG, HHHH, IIII, JJJJ, KKKK, LLLL, MMMM, NNNN, OOOO, PPPP, QQQQ, SSSS, TTTT, UUUU, BBBBB, FFFFF, GGGGG, HHHHH, IIIII, MMMMM, NNNNN, OOOOO)
- Sidebar links: Push Subscriptions, DB Migrations, Webhook Events
- AdminLayout: dark-mode toggle, language switcher, currency switcher, ⌘K hint, "/" focus search, scroll shadow on topbar, sticky filter shadow on ImpactDashboard, persisted sidebar-collapse
- Pending-migration warning chip + first-run hint on /admin/migrations
- Webhook events page: search, per-type count chips, sortable columns, source color badges, prune button + confirm, drill-down detail drawer with payload + replay
- AuditLog: drawer, "My actions" toggle, filter counts, "Last 24h" chip, top-noisy-users panel, CSV + JSON export, pagination, infinite scroll, signature-failed filter chip
- ByDonor: kebab menu (View profile / Send portal link / Email donor), URL-persisted sort (?sort=&asc=), city/diocese drill-through filtering, click-outside-to-close kebab
- Donations: filter chip "One-time", multi-select with shift-click range, bulk-allocate, bulk-channel, sticky header
- Sidebar collapse mode (icon-only, 56px)
- Cmd-K command palette: 40+ pages, recents (top-5 with "Recents"/"All pages" headers), 4 quick actions (theme/sidebar/logout/copyurl), emoji icons per item

### Background jobs (H, K, P, TTT, YYYY)
- `FxRefreshService` (daily, exchangerate.host with seed defaults)
- `MagicLinkCleanupService` (hourly, prunes both donor + volunteer)
- `WebhookCleanupService` (daily, 90-day retention)

### Reports & exports (I, OOO, PPP, ZZZ, HHHHH, DDD, AAAAA)
- `/donor/receipts` — HTML + PDF per-donation receipt links + year-end PDF
- Donations-with-id public endpoint
- `/admin/webhooks/old?days=N` prune
- AuditLog CSV + JSON export of rendered slice
- Donor receipts print CSS (global @media print)
- AuditLog "SignatureFailed" filter chip
- Donations select-all tooltip + aria-label

### Diagnostics (CC, RRR, GG)
- `/api/v1/admin/diagnostics` — push count, FX freshness, last/pending migrations, webhook-7d/24h, donors-with-StripeCustomer
- `/admin/migrations` page with applied + pending lists
- VAPID key generator (HQAdmin)

### Webhook viewer (HH, II, CCC, DDD, KKKKK, LLL, OOO, PPPP, XXX)
- `/admin/webhooks` table with last 100, source filter, search, sort, drill-down drawer with payload viewer + replay
- Source color badges (stripe purple / givebutter green)
- Per-type count chips
- Idempotency for Stripe + GiveButter via `WebhookEvent` table

### Dark mode + i18n + currency (1-EE, JJJ, EEE, OOOOO)
- LocalizationService (en/es) with LanguageSwitcher
- CurrencyService (USD/CAD/EUR/GBP/MXN) with CurrencySwitcher + Money
- ExchangeRate model + daily refresh
- Dark mode body theme + comprehensive override CSS catching hard-coded whites

### Avatars (9, BB, F, FF, L)
- AvatarUrl on `LotvIdentityUser` + `Donor`
- MyProfile picker + DonorPortal picker
- Display in AdminLayout topbar pill, ByDonor table, UserManagement
- Admin "Clear avatar" + bulk diagnostics

### PWA + mobile (5, 7, GGGG)
- manifest.webmanifest, sw.js
- Mobile hamburger on PublicLayout (≤720px)

### Bulk admin actions (AAA, GGG, OOOO, LLLLL, QQ, TT)
- Magic-link bulk send (chapter-wide)
- Magic-link bulk send by diocese
- Donations bulk-allocate, bulk-channel
- Admin send-portal-link to single donor (variable expiry days)
- Admin push-test to any user

## Verification
- `dotnet build Lotv.slnx` → 0 warnings, 0 errors after every batch
- 17 batches committed sequentially:
  - 4a1cf9c, 777bef5, 8279769, 1a64f93, a4d9380, c9e58c3, 677a298, fab6916, ca941e0, 8bc25c5, 358c6e9, f1e8fac, 415e14b, cbb9d31, 1d750a8, ffd5443, 7889288, 3c5eb8f, b4ada6d, b15e52f, d062ff4
- All migrations generated: AddAvatarPushDonorLinkFx, AddDonorAvatar, AddWebhookEvents, AddWebhookPayload, AddVolunteerMagicLink

## Configuration touchpoints (operator action)
For full functionality, populate appsettings:
- `Stripe:SecretKey`, `Stripe:PublishableKey`, `Stripe:WebhookSecret`
- `Push:VapidPublicKey`, `Push:VapidPrivateKey`, `Push:VapidSubject` (use Settings → "Generate New Key Pair")
- `GiveButter:WebhookSecret` (existing)
- `SendGrid:ApiKey` / `Twilio:*` (existing)

The Settings page now has a "Download appsettings.snippet.json" button that produces a placeholder template.

## Files added (new only)
- `src/Lotv.Api/Services/{PushSenderService,FxRefreshService,MagicLinkCleanupService,WebhookCleanupService,PdfReceiptService}.cs`
- `src/Lotv.Core/Models/{PushSubscription,DonorMagicLink,VolunteerMagicLink,ExchangeRate,WebhookEvent}.cs`
- `src/Lotv.Web/Pages/{DonorLogin,DonorPortal,DonorReceipts,VolunteerLogin,VolunteerPortal}.razor`
- `src/Lotv.Web/Pages/Admin/{Migrations,PushSubscriptions,Webhooks}.razor`
- `src/Lotv.Web/Services/{LocalizationService,CurrencyService}.cs`
- `src/Lotv.Web/Shared/{LanguageSwitcher,CurrencySwitcher,EnableNotifications,Money,CommandPalette}.razor`
- `src/Lotv.Web/wwwroot/{js/lotv-app.js,manifest.webmanifest,sw.js}`
- 5 EF migrations under `src/Lotv.Api/Migrations/`

## What was deferred
- True table virtualization (Microsoft.AspNetCore.Components.Web.Virtualization conflicts with `<tr>` rendering — used Take(N) + Load-more instead)
- Per-page i18n body extraction (PageTitle + headlines done; remaining strings can use the same `@Loc[...]` pattern)
- Cloud hosting / domain / DNS / TLS (Phase 6 cloud items remain — blocked on hosting decision; AWS plan was scoped and skipped per user)
