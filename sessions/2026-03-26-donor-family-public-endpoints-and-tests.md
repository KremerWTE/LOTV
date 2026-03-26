# Session Notes — 2026-03-26 (continued)
## Donor/Family Public Endpoints, PublicApiTests Suite

### What Was Done

#### 1. DonorImpact and MyRequests — Final Public Page Safety Fix

Two remaining public-facing pages were still calling staff-only endpoints.

**New endpoints added to `/api/public/v1/`:**

| Endpoint | Returns |
|---|---|
| `GET /api/public/v1/donors/{id}/impact` | Donor giving history, calculated impact stats, category breakdown |
| `GET /api/public/v1/families/{id}/requests` | Family's service requests with enums serialized as strings |

Both are `AllowAnonymous`.

**`/donors/{id}/impact` details:**
- Returns 404 if donor has no donations on record
- `TotalGiven`, `GiftCount`, `FamiliesHelped` (approx `giftCount * 2`), `ChaptersServed` (distinct chapter count)
- `CategoryBreakdown` — uses real `FundAllocation` records when available; falls back to the standard LOTV split (45/20/15/12/8%) computed from `TotalGiven`
- Category extraction from `AllocatedTo` text (splits on `" — "`, `" - "`, `" ("`, `":"`)
- `DonationHistory` — ordered most-recent first, Channel and Status as strings

**`/families/{id}/requests` details:**
- Returns 404 if family doesn't exist
- All `RequestCategory`, `CaseStatus`, `RequestPriority`, `PackageReason` values serialized as strings (not integers)

**Pages migrated:**
- `DonorImpact.razor` — added `[Parameter, SupplyParameterFromQuery] int? DonorId`; calls `GetPublicDonorImpactAsync`; category breakdown iterates `DonorImpactCategoryDto` (no more tuples); donation history iterates `DonorDonationDto`
- `MyRequests.razor` — added `[Parameter, SupplyParameterFromQuery] int? FamilyId`; calls `GetPublicFamilyRequestsAsync`; all `CaseStatus`/`RequestPriority` enum comparisons replaced with string pattern matching (`r.Status is "New" or "InProgress" ...`); `GetStatusBadge`/`GetSteps` now take `string` instead of `CaseStatus`

**New DTOs added to ApiService.cs:**
- `PublicDonorImpactDto` — maps `/api/public/v1/donors/{id}/impact`
- `DonorImpactCategoryDto` — category, amount, percentage
- `DonorDonationDto` — date, amount, channel, status (all strings from API)
- `PublicFamilyRequestDto` — id, category, status, priority, createdAt, dueDate, reason, assignedTo

---

#### 2. PublicApiTests Integration Test Suite (35 tests)

New file: `tests/Lotv.Tests/Integration/PublicApiTests.cs`

**Anonymous public endpoint smoke tests:**
- Theory: all 6 known public GET endpoints return 200 with no auth token
- `PublicImpact_Returns_ExpectedShape` — verifies 6 required JSON fields
- `PublicTransparencyMoney/Timeline/Events/WishList_Returns_Array` — shape checks

**Donor impact end-to-end:**
- `DonorImpact_UnknownDonor_Returns404`
- `DonorImpact_DonorWithDonation_Returns200_WithCorrectTotals` — creates donor + 2 donations ($150 + $75) via authed API, fetches impact anonymously, asserts `totalGiven == 225.00` and `giftCount == 2`
- `DonorImpact_CategoryBreakdown_IsNonEmpty` — verifies fallback breakdown array is non-empty

**Family requests end-to-end:**
- `FamilyRequests_UnknownFamily_Returns404`
- `FamilyRequests_ExistingFamily_Returns200_Array` — creates family, verifies empty array (not 404)
- `FamilyRequests_AfterSubmittingRequest_ReturnsItWithStringFields` — verifies `status` is JSON string `"New"`, not integer

**Dashboard endpoint coverage:**
- Theory: all 6 dashboard endpoints return 401 with no token
- Theory: all 6 dashboard endpoints return 200 with ChapterStaff token
- `DashboardByAmount_Returns_SixBands` — asserts exactly 6 elements with required fields
- `DashboardTimeline_DefaultWindow_Returns12Periods` — asserts 12 elements with `period`, `donations`, `requestsFulfilled`, `newRequests`
- `DashboardTimeline_CustomWindow_ReturnsMatchingCount` — `?months=6` → 6 elements
- `DashboardByCity/Resources/Money_Returns_Array` — shape checks

---

#### 3. EF Core GroupBy Translation Bug Fixes

Tests caught two real bugs in the dashboard endpoints that were never hit at runtime (no existing tests):

**`by-city` and `by-diocese`** — both originally used `Include(d => d.Donor)` + `GroupBy(d => d.Donor.City)`. EF Core cannot translate `Distinct().Count()` inside a GroupBy projection to SQL.

**Fix:** Replaced with a two-step approach:
1. SQL: `JOIN Donors` → project flat DTO → `.ToListAsync()`
2. In-memory: `GroupBy` + `Distinct().Count()`

This eliminates the EF Core translation limit while preserving the same correct results.

---

### Test Results
- 423/423 unit + integration tests passing (up from 388)
- 0 build warnings, 0 errors

### Commits This Session
1. `fix: DonorImpact and MyRequests use public anonymous API endpoints`
2. `test: add PublicApiTests — 35 tests for public endpoints and dashboard API`
3. (MASTER_TODO + this session note)
