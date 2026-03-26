# LOTV Auto-Assignment Algorithm
**Version**: 1.0
**Status**: Approved — Phase 1
**Last Updated**: 2026-03-25

---

## Overview

When a new PackageRequest is submitted (either via public intake or staff entry), the system automatically scores all available volunteers in the same chapter and assigns the highest-scoring candidate. If no volunteer meets the minimum threshold, the case is routed to the unassigned queue for manual dispatch.

---

## Trigger

Auto-assignment fires **immediately on request creation** for all requests submitted with:
- Status = `New`
- No `AssignedToId` already set (manual override skips auto-assignment)

For requests created by staff with an explicit `assignedToId`, the auto-assignment step is skipped entirely.

---

## Candidate Pool

Before scoring, the candidate pool is filtered to volunteers who:
1. Belong to the same `ChapterId` as the request
2. Have `Status = Active`
3. Have `Role` in `{ PackageAssembler, Admin }` — roles that can fulfill package requests
4. Have `ActiveCases < 6` (configurable max capacity; set in chapter settings)
5. Are **not** currently in a pending-acceptance window on another case assigned in the last 2 hours

If the request `Priority = Urgent`, the capacity filter in step 4 is relaxed to `< 8`.

---

## Scoring Formula

Each candidate receives a **CompositeScore** on a 0–100 scale:

```
CompositeScore = (ProximityScore × 0.45)
              + (WorkloadScore  × 0.35)
              + (LoyaltyScore   × 0.20)
```

### Component 1 — ProximityScore (0–100)

Measures geographic proximity between the volunteer and the family using the **Haversine formula**.

```
distanceMiles = Haversine(family.Latitude, family.Longitude,
                          volunteer.Latitude, volunteer.Longitude)

ProximityScore = max(0, 100 - (distanceMiles / volunteer.ServiceRadiusMiles) × 100)
```

If distance > `volunteer.ServiceRadiusMiles`, ProximityScore = 0 and the volunteer is **excluded from the pool entirely** (hard boundary).

If either the family or volunteer has no geocoded coordinates, ProximityScore defaults to 50 (neutral).

### Component 2 — WorkloadScore (0–100)

Rewards volunteers with more available capacity.

```
capacityUsed = volunteer.ActiveCases / maxCapacity   (e.g., 3/6 = 0.50)
WorkloadScore = (1 - capacityUsed) × 100            (e.g., 50.0)
```

Overdue cases count double in the `ActiveCases` numerator when calculating WorkloadScore, to deprioritize overloaded volunteers:

```
adjustedActive = volunteer.ActiveCases + volunteer.OverdueCases
WorkloadScore  = max(0, (1 - adjustedActive / maxCapacity) × 100)
```

### Component 3 — LoyaltyScore (0–100)

Rewards experienced, reliable volunteers.

```
LoyaltyScore = min(100, volunteer.TotalCasesFulfilled × 4)
```

This means a volunteer who has fulfilled 25+ cases achieves the maximum loyalty score. New volunteers (0 fulfilled) start at 0, which is offset by their available capacity and proximity.

---

## Scoring Example

| Volunteer | Distance | ActiveCases | Overdue | Fulfilled | ProxScore | WorkScore | LoyalScore | Composite |
|---|---|---|---|---|---|---|---|---|
| Alice | 8 mi | 1 | 0 | 18 | 87 | 83 | 72 | 81.3 |
| Bob | 3 mi | 4 | 1 | 5 | 95 | 17 | 20 | 53.7 |
| Carol | 12 mi | 0 | 0 | 32 | 80 | 100 | 100 | 90.0 ✓ |
| Dave | 40 mi (>radius) | 2 | 0 | 10 | excluded | — | — | — |

Carol is assigned (highest composite). Dave is excluded (beyond service radius).

---

## Assignment Execution

On selection of the top candidate:

1. Set `PackageRequest.AssignedToId = volunteer.Id`
2. Set `PackageRequest.AssignedTo = volunteer.FullName`
3. Set `PackageRequest.AssignedAt = UtcNow`
4. Set `PackageRequest.Status = InProgress`
5. Increment `volunteer.ActiveCases`
6. Create a `RequestAssignment` record: `{ RequestId, AssignedToId, AssignedById = "system", AssignedAt, Status = Pending }`
7. Create a `RequestActivity` record: `{ ActivityType = Assigned, NewValue = volunteer.FullName }`
8. Log an audit entry
9. Send notification to volunteer (email + optional SMS) via `INotificationService`

---

## Acceptance Window

After auto-assignment, the volunteer has **24 hours** to explicitly accept or decline.

- **Accept** (`POST /api/v1/cases/{id}/accept`): `RequestAssignment.Status = Accepted`; case proceeds
- **Decline** (`POST /api/v1/cases/{id}/decline`): triggers reassignment (see below)
- **Timeout** (no response in 24 hours): system auto-escalates to reassignment queue

The acceptance window is configurable per chapter (default 24 hours). For `Priority = Urgent`, window is 4 hours.

---

## Fallback & Reassignment

If the top candidate declines or times out:

1. The declined volunteer is excluded from the scoring pool for this request
2. The algorithm re-runs against the remaining pool
3. If a new candidate is found: assign and start a new 24-hour window
4. If **no candidates remain** above the minimum threshold (CompositeScore ≥ 20):
   - Case status returns to `New`
   - `AssignedTo = null`
   - Case is added to the **Unassigned Queue** visible on the Workload page
   - Staff are notified via the overdue/unassigned daily digest
   - Staff can manually assign from the queue

Maximum automatic reassignment attempts: **3**. After 3 failures, the case is always escalated to staff.

---

## Manual Override

Staff can always bypass auto-assignment:
- On case creation: pass `assignedToId` explicitly → algorithm skipped
- Post-creation: use `PUT /api/v1/cases/{id}/assign` → overrides any existing assignment; no scoring run

---

## Geocoding

Volunteer and family coordinates are populated via a background geocoding job:
- On record creation, if `Latitude/Longitude` are null, address is queued for geocoding
- Geocoding runs via a background `IHostedService` using the configured mapping provider (see ADR-002 equivalent note: OpenStreetMap Nominatim for dev; Mapbox/Google Maps for production)
- Until geocoded, ProximityScore defaults to 50 (neutral) so the volunteer remains in pool

---

## Configuration (per chapter, stored in Chapter settings)

| Setting | Default | Description |
|---|---|---|
| `MaxVolunteerCapacity` | 6 | Max active cases before volunteer excluded from pool |
| `UrgentMaxCapacity` | 8 | Capacity limit for Urgent priority requests |
| `AcceptanceWindowHours` | 24 | Hours for volunteer to accept before auto-escalation |
| `UrgentAcceptanceWindowHours` | 4 | Acceptance window for Urgent requests |
| `MaxReassignmentAttempts` | 3 | Auto-reassignment retries before escalating to staff |
| `MinimumCompositeScore` | 20 | Floor score; candidates below this are skipped |
| `ProximityWeight` | 0.45 | Weighting for proximity in composite formula |
| `WorkloadWeight` | 0.35 | Weighting for workload in composite formula |
| `LoyaltyWeight` | 0.20 | Weighting for loyalty in composite formula |

---

## API Exposure

Staff can preview the scoring results without triggering assignment:
```
GET /api/v1/volunteers/score?requestId=42
```
Returns `VolunteerScoreResult[]`:
```json
[
  {
    "volunteerId": 7,
    "name": "Carol Williams",
    "distanceMiles": 12.3,
    "activeCases": 0,
    "totalFulfilled": 32,
    "proximityScore": 80,
    "workloadScore": 100,
    "loyaltyScore": 100,
    "compositeScore": 90.0,
    "isRecommended": true,
    "exclusionReason": null
  },
  ...
]
```
