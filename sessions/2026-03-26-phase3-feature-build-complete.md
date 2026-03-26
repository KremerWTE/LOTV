# Session Notes — 2026-03-26
## Phase 3 Feature Build Complete + Mock Seed Data

### Summary
Completed all remaining codeable Phase 3 backlog items across two sessions, then
consolidated and labeled all mock/seed data. All 388 tests passing at end of session.

---

### What Was Built

#### New Core Models
| Model | Key Fields / Notes |
|---|---|
| `RecurringDonation` | Amount, Frequency (enum + NextFrom() extension), NextChargeDate, Status, StripeSubscriptionId, EndsOn |
| `DonorPledge` | PledgedAmount, FulfilledAmount, TargetDate, Status; computed RemainingAmount, FulfillmentPercent, IsOverdue, IsFulfilled |
| `WishListItem` | `int? FamilyId` (null = chapter-wide), WishListCategory/WishListStatus enums, QuantityRequested/Fulfilled |
| `SmsLog` | Immutable audit: ToPhoneNumber, MessageType, CaseId, ProviderMessageId, Success |
| `ApiKey` | KeyHash (SHA-256 hex), PartnerName, ChapterId (nullable = all chapters), ApiKeyScope enum, IsValid computed |

#### New Services
- **`ISmsService` / `SmsService`** — Twilio REST API via plain `HttpClient` (no extra NuGet); reads `Twilio:AccountSid/AuthToken/FromNumber` from config; dev no-op when not configured (`SmsResult(true, "DEV_NOOP", null)`); always persists `SmsLog` to DB; message templates: assignment notification, overdue reminder, check-in confirmation, accepted.
- **`DevSeedData.SeedAsync`** — idempotent (checks `Chapters.Any()`), calls `EnsureCreatedAsync` first; Development-only; skipped in tests via `Testing:SkipSeed=true` config flag.

#### New API Endpoints (Program.cs)
- `POST /api/v1/requests/{id}/checkin` — volunteer SMS check-in
- `GET /api/v1/requests/sms-log` — staff SMS log viewer
- `GET/POST /api/v1/wishlist`, `GET /wishlist/open`, `GET /wishlist/{id}`, `POST /wishlist/{id}/fulfill`, `DELETE /wishlist/{id}`
- `GET/POST/DELETE /api/v1/apikeys` — HQAdmin API key management (raw key returned once on create)
- Public API at `/api/public/v1` (X-Api-Key header auth, SHA-256 lookup):
  - `GET /impact`, `GET /chapters`, `GET /wishlist` — anonymous
  - `POST /requests` — Write scope key (creates Family + PackageRequest)
  - `POST /donations` — Write scope key (upserts Donor + creates Donation)

#### New Blazor Pages
| Route | File | Notes |
|---|---|---|
| `/onboarding/volunteer` | `OnboardingVolunteer.razor` | 5-step wizard: profile → availability/skills → chapter → role explanation → completion |
| `/onboarding/staff` | `OnboardingStaff.razor` | 5-step wizard: profile → chapter → role capabilities → first-action picker → completion |
| `/donor/recurring` | `DonorRecurring.razor` | Active schedules, pause/resume/cancel/edit, new recurring gift modal |
| `/admin/wishlist` | `WishList.razor` | Card grid, status badges, fulfill drawer, add/cancel item |
| `/wish-list` | `WishListPublic.razor` | Public donor view, category filter chips, "I'll Donate This" pledge modal |
| `/help` | `Help.razor` | 25 FAQ items, live search + category filter, accordion |
| `/admin/reconciliation` | `PaymentReconciliation.razor` | Period selector, KPI strip, filterable results table, CSV export stub |
| `/admin/volunteer-schedule` | `VolunteerSchedule.razor` | Week / Month / List views, prev/next/today navigation |
| `/admin/sponsorships` | `Sponsorships.razor` | KPI strip, tier badges, detail drawer, Add Sponsor modal |

#### New Shared Components
- `BarChart.razor` — `@typeparam TItem`, horizontal/vertical orientations, CSS bars
- `PieChart.razor` — SVG donut/pie, arc math, legend with percentages
- `LineChart.razor` — SVG line/area, dual-series, grid lines, fluid width
- **Blazor SVG note**: `<text>` is a reserved Razor token; all SVG text elements must use `@((MarkupString)$"<text ...>...</text>")`.

#### Mock Seed Data (`src/Lotv.Api/Data/SeedData.cs`)
All data is entirely fictitious — names, emails, phone numbers, addresses, and financials are
for demo/dev use only. Do not load in production.

| Entity | Count | Notes |
|---|---|---|
| Chapters | 3 | Chicago Metro, Milwaukee, Twin Cities |
| Dioceses | 3 | Archdiocese of Chicago, Diocese of Milwaukee, Archdiocese of St. Paul |
| Parishes | 6 | 3 per Chicago chapter, 2 Milwaukee, 1 Twin Cities |
| Families | 10 | Varied loss reasons, statuses, faith traditions |
| Volunteers | 10 | Varied roles (PackageAssembler, Driver, PrayerAmbassador, etc.) and statuses |
| Donors | 8 | All 4 tiers (Friend/Supporter/Champion/Benefactor), some recurring |
| Package Requests | 12 | All statuses represented including urgent cases and multi-request families |
| Donations | 13 | All channels (Online/Check/Cash/Gala/SilentAuction/CorporateMatch) |
| Fund Allocations | 6 | Mix of Allocated and PendingReview |
| Expenses | 10 | Supplies, Shipping, Events, Printing, Staffing categories |
| Ministry Events | 5 | Gala, Prayer Night, Dinner, Knitting Circle, Walkathon (Draft/Open/Completed) |
| Resource Items | 10 | All ResourceCategory values; 3 chapters have inventory |
| Wish List Items | 8 | Mix of Open/PartiallyFulfilled/Fulfilled; some chapter-wide (null FamilyId) |
| Recurring Donations | 5 | Active/Paused; Monthly/Quarterly frequencies |
| Donor Pledges | 4 | Active/Fulfilled/Overdue; with notes |

---

### Bugs Fixed This Session
1. **CS0117** (`Family.LastName`, `PackageRequest.Notes`) in public API endpoint — corrected to `Parent1LastName` and `InternalNotes`.
2. **RZ1023** SVG `<text>` tag in PieChart/LineChart — wrapped in `MarkupString` cast.
3. **CS8602** null dereference in OnboardingStaff — added null-conditional on `FirstOrDefault(...)?.Label`.
4. **Integration test failure** (1/388) after seed introduction — `GetRequest_ById_ReturnsSavedData` got "InProgress" instead of "New" because the seed was running in the test environment and interfering with ID assignment. Fixed by adding `Testing:SkipSeed=true` config flag; set in `LotvApiFactory` config override.
5. **WishListItem.FamilyId** made nullable (`int? → null = chapter-wide wish`) — updated both model and DbContext FK config.
6. **Parish seed data** used non-existent `City`/`State` fields — corrected to use actual Parish model fields (`DioceseName`, `CertificationLevel`, `EnrolledDate`, `Status`).

---

### Build / Test Status
- `dotnet build src/Lotv.Api/Lotv.Api.csproj` — ✅ 0 errors, 0 warnings
- `dotnet test tests/Lotv.Tests/Lotv.Tests.csproj` — ✅ 388/388 passed

---

### Commit
`6a30b94` — `feat: complete Phase 3 feature build — SMS, public API, wish list, onboarding, charts, mock seed data`
202 files changed, 39560 insertions(+), 1144 deletions(-)
Pushed to: `origin/kremer-dev`

---

### MASTER_TODO Items Marked Complete This Session
- Recurring donations (UI + model + endpoints)
- Wish list / in-kind donation requests
- SMS check-in for volunteers on active requests
- Public API for third-party integrations (partner organizations)
- Mock / seed data (clearly labeled, Development-only)

### Remaining Open Items (MASTER_TODO)
- Performance profiling of dashboard aggregate queries
- Localization / i18n
- Online bidding for silent auction (SignalR live bidding — separate from ops board)
- Event QR code check-in
- Stripe subscription wiring for recurring donations (credentials required)
