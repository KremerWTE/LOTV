# LOTV REST API Contract
**Version**: v1
**Base URL**: `/api/v1`
**Status**: Approved — Phase 1
**Last Updated**: 2026-03-25

---

## Conventions

### Versioning
All endpoints are prefixed `/api/v1/`. Breaking changes increment to `/api/v2/`.

### Authentication
All endpoints except public intake routes require `Authorization: Bearer <token>`.

### Standard Response Envelope
```json
// Success
{ "data": { ... }, "meta": { "page": 1, "pageSize": 25, "total": 142 } }

// Error
{ "error": { "code": "VALIDATION_FAILED", "message": "...", "fields": { "email": "required" } } }
```

### HTTP Status Codes
| Code | Meaning |
|---|---|
| 200 | OK |
| 201 | Created |
| 204 | No Content (DELETE success) |
| 400 | Bad Request / Validation failed |
| 401 | Unauthenticated |
| 403 | Unauthorized (authenticated but insufficient role) |
| 404 | Not Found |
| 409 | Conflict (e.g., duplicate email) |
| 422 | Unprocessable Entity (business rule violation) |
| 429 | Rate limit exceeded |
| 500 | Internal Server Error |

### Pagination
All list endpoints accept `?page=1&pageSize=25` query parameters. Max pageSize = 100.

### Chapter Scoping
For all non-HQAdmin requests, the `chapterId` claim from the JWT is automatically applied to all queries. HQAdmin may pass `?chapterId=X` to scope to a specific chapter, or omit it to see all.

---

## Authentication Endpoints

### POST /api/v1/auth/login
Login with email + password. Returns tokens.
- **Auth**: None
- **Request**: `{ "email": "string", "password": "string" }`
- **Response 200**: `{ "accessToken": "string", "refreshToken": "string", "expiresIn": 3600, "user": { "id": "string", "name": "string", "email": "string", "role": "string", "chapterId": int|null } }`
- **Response 401**: Invalid credentials

### POST /api/v1/auth/refresh
Exchange a refresh token for a new access token.
- **Auth**: None
- **Request**: `{ "refreshToken": "string" }`
- **Response 200**: `{ "accessToken": "string", "refreshToken": "string", "expiresIn": 3600 }`
- **Response 401**: Token expired or revoked

### POST /api/v1/auth/logout
Revoke the current refresh token.
- **Auth**: Bearer
- **Request**: `{ "refreshToken": "string" }`
- **Response 204**: Token revoked

### POST /api/v1/auth/forgot-password
Trigger password reset email.
- **Auth**: None
- **Request**: `{ "email": "string" }`
- **Response 200**: Always returns 200 (no user enumeration)

### POST /api/v1/auth/reset-password
Complete password reset.
- **Auth**: None
- **Request**: `{ "email": "string", "token": "string", "newPassword": "string" }`
- **Response 204**: Success

---

## Public Intake Endpoints

### POST /api/v1/intake/family
Submit a family comfort package request. Creates a Family record and a PackageRequest.
- **Auth**: None (rate limited: 5/IP/hr)
- **Request**:
```json
{
  "parent1FirstName": "string",
  "parent1LastName": "string",
  "parent2FirstName": "string?",
  "email": "string",
  "phone": "string?",
  "city": "string",
  "state": "string",
  "reason": "PackageReason",
  "faithTradition": "string?",
  "childrenInitials": "string?",
  "story": "string?",
  "parishName": "string?",
  "howHeard": "string?"
}
```
- **Response 201**: `{ "requestId": int, "confirmationMessage": "string" }`

### POST /api/v1/intake/volunteer
Submit a volunteer application. Creates a Volunteer record with status Onboarding.
- **Auth**: None (rate limited: 10/IP/hr)
- **Request**:
```json
{
  "firstName": "string",
  "lastName": "string",
  "email": "string",
  "phone": "string?",
  "city": "string?",
  "state": "string?",
  "role": "VolunteerRole",
  "parishName": "string?",
  "notes": "string?"
}
```
- **Response 201**: `{ "message": "string" }`

### POST /api/v1/intake/donation
Submit a public donation (e.g., from /give page).
- **Auth**: None (rate limited: 20/IP/hr)
- **Request**:
```json
{
  "firstName": "string",
  "lastName": "string",
  "email": "string",
  "amount": "decimal",
  "isRecurring": "bool",
  "parishName": "string?",
  "dedicationMessage": "string?",
  "stripePaymentMethodId": "string"
}
```
- **Response 201**: `{ "donationId": int, "receiptUrl": "string?" }`

---

## Family Endpoints

### GET /api/v1/families
List all families (chapter-scoped).
- **Auth**: ChapterStaff+
- **Query**: `?page=1&pageSize=25&status=Active&reason=Miscarriage&search=smith`
- **Response 200**: `{ "data": [ Family[] ], "meta": { ... } }`

### POST /api/v1/families
Create a new family record (staff intake).
- **Auth**: ChapterStaff+
- **Request**: Family fields (see data model)
- **Response 201**: `{ "data": Family }`

### GET /api/v1/families/{id}
Get a single family by ID.
- **Auth**: ChapterStaff+
- **Response 200**: `{ "data": Family }` | 404

### PUT /api/v1/families/{id}
Update a family record.
- **Auth**: ChapterStaff+
- **Request**: Updatable Family fields (contact info, status, notes)
- **Response 200**: `{ "data": Family }`

---

## Case (PackageRequest) Endpoints

### GET /api/v1/cases
List all cases (chapter-scoped).
- **Auth**: ChapterStaff+
- **Query**: `?status=New&assignedTo=userId&overdue=true&reason=Miscarriage&page=1&pageSize=25`
- **Response 200**: `{ "data": [ Case[] ], "meta": { ... } }`

### POST /api/v1/cases
Create a new case for an existing family or with new family data.
- **Auth**: ChapterStaff+
- **Request**: `{ "familyId": int?, "newFamily": FamilyInput?, "reason": PackageReason, "priority": RequestPriority, "assignedToId": int?, "dueDate": "date?" }`
- **Response 201**: `{ "data": Case }`

### GET /api/v1/cases/{id}
Get case detail with family, notes, activity log.
- **Auth**: ChapterStaff+ | Volunteer (own assigned)
- **Response 200**: `{ "data": CaseDetail }` | 404

### PUT /api/v1/cases/{id}/status
Update case status.
- **Auth**: ChapterStaff+ | Volunteer (limited: InProgress → AwaitingShipment only)
- **Request**: `{ "status": "CaseStatus", "notes": "string?" }`
- **Response 200**: `{ "data": Case }`

### PUT /api/v1/cases/{id}/assign
Assign or reassign a volunteer to a case.
- **Auth**: ChapterStaff+
- **Request**: `{ "volunteerId": int?, "notes": "string?" }` (null = unassign)
- **Response 200**: `{ "data": Case }`

### PUT /api/v1/cases/{id}/priority
Set case priority.
- **Auth**: ChapterStaff+
- **Request**: `{ "priority": "RequestPriority" }`
- **Response 200**: `{ "data": Case }`

### PUT /api/v1/cases/{id}/due-date
Set or clear case due date.
- **Auth**: ChapterStaff+
- **Request**: `{ "dueDate": "date?" }`
- **Response 200**: `{ "data": Case }`

### PUT /api/v1/cases/{id}/shipping
Update tracking / shipping info.
- **Auth**: ChapterStaff+
- **Request**: `{ "trackingNumber": "string?", "shippedDate": "date?" }`
- **Response 200**: `{ "data": Case }`

### POST /api/v1/cases/{id}/accept
Volunteer accepts their assignment.
- **Auth**: Volunteer (own assigned)
- **Response 200**: `{ "data": Case }`

### POST /api/v1/cases/{id}/decline
Volunteer declines; triggers reassignment queue.
- **Auth**: Volunteer (own assigned)
- **Request**: `{ "reason": "string?" }`
- **Response 200**: `{ "data": Case }`

### POST /api/v1/cases/{id}/escalate
Escalate a case to supervisor.
- **Auth**: ChapterStaff+
- **Request**: `{ "reason": "string" }`
- **Response 200**: `{ "data": Case }`

### GET /api/v1/cases/{id}/notes
Get all notes on a case.
- **Auth**: ChapterStaff+ | Volunteer (non-internal notes only)
- **Response 200**: `{ "data": Note[] }`

### POST /api/v1/cases/{id}/notes
Add a note to a case.
- **Auth**: ChapterStaff+ | Volunteer
- **Request**: `{ "content": "string", "isInternal": bool }`
- **Response 201**: `{ "data": Note }`

### GET /api/v1/cases/{id}/activity
Get the immutable activity log for a case.
- **Auth**: ChapterStaff+
- **Response 200**: `{ "data": ActivityEntry[] }`

---

## Volunteer Endpoints

### GET /api/v1/volunteers
List volunteers (chapter-scoped).
- **Auth**: ChapterStaff+
- **Query**: `?status=Active&role=PackageAssembler&search=name`
- **Response 200**: `{ "data": Volunteer[] }`

### POST /api/v1/volunteers
Add a volunteer (staff-created).
- **Auth**: ChapterAdmin+
- **Request**: Volunteer fields
- **Response 201**: `{ "data": Volunteer }`

### GET /api/v1/volunteers/{id}
Get volunteer detail.
- **Auth**: ChapterStaff+ | Volunteer (own profile)
- **Response 200**: `{ "data": Volunteer }` | 404

### PUT /api/v1/volunteers/{id}
Update volunteer record.
- **Auth**: ChapterAdmin+ | Volunteer (limited fields: own profile)
- **Request**: Updatable Volunteer fields
- **Response 200**: `{ "data": Volunteer }`

### GET /api/v1/volunteers/{id}/cases
Get cases assigned to a volunteer.
- **Auth**: ChapterStaff+ | Volunteer (own)
- **Query**: `?status=New&page=1&pageSize=25`
- **Response 200**: `{ "data": Case[] }`

### GET /api/v1/volunteers/workload
Get workload summary for all active volunteers.
- **Auth**: ChapterStaff+
- **Response 200**: `{ "data": [ { "volunteerId": int, "name": "string", "openCases": int, "overdueCases": int, "capacityPct": int } ] }`

### GET /api/v1/volunteers/score?requestId={id}
Run the auto-assignment scoring algorithm for a specific request. Returns scored candidate list.
- **Auth**: ChapterStaff+
- **Response 200**: `{ "data": [ VolunteerScoreResult[] ] }` (see auto-assignment-algorithm.md)

---

## Donor Endpoints

### GET /api/v1/donors
List donors (chapter-scoped).
- **Auth**: ChapterAdmin+
- **Query**: `?tier=Champion&recurring=true&search=name`
- **Response 200**: `{ "data": Donor[] }`

### POST /api/v1/donors
Create a donor record.
- **Auth**: ChapterStaff+
- **Request**: Donor fields
- **Response 201**: `{ "data": Donor }`

### GET /api/v1/donors/{id}
Get donor detail with giving history.
- **Auth**: ChapterAdmin+ | Donor (own)
- **Response 200**: `{ "data": DonorDetail }` | 404

### PUT /api/v1/donors/{id}
Update donor record.
- **Auth**: ChapterAdmin+ | Donor (own profile fields)
- **Request**: Updatable Donor fields
- **Response 200**: `{ "data": Donor }`

### GET /api/v1/donors/{id}/donations
Get giving history for a donor.
- **Auth**: ChapterAdmin+ | Donor (own)
- **Response 200**: `{ "data": Donation[] }`

### GET /api/v1/donors/{id}/receipt/{year}
Generate and return a tax receipt for a donor's giving in a given year.
- **Auth**: ChapterAdmin+ | Donor (own)
- **Response 200**: PDF or `{ "data": { "receiptUrl": "string" } }`

---

## Donation Endpoints

### GET /api/v1/donations
List all donations (chapter-scoped).
- **Auth**: ChapterAdmin+
- **Query**: `?channel=Online&allocationStatus=Unallocated&donorId=int&year=2026&page=1`
- **Response 200**: `{ "data": Donation[] }`

### POST /api/v1/donations
Record a donation (staff entry).
- **Auth**: ChapterStaff+
- **Request**: `{ "donorId": int, "amount": decimal, "date": "date", "channel": DonationChannel, "campaign": "string?", "isRecurring": bool, "notes": "string?" }`
- **Response 201**: `{ "data": Donation }`

### GET /api/v1/donations/{id}
Get a single donation.
- **Auth**: ChapterAdmin+
- **Response 200**: `{ "data": Donation }` | 404

### PUT /api/v1/donations/{id}
Update a donation (e.g., correct channel, add tracking).
- **Auth**: ChapterAdmin+
- **Request**: Updatable Donation fields
- **Response 200**: `{ "data": Donation }`

---

## Fund Allocation Endpoints

### GET /api/v1/allocations
List all fund allocations (chapter-scoped).
- **Auth**: ChapterAdmin+
- **Query**: `?status=PendingReview`
- **Response 200**: `{ "data": FundAllocation[] }`

### POST /api/v1/allocations
Create an allocation record (moves donation to PendingReview).
- **Auth**: ChapterStaff+
- **Request**: `{ "donationId": int, "amount": decimal, "allocatedTo": "string?", "notes": "string?" }`
- **Response 201**: `{ "data": FundAllocation }`

### POST /api/v1/allocations/{id}/approve
Approve a pending allocation.
- **Auth**: ChapterAdmin+
- **Request**: `{ "allocatedTo": "string", "notes": "string?" }`
- **Response 200**: `{ "data": FundAllocation }`

### POST /api/v1/allocations/{id}/reject
Reject a pending allocation (returns to Unallocated).
- **Auth**: ChapterAdmin+
- **Request**: `{ "reason": "string" }`
- **Response 200**: `{ "data": FundAllocation }`

---

## Event Endpoints

### GET /api/v1/events
List events (chapter-scoped).
- **Auth**: ChapterStaff+
- **Query**: `?status=Published&upcoming=true`
- **Response 200**: `{ "data": Event[] }`

### POST /api/v1/events
Create an event.
- **Auth**: ChapterAdmin+
- **Request**: Event fields
- **Response 201**: `{ "data": Event }`

### GET /api/v1/events/{id}
Get event detail with attendee count and auction items.
- **Auth**: ChapterStaff+
- **Response 200**: `{ "data": EventDetail }` | 404

### PUT /api/v1/events/{id}
Update event details.
- **Auth**: ChapterAdmin+
- **Response 200**: `{ "data": Event }`

### PUT /api/v1/events/{id}/status
Change event status (publish, open, close, complete, cancel).
- **Auth**: ChapterAdmin+
- **Request**: `{ "status": "EventStatus" }`
- **Response 200**: `{ "data": Event }`

### GET /api/v1/events/{id}/attendees
List attendees for an event.
- **Auth**: ChapterStaff+
- **Response 200**: `{ "data": Attendee[] }`

### POST /api/v1/events/{id}/attendees
Register an attendee.
- **Auth**: ChapterStaff+
- **Request**: `{ "donorId": int?, "guestName": "string?", "ticketCount": int, "amountPaid": decimal, "channel": DonationChannel }`
- **Response 201**: `{ "data": Attendee }`

### POST /api/v1/events/{id}/attendees/{attendeeId}/checkin
Check in an attendee at the door.
- **Auth**: ChapterStaff+
- **Response 200**: `{ "data": Attendee }`

### GET /api/v1/events/{id}/auction-items
List auction items for an event.
- **Auth**: ChapterStaff+
- **Response 200**: `{ "data": AuctionItem[] }`

### POST /api/v1/events/{id}/auction-items
Add an auction item.
- **Auth**: ChapterAdmin+
- **Request**: AuctionItem fields
- **Response 201**: `{ "data": AuctionItem }`

### POST /api/v1/events/{id}/auction-items/{itemId}/bids
Place a bid on an auction item.
- **Auth**: Donor+ | ChapterStaff (on behalf of bidder)
- **Request**: `{ "bidderId": int, "bidAmount": decimal }`
- **Response 201**: `{ "data": Bid }` | 409 (bid too low)

### POST /api/v1/events/{id}/auction-items/{itemId}/close
Close bidding on an item and set the winner.
- **Auth**: ChapterAdmin+
- **Response 200**: `{ "data": AuctionItem }` (with winner set)

---

## Diocese & Parish Endpoints

### GET /api/v1/dioceses
List all dioceses.
- **Auth**: ChapterStaff+
- **Response 200**: `{ "data": Diocese[] }`

### GET /api/v1/dioceses/{id}
Get diocese detail with parishes.
- **Auth**: ChapterStaff+
- **Response 200**: `{ "data": DioceseDetail }` | 404

### PUT /api/v1/dioceses/{id}
Update diocese record.
- **Auth**: HQAdmin
- **Response 200**: `{ "data": Diocese }`

### GET /api/v1/parishes
List all parishes (optionally filtered by diocese).
- **Auth**: ChapterStaff+
- **Query**: `?dioceseId=int`
- **Response 200**: `{ "data": Parish[] }`

### PUT /api/v1/parishes/{id}
Update parish record.
- **Auth**: ChapterAdmin+
- **Response 200**: `{ "data": Parish }`

---

## Dashboard & Reporting Endpoints

### GET /api/v1/dashboard/stats
Get the dashboard KPI summary.
- **Auth**: ChapterStaff+
- **Response 200**: `{ "data": DashboardStats }` (chapter-scoped or HQ roll-up)

### GET /api/v1/dashboard/overdue
Get all overdue cases.
- **Auth**: ChapterStaff+
- **Response 200**: `{ "data": Case[] }`

### GET /api/v1/reports/impact
Get impact report data (breakdown by reason, status, faith tradition, donations trend).
- **Auth**: ChapterStaff+
- **Query**: `?from=date&to=date`
- **Response 200**: `{ "data": ImpactReport }`

### GET /api/v1/reports/donations/by-channel
Donation breakdown by channel.
- **Auth**: ChapterAdmin+
- **Query**: `?from=date&to=date`
- **Response 200**: `{ "data": [ { "channel": "string", "count": int, "amount": decimal, "pct": decimal } ] }`

### GET /api/v1/reports/donations/by-donor
Donation summary per donor.
- **Auth**: ChapterAdmin+
- **Response 200**: `{ "data": DonorSummaryRow[] }`

### GET /api/v1/reports/donations/by-diocese
Donation and case breakdown per diocese.
- **Auth**: ChapterAdmin+
- **Response 200**: `{ "data": DioceseSummaryRow[] }`

### GET /api/v1/reports/workload
Full volunteer workload report.
- **Auth**: ChapterStaff+
- **Response 200**: `{ "data": WorkloadRow[] }`

### GET /api/v1/reports/hq-summary
HQ cross-chapter roll-up report.
- **Auth**: HQAdmin
- **Response 200**: `{ "data": ChapterSummaryRow[] }`

---

## Export Endpoints

### GET /api/v1/export/families
Export family records as CSV.
- **Auth**: ChapterAdmin+
- **Query**: `?status=Active`
- **Response 200**: `text/csv` file download
- **Audit**: Logged automatically

### GET /api/v1/export/donations
Export donation ledger as CSV.
- **Auth**: ChapterAdmin+
- **Query**: `?year=2026&allocationStatus=Unallocated`
- **Response 200**: `text/csv`

### GET /api/v1/export/donors
Export donor directory as CSV.
- **Auth**: ChapterAdmin+
- **Query**: `?recurring=true`
- **Response 200**: `text/csv`

### GET /api/v1/export/volunteers
Export volunteer roster as CSV.
- **Auth**: ChapterAdmin+
- **Query**: `?status=Active`
- **Response 200**: `text/csv`

### GET /api/v1/export/audit-log
Export full audit log as CSV.
- **Auth**: HQAdmin | ChapterAdmin (own chapter entries only)
- **Response 200**: `text/csv`

---

## Audit Log Endpoints

### GET /api/v1/audit
Get audit log entries (chapter-scoped).
- **Auth**: ChapterAdmin+
- **Query**: `?action=Created&entity=PackageRequest&from=date&to=date&search=text&page=1`
- **Response 200**: `{ "data": AuditEntry[], "meta": { ... } }`

---

## User Management Endpoints

### GET /api/v1/users
List staff users (chapter-scoped).
- **Auth**: ChapterAdmin+
- **Response 200**: `{ "data": StaffUser[] }`

### POST /api/v1/users/invite
Invite a new staff user (sends email with setup link).
- **Auth**: ChapterAdmin+
- **Request**: `{ "email": "string", "name": "string", "role": "UserRole", "chapterId": int? }`
- **Response 201**: `{ "message": "Invite sent" }`

### PUT /api/v1/users/{id}
Update a user's role or active status.
- **Auth**: ChapterAdmin+ (own chapter); HQAdmin (any)
- **Request**: `{ "role": "UserRole?", "isActive": bool? }`
- **Response 200**: `{ "data": StaffUser }`

### GET /api/v1/users/me
Get the current user's profile.
- **Auth**: Any authenticated
- **Response 200**: `{ "data": UserProfile }`

### PUT /api/v1/users/me
Update own profile (name, phone).
- **Auth**: Any authenticated
- **Request**: `{ "firstName": "string", "lastName": "string", "phone": "string?" }`
- **Response 200**: `{ "data": UserProfile }`

---

## Webhook Endpoints

### POST /api/v1/webhooks/stripe
Receive Stripe payment events (charge.succeeded, charge.failed, customer.subscription.updated, etc.).
- **Auth**: Stripe webhook signature header (`Stripe-Signature`)
- **Response 200**: Always 200 to acknowledge receipt; processing is async

---

## Chapter Management Endpoints (HQAdmin only)

### GET /api/v1/chapters
List all chapters.
- **Auth**: HQAdmin
- **Response 200**: `{ "data": Chapter[] }`

### POST /api/v1/chapters
Create a new chapter.
- **Auth**: HQAdmin
- **Request**: Chapter fields
- **Response 201**: `{ "data": Chapter }`

### PUT /api/v1/chapters/{id}
Update chapter details.
- **Auth**: HQAdmin
- **Response 200**: `{ "data": Chapter }`

### PUT /api/v1/chapters/{id}/status
Activate or deactivate a chapter.
- **Auth**: HQAdmin
- **Request**: `{ "isActive": bool }`
- **Response 200**: `{ "data": Chapter }`
