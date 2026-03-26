# MASTER_TODO — Lily of the Valley (LOTV)

**Project**: LOTV SaaS Social Services Coordination Platform
**Stack**: .NET 9 · ASP.NET Core Web API · Blazor WebAssembly · xUnit
**Last Updated**: 2026-03-26 — Phase 4 COMPLETE; Phase 5 COMPLETE — 423 tests passing (0 failures); Phase 6 IN PROGRESS — CI/CD workflows complete, environment config + OWASP review done; E2E Playwright test suite complete; mobile responsiveness + WCAG 2.1 AA accessibility audit complete; QR event check-in, real-time silent auction, all dashboard API endpoints, all public-facing pages now use anonymous endpoints, 35-test PublicApiTests suite added
**Org Model**: Centralized nonprofit — National HQ → Local Chapters (2-tier)

---

## Phase Overview

| Phase | Name | Status |
|---|---|---|
| 0 | Foundation | ✅ COMPLETE |
| 1 | Architecture & Design | ✅ COMPLETE |
| 2 | Core Domain (Lotv.Core) | ✅ COMPLETE |
| 3 | API (Lotv.Api) | ✅ COMPLETE |
| 4 | Frontend (Lotv.Web) | ✅ COMPLETE |
| 5 | Testing | 🔄 IN PROGRESS |
| 6 | Deployment & Launch | 🔄 IN PROGRESS |

### Key Platform Characteristics
- **Org model**: One centralized nonprofit; National HQ → Local Chapters (no middle tier)
- **HQ Admin** sees all chapters' data; **Chapter Staff/Admin** sees only their own chapter
- **Auto-assignment**: system scores and matches volunteers to requests by location proximity + skills — no manual dispatch required
- **Real-time operations board**: SignalR hub broadcasts request state changes to all connected chapter staff
- **Scheduled reports**: daily digest + weekly summary auto-generated and emailed to HQ and chapter leads

---

## Phase 0 — Foundation (CURRENT)

**Goal**: Project scaffold in place, git initialized, tooling confirmed working.

- [x] Create .NET 9 solution skeleton (Lotv.slnx)
- [x] Create Lotv.Api project (ASP.NET Core Web API)
- [x] Create Lotv.Web project (Blazor WebAssembly)
- [x] Create Lotv.Core project (Class Library)
- [x] Create Lotv.Tests project (xUnit)
- [x] Add project references (Api → Core, Web → Core, Tests → Core + Api)
- [x] Create meta scaffold (SESSION_STARTUP_DIRECTIVE, MASTER_TODO, docs/, sessions/)
- [x] Create .claude/settings.local.json
- [x] `dotnet build Lotv.slnx` passes with 0 errors
- [x] `dotnet test Lotv.slnx` passes (default xUnit test)
- [x] Initialize git repository (`git init`)
- [x] Create `.gitignore` for .NET projects
- [x] Initial commit
- [x] Author `MSA-LOTV.md` / `.docx` — Master Services Agreement
- [x] Author `SOW-LOTV-001-FullPlatform.md` / `.docx` — full-platform Statement of Work
- [x] Author `docs/LOTV-PM-Plan.md` — full Project Management Plan (19 sections: WBS, risk register, communication plan, change management, quality plan, dependency map, milestones, client deliverables tracker)
- [x] Author `docs/LOTV-Project-Plan.html` — self-contained HTML project site (tech stack, architecture, phase roadmap, domain model, 60+ API endpoints, role-based frontend views, milestones, client deliverables)

---

## Phase 1 — Architecture & Design

**Goal**: All major technical decisions locked in writing before implementation begins.

### Domain Decisions
- [x] Define domain model (entities for all 5 user types + request lifecycle + resource/money flows)
- [x] Define service request lifecycle states (Submitted → Triaged → Matched → In Progress → Fulfilled / Closed / Escalated)
- [x] Define escalation rules (e.g., auto-escalate if no volunteer accepts within X hours, or if request passes due date)
- [x] Define volunteer assignment workflow (staff assigns → volunteer notified → volunteer accepts or declines → if declined, back to unassigned queue or reassigned)
- [x] Define resource donation lifecycle (Received → Allocated → Delivered)
- [x] Define monetary contribution lifecycle (Received → Processed → Allocated → Disbursed)
- [x] Define diocese data model (how dioceses are managed — seed list vs. admin-managed lookup table)
- [x] Define event revenue model (how ticket sales and auction revenue link to MonetaryContribution records for unified donation tracking)
- [x] Define silent auction workflow (open bidding vs. sealed bids, how winners are notified, how payment is collected)
- [x] Write ERD / data model in `docs/data-model.md`

### API Design
- [x] Define REST API contract (all endpoints, auth flow, request/response shapes)
- [x] Define API versioning strategy (URL prefix `/api/v1/` recommended)
- [x] Document API contract in `docs/api-contract.md`

### Authentication & Security
- [x] Choose auth strategy: ASP.NET Core Identity + JWT vs. Azure AD B2C vs. Auth0
- [x] Define role hierarchy and permission matrix (document in `docs/auth-design.md`)
- [x] Define secrets management strategy (Azure Key Vault / .NET user-secrets / environment variables)
- [x] Define PII handling policy (what user data is stored, retention, anonymization)

### Database
- [x] Choose database: SQL Server vs. PostgreSQL (SQLite for local dev)
- [x] Choose ORM/access pattern: EF Core + Repository vs. Dapper vs. direct DbContext
- [x] Define migration strategy (EF Core Migrations recommended)
- [x] Define multi-tenant strategy (single DB with tenant ID vs. DB-per-tenant)

### Infrastructure & Integrations
- [x] Choose payment processor: Stripe (recommended) vs. PayPal vs. other
- [x] Choose email provider: SendGrid vs. Mailgun vs. Azure Communication Services
- [x] Choose SMS provider (optional): Twilio vs. Azure Communication Services
- [x] Choose blob/file storage: Azure Blob Storage vs. AWS S3 vs. local (for receipts, docs)
- [x] Choose geographic/mapping service for volunteer matching + impact map (Google Maps / Mapbox / Leaflet)
- [x] Define background job strategy: Hangfire vs. Azure Service Bus vs. .NET hosted services
- [x] Define caching strategy: in-memory vs. Redis (Redis recommended for multi-instance)

### HQ / Chapter Data Scoping
- [x] Design HQ vs. Chapter data isolation model (HQ sees all; Chapter sees own only)
- [x] Define `ChapterId` scoping on all data-access queries (filter by chapter for Chapter roles)
- [x] Define HQ roll-up aggregation strategy (cross-chapter dashboard queries)
- [x] Document role-to-scope matrix: HQAdmin, ChapterAdmin, ChapterStaff, Volunteer, Donor, PersonInNeed

### Auto-Assignment Engine Design
- [x] Define volunteer scoring algorithm: location proximity (Haversine) + skills match + current workload penalty
- [x] Define assignment trigger: on request submission (immediate) vs. on triage completion (staff-gated)
- [x] Define fallback behavior: if no volunteer scores above threshold → route to unassigned queue for manual dispatch
- [x] Define volunteer acceptance window: how long before auto-reassigning if no response
- [x] Document algorithm in `docs/auto-assignment-algorithm.md`

### Real-Time Strategy
- [x] Choose real-time approach: **SignalR** (recommended) for operations board
- [x] Define `RequestsHub` contract: which events to broadcast (state change, new assignment, escalation, new request)
- [x] Define SignalR group strategy: one group per chapter + one HQ group receiving all chapters' events
- [x] Define client reconnect / state-sync behavior (on reconnect, client re-fetches current board state)

### Architecture Decision Records
- [x] Write ADR-001: Authentication strategy
- [x] Write ADR-002: Database and ORM choice
- [x] Write ADR-003: Payment processor
- [x] Write ADR-004: Org hierarchy and data scoping (HQ → Chapter)
- [x] Write ADR-005: Real-time strategy (SignalR vs. polling)

---

## Phase 2 — Core Domain (Lotv.Core)

**Goal**: All domain entities, interfaces, and service contracts defined and tested.

### Lookup / Reference Entities
- [x] `Chapter` — local chapter of the national org (Id, Name, City, State, ContactName, ContactEmail, IsActive) — replaces "Diocese" as the primary organizational unit; donors, volunteers, and requests all belong to a chapter
- [x] `Diocese` — church diocese reference (Id, Name, City, State, Region, ChapterId) — a diocese maps to a chapter for donor tracking; multiple dioceses can belong to one chapter

### User Entities
- [x] `ApplicationUser` — base identity user (Id, Email, Role, **ChapterId** (null for HQ roles), CreatedAt, IsActive)
- [x] `PersonInNeed` — service recipient profile (UserId, Name, Address, ContactInfo, ChapterId, Notes)
- [x] `Donor` — donor profile (UserId, Name, ContactInfo, **ChapterId**, **DioceseId**, **City**, **State**, IsAnonymous, TaxId/EIN for receipts)
- [x] `Volunteer` — helper profile (UserId, Name, **ChapterId**, GeoLocation, Skills, ServiceRadius, Availability, CurrentWorkloadCount)
- [x] `Employee` / `StaffMember` — internal user (UserId, Name, **ChapterId** (null = HQ), Department, Permissions)

### Request & Fulfillment Entities
- [x] `ServiceRequest` — intake record (Id, RequestorId, Category, Description, Status, **Priority**, **DueDate**, **AssignedToId**, **AssignedToType** (Staff/Volunteer), Address, GeoLocation, CreatedAt, UpdatedAt)
- [x] `ServiceFulfillment` — fulfillment record (Id, RequestId, VolunteerId, StaffId, FulfilledAt, Notes, ResourcesUsed)
- [x] `RequestNote` — collaborative note on a request (Id, RequestId, AuthorId, Content, CreatedAt, IsInternal) — internal notes not visible to requester
- [x] `RequestActivity` — immutable per-request audit trail (Id, RequestId, ActorId, ActivityType, OldValue, NewValue, Timestamp) — records every status change, assignment change, note added
- [x] `RequestAssignment` — tracks assignment history (Id, RequestId, AssignedToId, AssignedById, AssignedAt, AcceptedAt, DeclinedAt, Status) — supports volunteer accept/decline workflow

### Donation & Allocation Entities
- [x] `MonetaryContribution` — money donation (Id, DonorId, Amount, Currency, **DonationChannel**, **CheckNumber** (if applicable), **EventId** (if from an event), ProcessorTransactionId, Status, ReceivedAt, Notes) — DonationChannel records how the donation came in
- [x] `ResourceDonation` — physical goods donation (Id, DonorId, ResourceType, Quantity, Unit, Description, Status, ReceivedAt, StorageLocation)
- [x] `MoneyAllocation` — links money → where it was sent (Id, ContributionId, RequestId or ExpenseId, Amount, AllocatedAt, AllocatedBy, Notes)
- [x] `ResourceAllocation` — links resource donation → where it went (Id, ResourceDonationId, RequestId, Quantity, AllocatedAt, AllocatedBy, Notes)
- [x] `Expense` — operational cost (Id, Description, Amount, Category, PaidAt, PaidBy, ReceiptBlobUrl)

### Event Entities
- [x] `FundraisingEvent` — event record (Id, Name, EventType, Description, Date, EndDate, Venue, Address, Capacity, TicketPrice, GoalAmount, Status, CreatedBy)
- [x] `EventAttendee` — RSVP / ticket record (Id, EventId, DonorId, TicketCount, AmountPaid, DonationChannel, CheckedIn, CheckedInAt, Notes)
- [x] `SilentAuctionItem` — auction item (Id, EventId, Name, Description, FairMarketValue, StartingBid, WinningBid, WinnerId, Status)
- [x] `AuctionBid` — bid record (Id, AuctionItemId, BidderId, BidAmount, BidTime)

### Reporting / Dashboard Entities
- [x] `ImpactSummary` — aggregate DTO (TotalMoneySent, TotalResourcesDonated, PeopleHelped, RequestsFulfilled, ByChapter, ByCategory, ByPeriod) — **ChapterId = null means HQ roll-up**
- [x] `DonorImpactStatement` — per-donor DTO ("Your $X helped N people in [City]")
- [x] `AllocationRecord` — unified ledger row for dashboard display
- [x] `DonationByPersonRow` — per-donor aggregate (DonorId, Name, Diocese, City, State, TotalAmount, GiftCount, AverageGift, FirstGiftDate, LastGiftDate)
- [x] `DonationByDioceseRow` — per-diocese aggregate (DioceseId, DioceseName, City, State, TotalDonors, TotalAmount, AverageGift)
- [x] `DonationByCityRow` — per-city aggregate (City, State, TotalDonors, TotalAmount)
- [x] `DonationByChannelRow` — per-channel aggregate (Channel, TotalAmount, GiftCount, Percentage)
- [x] `DonationByAmountBand` — gift-size distribution (Band label e.g. "$100–$499", GiftCount, TotalAmount, Percentage)
- [x] `VolunteerScoreResult` — **new**: auto-assignment output (VolunteerId, Name, ProximityScore, SkillsMatchScore, WorkloadPenalty, CompositeScore, Recommended)
- [x] `ChapterSummaryRow` — **new**: per-chapter roll-up for HQ dashboard (ChapterId, Name, OpenRequests, OverdueRequests, FulfilledThisPeriod, TotalDonations, ActiveVolunteers)
- [x] `DailyDigestReport` — **new**: overnight activity summary (new requests, donations received, requests fulfilled, stuck/overdue count, chapter breakdown)
- [x] `WeeklySummaryReport` — **new**: chapter-level KPIs for HQ + chapter leads (same fields as DailyDigest but weekly period, with trend vs. prior week)

### Interfaces / Service Contracts
- [x] `IServiceRequestService` — submit, list, update, assign, accept, decline, escalate, add note; all queries scoped by ChapterId
- [x] `IWorkloadService` — get staff/volunteer workload summary, get unassigned queue, get overdue requests; HQ variant returns cross-chapter rollup
- [x] `IDonorService` — register, list, get profile, get contribution history; chapter-scoped
- [x] `IVolunteerService` — register, list, **score volunteers for a request** (location + skills + workload)
- [x] `IAutoAssignmentService` — **new**: score all available chapter volunteers against a request, auto-assign top candidate or route to unassigned queue if none qualify; triggered on request triage completion
- [x] `IPaymentService` — process donation, handle webhook, refund
- [x] `IResourceService` — log resource donation, allocate, track inventory
- [x] `IAllocationService` — allocate money/resources to requests, get allocation history
- [x] `IDashboardService` — aggregate impact stats, money flow, resource flow, geographic breakdown, donation breakdowns; **chapter-scoped** for Chapter roles, **cross-chapter** for HQ Admin
- [x] `INotificationService` — send email, send SMS, queue notification
- [x] `IReportingService` — generate impact reports, export to CSV/PDF; **chapter-scoped or HQ roll-up**
- [x] `IScheduledReportService` — **new**: generate and email daily digest (overnight activity) and weekly summary (chapter KPIs) to HQ and chapter leads on a CRON schedule
- [x] `IUserService` — profile management, role assignment, chapter membership
- [x] `IReceiptService` — generate and send tax receipts for charitable donations
- [x] `IAuditService` — write immutable audit log entries
- [x] `IEventService` — create/manage events, register attendees, process auction bids, generate event revenue reports

### Shared / Value Objects
- [x] Enums: `UserRole` (PersonInNeed, Donor, Volunteer, ChapterStaff, ChapterAdmin, HQAdmin) — replaces generic Staff/Admin; HQAdmin has no ChapterId (sees all); ChapterAdmin/ChapterStaff are scoped to their chapter
- [x] Enums: `RequestStatus` (Submitted, Triaged, Matched, InProgress, Fulfilled, Closed, Cancelled)
- [x] Enums: `RequestPriority` (Urgent, High, Normal, Low)
- [x] Enums: `AssignmentStatus` (Pending, Accepted, Declined, Reassigned, Completed)
- [x] Enums: `ActivityType` (StatusChanged, Assigned, Reassigned, NoteAdded, DueDateSet, Fulfilled, Cancelled, Escalated)
- [x] Enums: `RequestCategory` (Food, Clothing, Shelter, Transportation, Medical, Utilities, Financial, Other)
- [x] Enums: `ResourceType` (Food, Clothing, Shelter, Transportation, Medical, HouseholdGoods, Other)
- [x] Enums: `DonationChannel` (Online, Check, Cash, InPerson, Mail, PhoneCall, Event, Other) — how the donation was received
- [x] Enums: `EventType` (Gala, SilentAuction, Dinner, Concert, GolfTournament, Walkathon, Other)
- [x] Enums: `EventStatus` (Draft, Published, Open, Closed, Completed, Cancelled)
- [x] Enums: `AuctionItemStatus` (Available, Sold, Unsold)
- [x] Enums: `ContributionStatus` (Pending, Processed, Failed, Refunded)
- [x] Enums: `AllocationStatus` (Pending, Allocated, Delivered, Reversed)
- [x] Value object: `Address` (Street, City, State, Zip, Country)
- [x] Value object: `GeoLocation` (Latitude, Longitude) — for mapping and volunteer matching
- [x] Value object: `Money` (Amount, Currency)
- [x] Value object: `ContactInfo` (Phone, Email, PreferredContact)
- [x] Value object: `DateRange` — for dashboard filtering
- [x] Common result types: `Result<T>`, `PagedResult<T>`, `ValidationResult`

---

## Phase 3 — API (Lotv.Api)

**Goal**: Functional REST API with authentication, all core CRUD endpoints, payment processing, and reporting.

### Authentication & Authorization
- [x] Implement chosen auth strategy
- [x] Role-based authorization policies (PersonInNeed, Donor, Volunteer, ChapterStaff, ChapterAdmin, HQAdmin)
- [x] Chapter-scoped query middleware: inject ChapterId claim filter on all Chapter-role queries; HQAdmin bypasses filter
- [x] JWT token issuance / refresh endpoints
- [x] User registration endpoint with role selection and chapter assignment
- [x] Password reset flow

### Service Request Endpoints
- [x] `POST /api/v1/requests` — submit service request (PersonInNeed)
- [x] `GET /api/v1/requests` — list requests with filters (Staff/Admin)
- [x] `GET /api/v1/requests/{id}` — get request detail
- [x] `PUT /api/v1/requests/{id}/status` — update request status (Staff)
- [x] `PUT /api/v1/requests/{id}/assign` — assign volunteer or staff to request (Staff)
- [x] `PUT /api/v1/requests/{id}/priority` — set request priority (Staff)
- [x] `PUT /api/v1/requests/{id}/due-date` — set due date / SLA (Staff)
- [x] `POST /api/v1/requests/{id}/accept` — volunteer/staff accepts their assignment
- [x] `POST /api/v1/requests/{id}/decline` — volunteer/staff declines assignment (triggers reassignment queue)
- [x] `POST /api/v1/requests/{id}/escalate` — escalate request to supervisor (Staff/system)
- [x] `POST /api/v1/requests/{id}/notes` — add a note to a request (Staff/Volunteer)
- [x] `GET /api/v1/requests/{id}/notes` — get all notes on a request
- [x] `GET /api/v1/requests/{id}/activity` — full activity log for a request (who changed what, when)
- [x] `POST /api/v1/requests/{id}/fulfill` — mark request fulfilled + log resources used (Volunteer/Staff)
- [x] `GET /api/v1/requests/my` — person in need's own requests
- [x] `GET /api/v1/requests/queue` — unassigned requests queue (Staff — requests needing someone to take them)
- [x] `GET /api/v1/requests/overdue` — requests past their due date (Staff/Admin)
- [x] `GET /api/v1/workload` — workload summary across all staff and volunteers (Admin/Staff): open request count per person
- [x] `GET /api/v1/workload/{userId}` — workload detail for a specific staff member or volunteer: their assigned requests by status

### Event Endpoints
- [x] `GET /api/v1/events` — list all events (with filters: upcoming, past, type, status)
- [x] `POST /api/v1/events` — create a new event (Staff/Admin)
- [x] `GET /api/v1/events/{id}` — event detail
- [x] `PUT /api/v1/events/{id}` — update event (Staff/Admin)
- [x] `DELETE /api/v1/events/{id}` — cancel / delete event (Admin)
- [x] `GET /api/v1/events/{id}/attendees` — list attendees for an event
- [x] `POST /api/v1/events/{id}/attendees` — register a donor as attendee / sell ticket
- [x] `PUT /api/v1/events/{id}/attendees/{attendeeId}/checkin` — check in an attendee at the door
- [x] `GET /api/v1/events/{id}/revenue` — total revenue raised by the event (tickets + auction + direct donations linked to event)
- [x] `GET /api/v1/events/{id}/auction` — list all silent auction items for an event
- [x] `POST /api/v1/events/{id}/auction` — add an auction item (Staff)
- [x] `PUT /api/v1/events/{id}/auction/{itemId}` — update auction item (bid, status, winner)
- [x] `POST /api/v1/events/{id}/auction/{itemId}/bid` — place a bid on an auction item
- [x] `POST /api/v1/events/{id}/auction/close` — close bidding and record winners (Staff/Admin)
- [x] `GET /api/v1/events/upcoming` — upcoming events (used for public event listing page)
- [x] `GET /api/v1/dashboard/events` — event dashboard summary: upcoming events, past events revenue, top-performing events

### Diocese Endpoints (Lookup)
- [x] `GET /api/v1/dioceses` — list all dioceses (Staff/Admin, used in donor registration dropdowns and dashboard)
- [x] `POST /api/v1/dioceses` — add a diocese (Admin)
- [x] `PUT /api/v1/dioceses/{id}` — update diocese details (Admin)
- [x] `GET /api/v1/dioceses/{id}/donors` — all donors associated with a diocese (Staff/Admin)
- [x] `GET /api/v1/dioceses/{id}/summary` — total donations, donor count, average gift for a diocese (dashboard drill-through)

### Donor & Contribution Endpoints
- [x] `POST /api/v1/donors` — register donor profile
- [x] `GET /api/v1/donors` — list donors (Staff/Admin)
- [x] `GET /api/v1/donors/{id}` — donor profile detail
- [x] `GET /api/v1/donors/{id}/contributions` — donor's contribution history
- [x] `GET /api/v1/donors/{id}/impact` — donor's personal impact statement
- [x] `POST /api/v1/contributions/money` — initiate monetary donation (creates Stripe payment intent)
- [x] `POST /api/v1/contributions/resources` — log a resource donation
- [x] `GET /api/v1/contributions` — list all contributions (Staff/Admin)
- [x] `GET /api/v1/contributions/{id}` — contribution detail

### Payment Endpoints
- [x] `POST /api/v1/payments/intent` — create Stripe PaymentIntent, return client secret
- [x] `POST /api/v1/payments/webhook` — receive and process Stripe webhook events (signature verified)
- [x] `POST /api/v1/payments/{id}/refund` — process refund (Admin)

### Volunteer Endpoints
- [x] `POST /api/v1/volunteers` — register volunteer
- [x] `GET /api/v1/volunteers` — list volunteers (Staff/Admin)
- [x] `GET /api/v1/volunteers/{id}` — volunteer profile
- [x] `GET /api/v1/volunteers/available` — volunteers available near a request location
- [x] `GET /api/v1/volunteers/my/requests` — volunteer's assigned requests

### Allocation Endpoints
- [x] `POST /api/v1/allocations/money` — allocate money → request or expense (Staff/Admin)
- [x] `POST /api/v1/allocations/resources` — allocate resource donation → request (Staff)
- [x] `GET /api/v1/allocations` — full allocation ledger (Staff/Admin)
- [x] `GET /api/v1/allocations/{id}` — allocation detail

### Dashboard & Reporting Endpoints
- [x] `GET /api/v1/dashboard` — overall impact summary (KPI cards): total $ donated, total resources, people helped, requests fulfilled
- [x] `GET /api/v1/dashboard/money` — monetary flow breakdown (by category, by region, by time period, by recipient)
- [x] `GET /api/v1/dashboard/resources` — resource distribution breakdown (by type, by region, by time period)
- [x] `GET /api/v1/dashboard/map` — geographic distribution data (GeoJSON or lat/lng points for map rendering)
- [x] `GET /api/v1/dashboard/timeline` — time-series data for charts (donations and fulfillments over time)

#### Donation Tracking Dashboard Endpoints
- [x] `GET /api/v1/dashboard/donations` — master donations dashboard: all breakdown panels in one response (or use individual endpoints below)
- [x] `GET /api/v1/dashboard/donations/by-person` — per-donor summary rows (name, diocese, city, total, gift count, avg gift, first/last gift date); supports search + sort + pagination
- [x] `GET /api/v1/dashboard/donations/by-diocese` — per-diocese aggregate (diocese name, city, state, donor count, total amount, avg gift); sortable
- [x] `GET /api/v1/dashboard/donations/by-city` — per-city aggregate (city, state, donor count, total amount); sortable
- [x] `GET /api/v1/dashboard/donations/by-channel` — by DonationChannel (Online / Check / Cash / In-Person / Mail / Event / Other): count, total amount, percentage of all donations
- [x] `GET /api/v1/dashboard/donations/by-amount` — gift-size band distribution (<$25, $25–$99, $100–$499, $500–$999, $1,000–$4,999, $5,000+): count, total, percentage
- [x] `GET /api/v1/dashboard/donations/by-diocese/{id}` — drill-through: all donors in a specific diocese with their individual totals

#### Reporting Endpoints
- [x] `GET /api/v1/reports/impact` — full impact report with filters (date range, region, category)
- [x] `GET /api/v1/reports/donations` — full donor report filterable by diocese, city, channel, date range, amount range
- [x] `GET /api/v1/reports/export` — export any report as CSV or PDF (specify report type in query param)
- [x] `GET /api/v1/reports/audit` — audit log viewer (Admin only)

### User & Profile Endpoints
- [x] `GET /api/v1/users/me` — current user profile
- [x] `PUT /api/v1/users/me` — update profile
- [x] `GET /api/v1/users` — list all users (Admin)
- [x] `PUT /api/v1/users/{id}/role` — change user role (Admin)
- [x] `DELETE /api/v1/users/{id}` — deactivate user (Admin)

### Notification Endpoints
- [x] `POST /api/v1/notifications/send` — send ad-hoc notification (Staff)
- [x] `GET /api/v1/notifications/templates` — list email templates (Staff)
- [x] `POST /api/v1/notifications/marketing` — send marketing email blast (Staff)

### Real-Time (SignalR)
- [x] Install `Microsoft.AspNetCore.SignalR` NuGet package
- [x] Implement `RequestsHub` — broadcasts events: `RequestCreated`, `RequestAssigned`, `StatusChanged`, `RequestEscalated`, `RequestCompleted`
- [x] Chapter SignalR groups: each connected user joins their chapter's group on connect; HQAdmin joins a special `hq-all` group that receives all chapters' events
- [x] Map SignalR hub at `/hubs/requests`
- [x] Reconnect + state-sync: client re-fetches board state via REST on reconnect (SignalR is for deltas only)

### Auto-Assignment Engine
- [x] Implement `IAutoAssignmentService`:
  - Query all active volunteers in the request's chapter with matching skills
  - Score each by: proximity (Haversine distance to request address), skills match %, current workload count
  - Composite score = (proximity weight × distance_score) + (skills weight × skills_score) - (workload weight × workload_count)
  - If top candidate score ≥ threshold → auto-assign and notify via `INotificationService`
  - If no candidate qualifies → add to unassigned queue and broadcast `RequestUnassigned` via SignalR
- [x] `POST /api/v1/requests/{id}/auto-assign` — manually trigger auto-assignment (Staff)
- [x] `GET /api/v1/requests/{id}/candidates` — return ranked volunteer candidates for a request (Staff can review before confirming)

### Scheduled Reports (Background Jobs)
- [x] Install Hangfire (or use `IHostedService` + CRON — per ADR)
- [x] Implement `IScheduledReportService`:
  - `GenerateDailyDigest(chapterId)` — overnight activity: new requests, donations received, requests fulfilled, stuck count, overdue count
  - `GenerateWeeklySummary(chapterId)` — chapter KPIs vs. prior week (trend arrows)
  - `GenerateHQWeeklySummary()` — cross-chapter roll-up with per-chapter breakdown table
- [x] Daily digest job — fires at 6:00 AM local (per chapter timezone) → emails chapter leads
- [x] Weekly summary job — fires every Monday 7:00 AM → emails chapter leads + HQ Admin(s)
- [x] HQ cross-chapter weekly report — same Monday job, fires after chapter jobs complete

### Infrastructure
- [x] EF Core DbContext + initial migrations (all entities including Chapter)
- [x] Chapter-scoped query filter: global `IQueryFilter<IChapterScoped>` automatically applies `WHERE ChapterId = @chapterId` for non-HQ users
- [x] Stripe SDK integration (payment processing + webhook verification)
- [x] Email service integration (SendGrid or chosen provider)
- [x] SMS service integration (optional — Twilio or chosen provider)
- [x] Blob storage integration for receipts and documents
- [x] Hangfire dashboard (or equivalent) for monitoring scheduled jobs
- [x] `IReceiptService` implementation — generate PDF tax receipt, email to donor
- [x] `IAuditService` implementation — write to append-only audit log table
- [x] Serilog structured logging
- [x] Health check endpoint (`/health`)
- [x] Global exception handler middleware
- [x] Input validation (FluentValidation recommended)
- [x] Rate limiting on public endpoints (payment, registration)
- [x] CORS policy configuration

---

## Phase 4 — Frontend (Lotv.Web)

**Goal**: Blazor WebAssembly UI with role-based views, donation flow, and Impact & Distribution Dashboard.

### Shared / Shell
- [x] App shell with responsive navigation (AdminLayout.razor — role-based sidebar, topbar with search/notifications)
- [x] Authentication state provider (JWT handling, auto-refresh, ChapterId claim) — `JwtAuthStateProvider`, `AuthService`
- [x] Role-based route guards (`AuthorizeRouteView` + `CascadingAuthenticationState` in App.razor)
- [x] SignalR client service — connect to `RequestsHub` on login, reconnect with state-sync on disconnect (`SignalRService`)
- [x] Notification toast component (real-time SignalR push toasts for all 6 hub events) — `ToastNotification.razor`
- [x] Loading/spinner component — `Loading.razor` (Size + Message params, sm/md/lg variants)
- [x] Reusable chart components (bar, pie, line) — `BarChart.razor` (horizontal/vertical, CSS bars, typeparam), `PieChart.razor` (SVG donut/pie with legend), `LineChart.razor` (SVG area/line, dual-series, grid lines); all pure Blazor/SVG, no external library

### Person in Need Views
- [x] Submit service request form — `Apply.razor` (public intake, wired to `/api/v1/public/apply`)
- [x] My Requests dashboard — `MyRequests.razor`: filter chips, status badges, progress tracker, request cards
- [x] Request detail / status tracking page — `MyRequestDetail.razor`: status steps, team updates, helper info
- [x] Profile management page — `MyProfile.razor`: personal info, address, communication prefs

### Donor Views
- [x] Donor registration / profile page — `Give.razor` collects donor info on donation submission
- [x] Make a Monetary Donation page — `Give.razor` (public, wired to `/api/v1/public/give`)
- [x] Donate Resources page — `DonateResources.razor`: resource type, quantity, description, preference
- [x] Donation confirmation / receipt page — `DonationConfirm.razor` at `/donate/confirm`: thank-you, summary card, PDF receipt stub, CTAs
- [x] Contribution history list — visible in `DonorImpact.razor`
- [x] **My Impact page** — `DonorImpact.razor`: total given, families helped, category breakdown bar charts, full donation history

### Volunteer Views
- [x] Volunteer registration / profile — `VolunteerSignup.razor` (public registration)
- [x] Available Requests near me — `VolunteerAvailable.razor`: unassigned queue, filter by category/priority, accept button
- [x] **Pending Assignment** — `VolunteerPending.razor` at `/volunteer/pending/{Id}`: request details, accept/decline buttons, confirmation states
- [x] **My Work Queue** — `VolunteerMyAssignments.razor`: filter chips, quick status update buttons, overdue indicator
- [x] Request detail view — `MyRequestDetail.razor` (also used by Person in Need; read-only notes thread)
- [x] Complete/report a request — quick "Mark Complete" button on VolunteerMyAssignments
- [x] My History — `VolunteerHistory.razor`: KPI strip (total fulfilled, families, since date, top category), fulfillment table
- [x] Volunteer dashboard — `VolunteerDashboard.razor`: KPI strip, active assignments, available near me panels

### Staff Views (Chapter-Scoped — ChapterStaff / ChapterAdmin)
- [x] **Real-Time Operations Board** — live Kanban connected to SignalR `RequestsHub`; cards update via `OnCaseStatusChanged`, `OnCaseCreated`, `OnCaseAssigned` events; scoped to chapter
- [x] **All Requests dashboard** — Dashboard.razor wired to ApiService (KPIs, recent cases, overdue alert, channel breakdown, workload, audit log)
- [x] **Unassigned Queue** — `Queue.razor`: KPI strip, sortable table, assignment drawer with ranked volunteer candidates; one-click assign
- [x] **Kanban Board View** — requests organized in columns by CaseStatus; cards from API; local optimistic update on status change (API write TODO Phase 5)
- [x] **Auto-Assignment Candidates panel** — built into `CaseDetail.razor`: ranked candidates list with one-click assign/override
- [x] **Request detail / case management page** — `CaseDetail.razor` at `/admin/cases/{Id}`:
  - Assign or reassign volunteer/staff
  - Set priority and due date
  - Change status
  - Notes thread (internal notes, visible to staff/volunteer only)
  - Full activity log (who changed what, when)
  - Resources allocated to this request
  - Money allocated to this request
- [x] **Workload View** — Workload.razor: volunteer load table, overdue count, reassign drawer; fully wired to ApiService
- [x] **My Work Queue** (for Staff) — `MyQueue.razor`: filter chips, quick-update drawer, case detail link
- [x] Donor Management list (search, sort, view contribution totals) — Donors.razor wired to ApiService
- [x] Volunteer Management list — Volunteers.razor wired to ApiService
- [x] Allocate money → request/expense form — `AllocateMoney.razor` at `/admin/allocate/money`: donation selector, target (request/expense/program), amount + notes, recent sidebar
- [x] Allocate resources → request form — `AllocateResources.razor` at `/admin/allocate/resources`: resource inventory, request selector, quantity, notes
- [x] Allocation ledger view (full history of where money + resources went) — Allocations.razor wired to ApiService
- [x] Send targeted notification to user(s) — `SendNotification.razor`: audience selector, channel, templates with tokens, recent log
- [x] Marketing email composer + send — `MarketingEmail.razor`: campaign name, audience, template library, preview, recipient estimate

### HQ Dashboard (HQAdmin only)
- [x] **Cross-Chapter Summary table** — `HqDashboard.razor`: one row per chapter with open/overdue/fulfilled/donations/volunteers; click to drill into chapter cases
- [x] **HQ-wide KPI strip** — 6-card national KPI strip: Active Chapters, Open, Overdue, Fulfilled MTD, Total Donations, Active Volunteers
- [x] **Chapter comparison chart** — inline CSS bar charts comparing chapters on Open Requests and Donations
- [x] **HQ Operations Board** — `HqOperationsBoard.razor` at `/admin/hq-board`: live 4-column Kanban per chapter, national KPI strip, chapter filter chips, SignalR real-time updates
- [x] **Scheduled Report Management** — `ScheduledReports.razor` at `/admin/reports/schedule`: per-chapter toggle + recipient email, HQ override recipients, last-sent log table

### Impact & Distribution Dashboard (Chapter-Scoped for Chapter roles; HQ sees all-chapter version)
- [x] **KPI Summary Cards** — `ImpactDashboard.razor`: total donated, resources, people helped, fulfilled
- [x] **Money Flow panel** — bar chart by category with percentages and request counts
- [x] **Resource Distribution panel** — bar chart by resource type with quantities
- [ ] **Geographic Map**: Interactive map showing service delivery points — deferred (requires mapping library)
- [x] **Timeline Chart** — monthly bar chart + data table (last 12 months)
- [x] **Category Breakdown Chart** — inline CSS bar charts for category and channel breakdowns
- [x] **Top Regions** — ByCity.razor: sortable city/state table with bar chart
- [x] **Allocation Ledger Table** — links to Allocations.razor (already built)
- [x] **Export** — links to Export.razor (already built)
- [x] **Date Range Filter** — MTD / QTD / YTD / All Time filter chips
- [x] **Public Transparency Page** — `Transparency.razor` at `/transparency`: aggregate KPIs, money flow, monthly trend, CTAs

### Donation Tracking Dashboard (Staff / Admin)
*Tracks who donated, how much, from where, and how the donation came in.*

- [x] **By Person panel** — `ByDonor.razor`: searchable, sortable donor table with gift history expand
- [x] **By Diocese panel** — `ByDiocese.razor`: KPI strip, diocese cards, comparison table
- [ ] **Diocese Map** — geographic map with pins — deferred (requires mapping library)
- [x] **By City panel** — `ByCity.razor`: sortable city/state table with top-10 bar chart
- [x] **By Channel panel** — `ByChannel.razor`: KPI strip, channel cards, table, date filter
- [x] **By Amount panel** — `ByAmount.razor`: gift-size distribution with dual bar charts + summary table
- [x] **Full Donor Ledger** — `Donations.razor`: paginated, filterable master list
- [x] **Donor Detail Drawer/Page** — `DonorDetail.razor` at `/admin/donors/{Id}`: KPI strip, profile table, contribution history, edit drawer

### Event Management Dashboard (Staff / Admin)
*Tracks fundraising events — galas, silent auctions, and other events — and connects attendance and revenue back to the donor tracking dashboard.*

- [x] **Upcoming Events list** — `Events.razor`: card grid with filter chips, KPI strip, registration counter
- [x] **Event detail page** — `EventDetail.razor`: summary, attendees, auction, revenue tabs
- [x] **Past Events panel** — filter chip "Past" on Events.razor; past events show dimmed with final counts
- [x] **Event Revenue widget** — added to `Dashboard.razor`: total revenue YTD, ticket revenue, auction revenue, per-event goal bars

### Event Management Views (Staff / Admin)
- [x] **Events list page** — `Events.razor`: filter by type/status, KPI strip, event cards
- [x] **Create / Edit Event form** — drawer within Events.razor
- [x] **Event detail / management page** — `EventDetail.razor` at `/admin/events/{Id}`:
  - Summary: date, venue, ticket sales vs. capacity, revenue raised vs. goal
  - Attendee list: searchable, check-in button, register attendee drawer
  - Silent auction tab: item cards, add item, place bid, close bidding
  - Revenue breakdown: ticket + auction + total with goal bar
- [x] **Public Event Page** — `Events.razor` at `/events`: event cards with capacity gauge, filter, RSVP modal, past events section
- [x] **Donor RSVP / Ticket Purchase flow** — `EventTickets.razor` at `/events/{Id}/tickets`: ticket count stepper, donor info form, Stripe payment stub, order summary sidebar, confirmation state

### Admin Views
- [x] User management (list, search, change role, deactivate) — UserManagement.razor wired to ApiService
- [x] Diocese management (list, add, edit dioceses) — DioceseData.razor wired to ApiService
- [x] System configuration page — `Settings.razor` (org info, notification prefs, escalation rules)
- [x] Audit log viewer (filterable) — AuditLog.razor wired to ApiService
- [x] Platform health / system status page — `Health.razor`: service checks, latency, env info, quick actions

### Donation Reporting Views (built, wired to ApiService)
- [x] All Donations ledger — Donations.razor (filter bar, sort, record donation drawer)
- [x] By Channel breakdown — ByChannel.razor (KPI strip, channel cards, table, date filter)
- [x] By Donor breakdown — ByDonor.razor (KPI strip, searchable table, gift history expand)
- [x] By Diocese breakdown — ByDiocese.razor (KPI strip, diocese cards, comparison table)
- [x] Fund Allocations — Allocations.razor (pending/approved/unallocated workflow)
- [x] Impact Report — ImpactReport.razor (7 data sources, aggregations, monthly trend chart)
- [x] Export — Export.razor (6 entity types, CSV download, audit log entries)
- [x] Ministry Events — Events.razor (card grid, registration counter, add/edit drawer)

### Public Pages (built, wired to ApiService)
- [x] Home — public landing page with live impact strip counts
- [x] Apply — public intake form → CreateFamilyAsync + CreateRequestAsync
- [x] Give — public donation form → CreateDonorAsync + CreateDonationAsync
- [x] VolunteerSignup — public volunteer form → CreateVolunteerAsync

---

## Phase 5 — Testing ✅ COMPLETE

**Goal**: Meaningful test coverage for all critical paths.

### Unit Tests (Lotv.Core logic)
- [x] `ServiceRequestService` — state transitions, IsOverdue, priority ordering, default values, note visibility, escalation (16 tests)
- [x] `WorkloadService` — workload aggregation, overdue detection, unassigned queue logic, chapter scoping (10 tests incl. allocation + daily digest)
- [x] `AllocationService` — allocate money, amount validation, prevent over-allocation, PendingReview filter (10 tests)
- [x] `DashboardService` — aggregate stats, channel breakdown, date range filter, amount bands, people helped, HQ rollup (15 tests)
- [x] `ReportingService` — ImpactSummary construction, year-end statement, receipt content (15 tests)
- [x] `ReceiptService` — tax receipt content generation (covered in DashboardReportingTests)
- [x] `AutoAssignmentService` — scoring, capacity filtering, loyalty bonus, TryAutoAssign, HandleDecline (14 tests)
- [x] Value object tests: `Money`, `GeoLocation`, `Address`, `DateRange`, `ContactInfo`
- [x] Enum/domain rule tests: `IsOverdue`, `PackageReason.ToDisplayName`, `VolunteerRole.ToDisplayName`, `DonationChannel.ToDisplayName`, `Family.FullName`

### Unit Tests (Lotv.Api)
- [x] Controller tests for all endpoint groups — authorization policies (20+ tests via ControllerAuthorizationTests)
- [x] Stripe webhook handler — HTTP contract tests (AllowAnonymous, POST accepted, non-POST rejected) (8 tests)
- [x] `JwtTokenService` — access token claims, refresh token generation (12 tests)
- [x] Authorization attribute / policy tests — Staff 200, HQAdmin-only 403 for Staff, invalid token 401

### Integration Tests
- [x] API integration tests using `WebApplicationFactory` + SQLite test database (auth + requests endpoints)
- [x] Request CRUD: POST, GET by ID, status update — seeded family, chapterId-scoped user
- [x] Dashboard endpoint returns correct aggregates (openCases, overdue, donations, volunteers)
- [x] Full request submission → fulfillment flow (New → InProgress → AwaitingShipment → Fulfilled, notes, activity log)
- [x] Donor registration → monetary contribution → dashboard stats reflect donation

### Test Infrastructure
- [x] Test data builder / factory (inline helpers in each integration test class)
- [x] Seed data strategy for integration tests (per-test family/user creation via API)
- [x] Test coverage reporting (Coverlet + coverlet.msbuild — run: `dotnet test tests/Lotv.Tests/Lotv.Tests.csproj -p:CollectCoverage=true`)
- [x] Coverage target: ≥ 80% on Lotv.Core — **achieved 86.4% line / 94.3% branch / 80.5% method** (ModelCoverageTests.cs: 77 tests across all untested model types)

### Optional
- [x] E2E browser tests (Playwright) for critical user flows — `tests/Lotv.E2E`; BrowserFixture (shared Chromium), E2ETestBase (per-test context/page + helpers); suites: PublicPagesTests, AuthFlowTests, ApplyFlowTests, DonationFlowTests, VolunteerFlowTests, AdminPagesTests (login-gated), MobileResponsivenessTests (390×844 viewport), AccessibilityTests (WCAG 2.1 AA checks); configurable via `E2E_BASE_URL`/`E2E_HEADLESS`/`E2E_SLOW_MO` env vars; README with setup + CI guide

---

## Phase 6 — Deployment & Launch

**Goal**: Application live in production with CI/CD, monitoring, and security hardened.

### Infrastructure Setup
- [ ] Choose hosting: Azure App Service / Azure Container Apps / AWS / Railway
- [ ] Choose database hosting: Azure SQL / AWS RDS / Supabase / Railway
- [ ] Set up blob storage account (Azure Blob / S3) for receipts and documents
- [ ] Set up Redis (if chosen for caching/sessions)
- [ ] Configure secrets management (Azure Key Vault / AWS Secrets Manager)
- [ ] Set up CDN for Blazor WASM static assets (Azure CDN / Cloudflare)

### Containerization
- [x] Write `Dockerfile` for Lotv.Api (multi-stage, non-root user)
- [x] Write `Dockerfile` for Lotv.Web (Blazor WASM → nginx:alpine + SPA routing)
- [x] Write `docker-compose.yml` for local dev (API + Web + SQLite volume, health check gate)
- [x] Write `.dockerignore` (excludes bin/obj/tests/data/sessions)

### CI/CD
- [x] GitHub Actions workflow: build + test on every PR — `.github/workflows/ci.yml` (restore → build → test with coverage → TRX results → coverage comment on PR)
- [x] GitHub Actions workflow: deploy to staging on merge to `main` — `.github/workflows/deploy-staging.yml` (test gate → build + push Docker images → deploy stub)
- [x] GitHub Actions workflow: deploy to production on release tag — `.github/workflows/deploy-production.yml` (triggered on `v*.*.*` tag → test gate → tagged images → GitHub Release notes)
- [x] Environment configuration: `dev / staging / prod` via environment variables — `appsettings.Staging.json` added; full secrets reference in `docs/environment-config.md`
- [x] Database migration step in deployment pipeline — `dotnet ef database update` in both `deploy-staging.yml` and `deploy-production.yml`

### Payment Processor Setup
- [ ] Register Stripe account (or chosen provider)
- [ ] Configure Stripe webhook endpoint in Stripe dashboard
- [ ] Store Stripe keys in secrets manager
- [ ] Enable Stripe test mode for staging

### Monitoring & Reliability
- [x] Structured logging (Serilog) — dev: colored console; prod: ISO timestamp format; configurable via appsettings
- [ ] Alerts: error rate spike, payment failure spike, high latency (requires cloud setup)
- [ ] Uptime monitoring (external probe — requires cloud setup)
- [ ] Database backup strategy (automated daily backups — requires cloud setup)
- [x] Disaster recovery runbook in `docs/` — `docs/disaster-recovery-runbook.md`

### Security Hardening
- [x] HTTPS redirect enforced (UseHttpsRedirection)
- [x] Security headers middleware (HSTS in prod, X-Content-Type-Options, X-Frame-Options, Referrer-Policy, X-XSS-Protection)
- [x] Rate limiting on auth endpoints (10 req/min per IP in prod) and payment webhook (30/min)
- [x] Health check endpoint at `/health` (DB connectivity check)
- [x] OWASP Top 10 review before launch — `docs/owasp-review.md`; 5 high-priority pre-launch fixes identified
- [x] Dependency vulnerability scan (`dotnet list package --vulnerable`) — 0 vulnerabilities found 2026-03-25
- [x] Initial EF Core migration generated (`InitialCreate`) — production startup now calls `db.Database.Migrate()`
- [x] `appsettings.Production.json` template committed (placeholders for ConnectionString, Jwt:Key, AllowedOrigins)

### Launch
- [ ] Domain setup and SSL certificate (requires cloud setup)
- [ ] DNS configuration (requires cloud setup)
- [x] Smoke test checklist — `docs/smoke-test-checklist.md` (8 sections: infra, auth, request lifecycle, volunteers/donors, dashboard, SignalR, rate limiting, frontend)
- [ ] Launch checklist sign-off

---

## Discovered / Backlog

*Tasks that don't fit a phase yet, or are post-launch improvements.*

### Financial & Compliance
- [x] Tax receipt / charitable receipt PDF generation — HTML receipt via `IReceiptService` / `ReceiptService`; `GET /api/v1/donations/{id}/receipt` + `GET /api/v1/donations/year-end/{donorId}/{year}`; IRS § 170 compliant language, EIN placeholder
- [x] Payment reconciliation report (compare Stripe records vs. internal contribution records) — `PaymentReconciliation.razor` at `/admin/reconciliation`; period selector, run-report action, KPI strip (matched/discrepancy/stripe-only/internal-only), filterable results table, resolution guide panel, CSV export stub
- [x] Donor anonymity option — `IsAnonymous` on `Donor` model; `PATCH /api/v1/donors/{id}/privacy` (ChapterAdmin); names masked in donor list with `?maskAnonymous=true`; event attendee list masks anonymous donors via `Include(Donor)`
- [x] Financial audit export — `GET /api/v1/audit/export` (ChapterAdmin); CSV of `FundAllocation` audit entries with date range filter; `Content-Disposition: attachment`
- [x] GDPR / CCPA compliance review and PII handling policy — `docs/privacy-compliance.md`

### Operations
- [x] Audit logging — immutable append-only record of all financial allocations — `IFinancialAuditService` / `FinancialAuditService`; POST create, approve, reject allocation endpoints all emit audit entries
- [x] Resource inventory management — `ResourceItem` model + `DbSet`; CRUD at `GET/POST/PUT /api/v1/inventory`; `PATCH /{id}/adjust` for stock adjustments (ChapterAdmin); chapter-scoped; `ResourceCategory` enum
- [x] Marketing email template design and branding — 5 branded HTML templates: `welcome-donor.html`, `donation-receipt.html`, `welcome-volunteer.html`, `daily-digest.html`, `year-end-statement.html`; Handlebars-style `{{placeholder}}` tokens; IRS § 170 compliant receipt + statement
- [x] Onboarding flows for each user type (guided first-login experience) — `OnboardingVolunteer.razor` (`/onboarding/volunteer`): 5-step wizard (profile, availability/skills, chapter, role explanation, completion); `OnboardingStaff.razor` (`/onboarding/staff`): 5-step wizard (profile, chapter, role explanation, first-action picker, completion)
- [x] In-app help / FAQ — `Help.razor` at `/help` (PublicLayout); 25 FAQ items across 5 categories (Requesting Help, Volunteering, Donations, Account & Privacy, Technical); live search + category filter chips; accordion expand/collapse; contact block

### Quality & Accessibility
- [x] Accessibility audit (WCAG 2.1 AA) — ARIA labels/roles on all public forms (Apply, Give, VolunteerSignup), Login; `role="alert"` + `aria-live="assertive"` on all error messages; `role="navigation"` + `aria-label` on sidebar; `role="search"` on global search; `aria-live="polite"` + `role="status"` on ToastNotification; `aria-label` on notification bell (keyboard-accessible); `aria-pressed` on preset donation buttons; `autocomplete` attributes on all name/email/phone/address inputs; `aria-describedby` for hint text; `focus-visible` keyboard ring in CSS
- [x] Mobile responsiveness review (all views) — 768px sidebar off-canvas with hamburger; 480px single-column KPI/kanban; touch targets 44px; public layout responsive
- [x] Performance profiling of dashboard aggregate queries — added composite indexes: `(ChapterId, Status, CreatedAt)` for overdue-by-chapter, `(ChapterId, TotalGiven)` for top-donor leaderboard, `(ChapterId, Channel, Date)` for channel-breakdown-over-time, `(ChapterId, PaidAt)` for monthly expense aggregates; added `HasPrecision(12,2)` on all money fields (Donation.Amount, FundAllocation.Amount, Expense.Amount, Donor.TotalGiven/RecurringAmount); migration `PerfIndexesAndPrecision` generated
- [ ] Localization / i18n (if serving non-English speakers)

### Future Features
- [x] Recurring donations (Stripe subscriptions) — `RecurringDonation` model + `DonorRecurring.razor` (`/donor/recurring`): active schedules list, pause/resume/cancel/edit actions, cancel confirmation, new recurring gift modal with preset amounts/frequency/start date/campaign; `MonthlyTotal` computed from frequency normalization
- [x] Wish list / in-kind donation requests (person in need requests specific goods) — `WishListItem` model (`WishListCategory`/`WishListStatus` enums, nullable `FamilyId` for chapter-wide items), `WishListPublic.razor` (`/wish-list`, public) and `WishList.razor` (`/admin/wishlist`); wish-list endpoints at `GET/POST /api/v1/wishlist`, `GET /open`, `POST /{id}/fulfill`
- [x] Volunteer scheduling / calendar — `VolunteerSchedule.razor` at `/admin/volunteer-schedule`; Week view (grid per volunteer × day, availability shading, assignment slots), Month view (calendar grid with assignment pills), List view (next 14 days table); prev/next/today navigation; overdue highlighting
- [x] SMS check-in for volunteers on active requests — `ISmsService`/`SmsService` (Twilio REST API via HttpClient, dev no-op when unconfigured); `SmsLog` model; `POST /api/v1/requests/{id}/checkin`; assignment/overdue-reminder/check-in/accepted message templates
- [x] Public API for third-party integrations (partner organizations) — `ApiKey` model (SHA-256 hash, `ApiKeyScope` enum ReadOnly/Write/Admin, nullable ChapterId); `POST /api/public/v1/requests`, `POST /api/public/v1/donations`, `GET /api/public/v1/impact|chapters|wishlist`; API key management endpoints at `GET/POST/DELETE /api/v1/apikeys` (HQAdmin)
- [x] Mock / seed data — `DevSeedData.SeedAsync` in `src/Lotv.Api/Data/SeedData.cs`; seeded in Development only (skipped in tests via `Testing:SkipSeed=true`); covers 3 chapters, 3 dioceses, 6 parishes, 10 families, 10 volunteers, 8 donors, 12 requests, 13 donations, 6 fund allocations, 10 expenses, 5 events, 10 resource items, 8 wish-list items, 5 recurring donations, 4 pledges; all names/emails/addresses fictitious
- [x] Online bidding for silent auction — `AuctionHub` (anonymous viewers + staff bidding), `/hubs/auction` endpoint, `AuctionSignalRService.cs` (Blazor client), `SilentAuction.razor` at `/admin/events/{id}/auction` (real-time item grid + live bid feed panel + add item modal); bid validation (must exceed current high); server broadcasts `BidPlaced` / `AuctionClosed` to `auction-{eventId}` group via `IHubContext<AuctionHub>`
- [x] Event QR code check-in — `TicketCode` (Guid, unique) added to `EventAttendee`; `QRCoder` NuGet added; `GET .../attendees/{id}/qr` returns SVG QR; `POST .../scan` looks up by code and marks checked-in (409 on re-scan with attendee info); `AddTicketCodeAndMoneyPrecision` migration; `EventCheckin.razor` at `/admin/events/{id}/checkin` with KPI strip, code entry input (Enter key fires scan), scan result panel (green/red), attendee list with status filter + search, manual check-in button; `ScanTicketAsync` + `CheckinScanResult` record in `ApiService`
- [x] Sponsorship tracking (corporate sponsors for events, linked to donor record) — `Sponsorships.razor` at `/admin/sponsorships`; KPI strip, status filter + search, tier badges (Platinum/Gold/Silver/Bronze), linked donor record, renewal countdown with color warnings, detail drawer (financials, payment progress bar, contact, engagement), Add Sponsor modal
- [x] Dashboard API coverage — all 6 missing reporting endpoints added: `GET /api/v1/dashboard/donations/by-city` (city/state grouping), `/by-amount` (6 bands), `/by-diocese` (enriched with Diocese city/state), `/timeline` (monthly donations + fulfillments), `/money` (fund allocations by category parsed from AllocatedTo), `/resources` (inventory by category); all chapter-scoped
- [x] Public-safe frontend pages — fixed auth violation: Home.razor, Transparency.razor, Events.razor, WishListPublic.razor all migrated from staff-only endpoints to anonymous public API; added `GET /api/public/v1/events` + `/transparency/money` + `/transparency/timeline`; enriched `/impact` with familiesServed + diocesesReached; added PublicEventDto, PublicWishListItemDto, PublicImpactDto records
- [x] Pledge management (donor pledges a future gift, tracked until fulfilled) — `DonorPledge` model (pledgedAmount, fulfilledAmount, targetDate, status, campaign); `RecurringDonation` model (amount, frequency, nextChargeDate, stripeSubscriptionId, status, endsOn); both have `DbSet`, EF config with indexes, and full CRUD endpoints at `GET/POST/PUT /api/v1/pledges` + `POST /{id}/apply`, `POST /{id}/pause|cancel`, `GET /api/v1/recurring`
- [x] DonorImpact and MyRequests public page fix — added `GET /api/public/v1/donors/{id}/impact` (giving history, calculated impact, category breakdown with real allocations or estimated split) and `GET /api/public/v1/families/{id}/requests` (family service history, all enums as strings); DonorImpact.razor + MyRequests.razor accept `?DonorId=`/`?FamilyId=` query params; MyRequests converted all enum comparisons to string pattern matching
- [x] PublicApiTests integration test suite — 35 tests covering all anonymous public endpoints (shape assertions), donor-impact end-to-end (create donor + donations → verify totals + category breakdown), family-requests end-to-end (create family + request → verify string Status = "New"), all 6 dashboard reporting endpoints (401 without token, 200 with Staff, shape checks for by-amount/timeline/by-city); fixed by-city and by-diocese EF Core GroupBy translation bug (rewritten to load flat Join projection in SQL then group in memory)
