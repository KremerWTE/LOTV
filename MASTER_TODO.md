# MASTER_TODO — Lily of the Valley (LOTV)

**Project**: LOTV SaaS Social Services Coordination Platform
**Stack**: .NET 9 · ASP.NET Core Web API · Blazor WebAssembly · xUnit
**Last Updated**: 2026-03-02

---

## Phase Overview

| Phase | Name | Status |
|---|---|---|
| 0 | Foundation | ✅ COMPLETE |
| 1 | Architecture & Design | ⬜ PENDING |
| 2 | Core Domain (Lotv.Core) | ⬜ PENDING |
| 3 | API (Lotv.Api) | ⬜ PENDING |
| 4 | Frontend (Lotv.Web) | ⬜ PENDING |
| 5 | Testing | ⬜ PENDING |
| 6 | Deployment & Launch | ⬜ PENDING |

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
- [ ] Define domain model (entities for all 5 user types + request lifecycle + resource/money flows)
- [ ] Define service request lifecycle states (Submitted → Triaged → Matched → In Progress → Fulfilled / Closed / Escalated)
- [ ] Define escalation rules (e.g., auto-escalate if no volunteer accepts within X hours, or if request passes due date)
- [ ] Define volunteer assignment workflow (staff assigns → volunteer notified → volunteer accepts or declines → if declined, back to unassigned queue or reassigned)
- [ ] Define resource donation lifecycle (Received → Allocated → Delivered)
- [ ] Define monetary contribution lifecycle (Received → Processed → Allocated → Disbursed)
- [ ] Define diocese data model (how dioceses are managed — seed list vs. admin-managed lookup table)
- [ ] Define event revenue model (how ticket sales and auction revenue link to MonetaryContribution records for unified donation tracking)
- [ ] Define silent auction workflow (open bidding vs. sealed bids, how winners are notified, how payment is collected)
- [ ] Write ERD / data model in `docs/data-model.md`

### API Design
- [ ] Define REST API contract (all endpoints, auth flow, request/response shapes)
- [ ] Define API versioning strategy (URL prefix `/api/v1/` recommended)
- [ ] Document API contract in `docs/api-contract.md`

### Authentication & Security
- [ ] Choose auth strategy: ASP.NET Core Identity + JWT vs. Azure AD B2C vs. Auth0
- [ ] Define role hierarchy and permission matrix (document in `docs/auth-design.md`)
- [ ] Define secrets management strategy (Azure Key Vault / .NET user-secrets / environment variables)
- [ ] Define PII handling policy (what user data is stored, retention, anonymization)

### Database
- [ ] Choose database: SQL Server vs. PostgreSQL (SQLite for local dev)
- [ ] Choose ORM/access pattern: EF Core + Repository vs. Dapper vs. direct DbContext
- [ ] Define migration strategy (EF Core Migrations recommended)
- [ ] Define multi-tenant strategy (single DB with tenant ID vs. DB-per-tenant)

### Infrastructure & Integrations
- [ ] Choose payment processor: Stripe (recommended) vs. PayPal vs. other
- [ ] Choose email provider: SendGrid vs. Mailgun vs. Azure Communication Services
- [ ] Choose SMS provider (optional): Twilio vs. Azure Communication Services
- [ ] Choose blob/file storage: Azure Blob Storage vs. AWS S3 vs. local (for receipts, docs)
- [ ] Choose geographic/mapping service for volunteer matching + impact map (Google Maps / Mapbox / Leaflet)
- [ ] Define background job strategy: Hangfire vs. Azure Service Bus vs. .NET hosted services
- [ ] Define caching strategy: in-memory vs. Redis (Redis recommended for multi-instance)

### Architecture Decision Records
- [ ] Write ADR-001: Authentication strategy
- [ ] Write ADR-002: Database and ORM choice
- [ ] Write ADR-003: Payment processor
- [ ] Write ADR-004: Multi-tenancy approach

---

## Phase 2 — Core Domain (Lotv.Core)

**Goal**: All domain entities, interfaces, and service contracts defined and tested.

### Lookup / Reference Entities
- [ ] `Diocese` — church diocese reference (Id, Name, City, State, Region, ContactName, ContactEmail) — donors are associated to a diocese; drives dashboard grouping and reporting

### User Entities
- [ ] `ApplicationUser` — base identity user (Id, Email, Role, CreatedAt, IsActive)
- [ ] `PersonInNeed` — service recipient profile (UserId, Name, Address, ContactInfo, Notes)
- [ ] `Donor` — donor profile (UserId, Name, ContactInfo, **DioceseId**, **City**, **State**, IsAnonymous, TaxId/EIN for receipts) — diocese and city are required for donation tracking dashboard
- [ ] `Volunteer` / `LocalHelper` — helper profile (UserId, Name, GeoLocation, Skills, ServiceRadius, Availability)
- [ ] `Employee` / `StaffMember` — internal user (UserId, Name, Department, Permissions)

### Request & Fulfillment Entities
- [ ] `ServiceRequest` — intake record (Id, RequestorId, Category, Description, Status, **Priority**, **DueDate**, **AssignedToId**, **AssignedToType** (Staff/Volunteer), Address, GeoLocation, CreatedAt, UpdatedAt)
- [ ] `ServiceFulfillment` — fulfillment record (Id, RequestId, VolunteerId, StaffId, FulfilledAt, Notes, ResourcesUsed)
- [ ] `RequestNote` — collaborative note on a request (Id, RequestId, AuthorId, Content, CreatedAt, IsInternal) — internal notes not visible to requester
- [ ] `RequestActivity` — immutable per-request audit trail (Id, RequestId, ActorId, ActivityType, OldValue, NewValue, Timestamp) — records every status change, assignment change, note added
- [ ] `RequestAssignment` — tracks assignment history (Id, RequestId, AssignedToId, AssignedById, AssignedAt, AcceptedAt, DeclinedAt, Status) — supports volunteer accept/decline workflow

### Donation & Allocation Entities
- [ ] `MonetaryContribution` — money donation (Id, DonorId, Amount, Currency, **DonationChannel**, **CheckNumber** (if applicable), **EventId** (if from an event), ProcessorTransactionId, Status, ReceivedAt, Notes) — DonationChannel records how the donation came in
- [ ] `ResourceDonation` — physical goods donation (Id, DonorId, ResourceType, Quantity, Unit, Description, Status, ReceivedAt, StorageLocation)
- [ ] `MoneyAllocation` — links money → where it was sent (Id, ContributionId, RequestId or ExpenseId, Amount, AllocatedAt, AllocatedBy, Notes)
- [ ] `ResourceAllocation` — links resource donation → where it went (Id, ResourceDonationId, RequestId, Quantity, AllocatedAt, AllocatedBy, Notes)
- [ ] `Expense` — operational cost (Id, Description, Amount, Category, PaidAt, PaidBy, ReceiptBlobUrl)

### Event Entities
- [ ] `FundraisingEvent` — event record (Id, Name, EventType, Description, Date, EndDate, Venue, Address, Capacity, TicketPrice, GoalAmount, Status, CreatedBy)
- [ ] `EventAttendee` — RSVP / ticket record (Id, EventId, DonorId, TicketCount, AmountPaid, DonationChannel, CheckedIn, CheckedInAt, Notes)
- [ ] `SilentAuctionItem` — auction item (Id, EventId, Name, Description, FairMarketValue, StartingBid, WinningBid, WinnerId, Status)
- [ ] `AuctionBid` — bid record (Id, AuctionItemId, BidderId, BidAmount, BidTime)

### Reporting / Dashboard Entities
- [ ] `ImpactSummary` — aggregate DTO (TotalMoneySent, TotalResourcesDonated, PeopleHelped, RequestsFulfilled, ByRegion, ByCategory, ByPeriod)
- [ ] `DonorImpactStatement` — per-donor DTO ("Your $X helped N people in [City]")
- [ ] `AllocationRecord` — unified ledger row for dashboard display
- [ ] `DonationByPersonRow` — per-donor aggregate (DonorId, Name, Diocese, City, State, TotalAmount, GiftCount, AverageGift, FirstGiftDate, LastGiftDate)
- [ ] `DonationByDioceseRow` — per-diocese aggregate (DioceseId, DioceseName, City, State, TotalDonors, TotalAmount, AverageGift)
- [ ] `DonationByCityRow` — per-city aggregate (City, State, TotalDonors, TotalAmount)
- [ ] `DonationByChannelRow` — per-channel aggregate (Channel, TotalAmount, GiftCount, Percentage)
- [ ] `DonationByAmountBand` — gift-size distribution (Band label e.g. "$100–$499", GiftCount, TotalAmount, Percentage)

### Interfaces / Service Contracts
- [ ] `IServiceRequestService` — submit, list, update, assign, accept, decline, escalate, add note
- [ ] `IWorkloadService` — get staff/volunteer workload summary, get unassigned queue, get overdue requests
- [ ] `IDonorService` — register, list, get profile, get contribution history
- [ ] `IVolunteerService` — register, list, match to request by location/skill
- [ ] `IPaymentService` — process donation, handle webhook, refund
- [ ] `IResourceService` — log resource donation, allocate, track inventory
- [ ] `IAllocationService` — allocate money/resources to requests, get allocation history
- [ ] `IDashboardService` — aggregate impact stats, money flow, resource flow, geographic breakdown, donation breakdowns (by person, diocese, city, channel, amount band)
- [ ] `INotificationService` — send email, send SMS, queue notification
- [ ] `IReportingService` — generate impact reports, export to CSV/PDF
- [ ] `IUserService` — profile management, role assignment
- [ ] `IReceiptService` — generate and send tax receipts for charitable donations
- [ ] `IAuditService` — write immutable audit log entries
- [ ] `IEventService` — create/manage events, register attendees, process auction bids, generate event revenue reports

### Shared / Value Objects
- [ ] Enums: `UserRole` (PersonInNeed, Donor, Volunteer, Staff, Admin)
- [ ] Enums: `RequestStatus` (Submitted, Triaged, Matched, InProgress, Fulfilled, Closed, Cancelled)
- [ ] Enums: `RequestPriority` (Urgent, High, Normal, Low)
- [ ] Enums: `AssignmentStatus` (Pending, Accepted, Declined, Reassigned, Completed)
- [ ] Enums: `ActivityType` (StatusChanged, Assigned, Reassigned, NoteAdded, DueDateSet, Fulfilled, Cancelled, Escalated)
- [ ] Enums: `RequestCategory` (Food, Clothing, Shelter, Transportation, Medical, Utilities, Financial, Other)
- [ ] Enums: `ResourceType` (Food, Clothing, Shelter, Transportation, Medical, HouseholdGoods, Other)
- [ ] Enums: `DonationChannel` (Online, Check, Cash, InPerson, Mail, PhoneCall, Event, Other) — how the donation was received
- [ ] Enums: `EventType` (Gala, SilentAuction, Dinner, Concert, GolfTournament, Walkathon, Other)
- [ ] Enums: `EventStatus` (Draft, Published, Open, Closed, Completed, Cancelled)
- [ ] Enums: `AuctionItemStatus` (Available, Sold, Unsold)
- [ ] Enums: `ContributionStatus` (Pending, Processed, Failed, Refunded)
- [ ] Enums: `AllocationStatus` (Pending, Allocated, Delivered, Reversed)
- [ ] Value object: `Address` (Street, City, State, Zip, Country)
- [ ] Value object: `GeoLocation` (Latitude, Longitude) — for mapping and volunteer matching
- [ ] Value object: `Money` (Amount, Currency)
- [ ] Value object: `ContactInfo` (Phone, Email, PreferredContact)
- [ ] Value object: `DateRange` — for dashboard filtering
- [ ] Common result types: `Result<T>`, `PagedResult<T>`, `ValidationResult`

---

## Phase 3 — API (Lotv.Api)

**Goal**: Functional REST API with authentication, all core CRUD endpoints, payment processing, and reporting.

### Authentication & Authorization
- [ ] Implement chosen auth strategy
- [ ] Role-based authorization policies (PersonInNeed, Donor, Volunteer, Staff, Admin)
- [ ] JWT token issuance / refresh endpoints
- [ ] User registration endpoint with role selection
- [ ] Password reset flow

### Service Request Endpoints
- [ ] `POST /api/v1/requests` — submit service request (PersonInNeed)
- [ ] `GET /api/v1/requests` — list requests with filters (Staff/Admin)
- [ ] `GET /api/v1/requests/{id}` — get request detail
- [ ] `PUT /api/v1/requests/{id}/status` — update request status (Staff)
- [ ] `PUT /api/v1/requests/{id}/assign` — assign volunteer or staff to request (Staff)
- [ ] `PUT /api/v1/requests/{id}/priority` — set request priority (Staff)
- [ ] `PUT /api/v1/requests/{id}/due-date` — set due date / SLA (Staff)
- [ ] `POST /api/v1/requests/{id}/accept` — volunteer/staff accepts their assignment
- [ ] `POST /api/v1/requests/{id}/decline` — volunteer/staff declines assignment (triggers reassignment queue)
- [ ] `POST /api/v1/requests/{id}/escalate` — escalate request to supervisor (Staff/system)
- [ ] `POST /api/v1/requests/{id}/notes` — add a note to a request (Staff/Volunteer)
- [ ] `GET /api/v1/requests/{id}/notes` — get all notes on a request
- [ ] `GET /api/v1/requests/{id}/activity` — full activity log for a request (who changed what, when)
- [ ] `POST /api/v1/requests/{id}/fulfill` — mark request fulfilled + log resources used (Volunteer/Staff)
- [ ] `GET /api/v1/requests/my` — person in need's own requests
- [ ] `GET /api/v1/requests/queue` — unassigned requests queue (Staff — requests needing someone to take them)
- [ ] `GET /api/v1/requests/overdue` — requests past their due date (Staff/Admin)
- [ ] `GET /api/v1/workload` — workload summary across all staff and volunteers (Admin/Staff): open request count per person
- [ ] `GET /api/v1/workload/{userId}` — workload detail for a specific staff member or volunteer: their assigned requests by status

### Event Endpoints
- [ ] `GET /api/v1/events` — list all events (with filters: upcoming, past, type, status)
- [ ] `POST /api/v1/events` — create a new event (Staff/Admin)
- [ ] `GET /api/v1/events/{id}` — event detail
- [ ] `PUT /api/v1/events/{id}` — update event (Staff/Admin)
- [ ] `DELETE /api/v1/events/{id}` — cancel / delete event (Admin)
- [ ] `GET /api/v1/events/{id}/attendees` — list attendees for an event
- [ ] `POST /api/v1/events/{id}/attendees` — register a donor as attendee / sell ticket
- [ ] `PUT /api/v1/events/{id}/attendees/{attendeeId}/checkin` — check in an attendee at the door
- [ ] `GET /api/v1/events/{id}/revenue` — total revenue raised by the event (tickets + auction + direct donations linked to event)
- [ ] `GET /api/v1/events/{id}/auction` — list all silent auction items for an event
- [ ] `POST /api/v1/events/{id}/auction` — add an auction item (Staff)
- [ ] `PUT /api/v1/events/{id}/auction/{itemId}` — update auction item (bid, status, winner)
- [ ] `POST /api/v1/events/{id}/auction/{itemId}/bid` — place a bid on an auction item
- [ ] `POST /api/v1/events/{id}/auction/close` — close bidding and record winners (Staff/Admin)
- [ ] `GET /api/v1/events/upcoming` — upcoming events (used for public event listing page)
- [ ] `GET /api/v1/dashboard/events` — event dashboard summary: upcoming events, past events revenue, top-performing events

### Diocese Endpoints (Lookup)
- [ ] `GET /api/v1/dioceses` — list all dioceses (Staff/Admin, used in donor registration dropdowns and dashboard)
- [ ] `POST /api/v1/dioceses` — add a diocese (Admin)
- [ ] `PUT /api/v1/dioceses/{id}` — update diocese details (Admin)
- [ ] `GET /api/v1/dioceses/{id}/donors` — all donors associated with a diocese (Staff/Admin)
- [ ] `GET /api/v1/dioceses/{id}/summary` — total donations, donor count, average gift for a diocese (dashboard drill-through)

### Donor & Contribution Endpoints
- [ ] `POST /api/v1/donors` — register donor profile
- [ ] `GET /api/v1/donors` — list donors (Staff/Admin)
- [ ] `GET /api/v1/donors/{id}` — donor profile detail
- [ ] `GET /api/v1/donors/{id}/contributions` — donor's contribution history
- [ ] `GET /api/v1/donors/{id}/impact` — donor's personal impact statement
- [ ] `POST /api/v1/contributions/money` — initiate monetary donation (creates Stripe payment intent)
- [ ] `POST /api/v1/contributions/resources` — log a resource donation
- [ ] `GET /api/v1/contributions` — list all contributions (Staff/Admin)
- [ ] `GET /api/v1/contributions/{id}` — contribution detail

### Payment Endpoints
- [ ] `POST /api/v1/payments/intent` — create Stripe PaymentIntent, return client secret
- [ ] `POST /api/v1/payments/webhook` — receive and process Stripe webhook events (signature verified)
- [ ] `POST /api/v1/payments/{id}/refund` — process refund (Admin)

### Volunteer Endpoints
- [ ] `POST /api/v1/volunteers` — register volunteer
- [ ] `GET /api/v1/volunteers` — list volunteers (Staff/Admin)
- [ ] `GET /api/v1/volunteers/{id}` — volunteer profile
- [ ] `GET /api/v1/volunteers/available` — volunteers available near a request location
- [ ] `GET /api/v1/volunteers/my/requests` — volunteer's assigned requests

### Allocation Endpoints
- [ ] `POST /api/v1/allocations/money` — allocate money → request or expense (Staff/Admin)
- [ ] `POST /api/v1/allocations/resources` — allocate resource donation → request (Staff)
- [ ] `GET /api/v1/allocations` — full allocation ledger (Staff/Admin)
- [ ] `GET /api/v1/allocations/{id}` — allocation detail

### Dashboard & Reporting Endpoints
- [ ] `GET /api/v1/dashboard` — overall impact summary (KPI cards): total $ donated, total resources, people helped, requests fulfilled
- [ ] `GET /api/v1/dashboard/money` — monetary flow breakdown (by category, by region, by time period, by recipient)
- [ ] `GET /api/v1/dashboard/resources` — resource distribution breakdown (by type, by region, by time period)
- [ ] `GET /api/v1/dashboard/map` — geographic distribution data (GeoJSON or lat/lng points for map rendering)
- [ ] `GET /api/v1/dashboard/timeline` — time-series data for charts (donations and fulfillments over time)

#### Donation Tracking Dashboard Endpoints
- [ ] `GET /api/v1/dashboard/donations` — master donations dashboard: all breakdown panels in one response (or use individual endpoints below)
- [ ] `GET /api/v1/dashboard/donations/by-person` — per-donor summary rows (name, diocese, city, total, gift count, avg gift, first/last gift date); supports search + sort + pagination
- [ ] `GET /api/v1/dashboard/donations/by-diocese` — per-diocese aggregate (diocese name, city, state, donor count, total amount, avg gift); sortable
- [ ] `GET /api/v1/dashboard/donations/by-city` — per-city aggregate (city, state, donor count, total amount); sortable
- [ ] `GET /api/v1/dashboard/donations/by-channel` — by DonationChannel (Online / Check / Cash / In-Person / Mail / Event / Other): count, total amount, percentage of all donations
- [ ] `GET /api/v1/dashboard/donations/by-amount` — gift-size band distribution (<$25, $25–$99, $100–$499, $500–$999, $1,000–$4,999, $5,000+): count, total, percentage
- [ ] `GET /api/v1/dashboard/donations/by-diocese/{id}` — drill-through: all donors in a specific diocese with their individual totals

#### Reporting Endpoints
- [ ] `GET /api/v1/reports/impact` — full impact report with filters (date range, region, category)
- [ ] `GET /api/v1/reports/donations` — full donor report filterable by diocese, city, channel, date range, amount range
- [ ] `GET /api/v1/reports/export` — export any report as CSV or PDF (specify report type in query param)
- [ ] `GET /api/v1/reports/audit` — audit log viewer (Admin only)

### User & Profile Endpoints
- [ ] `GET /api/v1/users/me` — current user profile
- [ ] `PUT /api/v1/users/me` — update profile
- [ ] `GET /api/v1/users` — list all users (Admin)
- [ ] `PUT /api/v1/users/{id}/role` — change user role (Admin)
- [ ] `DELETE /api/v1/users/{id}` — deactivate user (Admin)

### Notification Endpoints
- [ ] `POST /api/v1/notifications/send` — send ad-hoc notification (Staff)
- [ ] `GET /api/v1/notifications/templates` — list email templates (Staff)
- [ ] `POST /api/v1/notifications/marketing` — send marketing email blast (Staff)

### Infrastructure
- [ ] EF Core DbContext + initial migrations
- [ ] Repository pattern (or direct DbContext — per ADR)
- [ ] Stripe SDK integration (payment processing + webhook verification)
- [ ] Email service integration (SendGrid or chosen provider)
- [ ] SMS service integration (optional — Twilio or chosen provider)
- [ ] Blob storage integration for receipts and documents
- [ ] Background job infrastructure (Hangfire or hosted service) for email queue, receipt generation
- [ ] `IReceiptService` implementation — generate PDF tax receipt, email to donor
- [ ] `IAuditService` implementation — write to append-only audit log table
- [ ] Serilog structured logging
- [ ] Health check endpoint (`/health`)
- [ ] Global exception handler middleware
- [ ] Input validation (FluentValidation recommended)
- [ ] Rate limiting on public endpoints (payment, registration)
- [ ] CORS policy configuration

---

## Phase 4 — Frontend (Lotv.Web)

**Goal**: Blazor WebAssembly UI with role-based views, donation flow, and Impact & Distribution Dashboard.

### Shared / Shell
- [ ] App shell with responsive navigation
- [ ] Authentication state provider (JWT handling, auto-refresh)
- [ ] Role-based route guards
- [ ] Notification toast component
- [ ] Loading/spinner component
- [ ] Reusable chart components (bar, pie, line) — consider MudBlazor or Radzen

### Person in Need Views
- [ ] Submit service request form (category, description, address, contact info)
- [ ] My Requests dashboard (list with status badges)
- [ ] Request detail / status tracking page
- [ ] Profile management page

### Donor Views
- [ ] Donor registration / profile page
- [ ] Make a Monetary Donation page (Stripe Elements / payment form)
- [ ] Donate Resources page (log resource type, quantity, description)
- [ ] Donation confirmation / receipt page (with PDF download)
- [ ] Contribution history list
- [ ] **My Impact page** — personalized impact statement: "Your $X and Y resources helped N people in [regions]", breakdown by category and time

### Volunteer Views
- [ ] Volunteer registration / profile (including location + skills)
- [ ] Available Requests near me (map + list view, filtered by location radius)
- [ ] **Pending Assignment** — notification + accept/decline screen when staff assigns the volunteer to a request
- [ ] **My Work Queue** — requests currently assigned to me, sorted by priority + due date; status badges; overdue indicator
- [ ] Request detail view (read-only, with notes thread)
- [ ] Complete/report a request (log outcome and resources used)
- [ ] My History

### Staff Views
- [ ] **All Requests dashboard** — filterable/sortable list: status, priority, category, region, date, assigned-to; status badge coloring; overdue indicator
- [ ] **Unassigned Queue** — dedicated view of requests with no assignee, sorted by priority + age; one-click assign
- [ ] **Kanban Board View** — requests organized in columns by status (Submitted | Triaged | Matched | In Progress | Fulfilled); drag or button to move cards between columns
- [ ] **Request detail / case management page**:
  - Assign or reassign volunteer/staff
  - Set priority and due date
  - Change status
  - Notes thread (internal notes, visible to staff/volunteer only)
  - Full activity log (who changed what, when)
  - Resources allocated to this request
  - Money allocated to this request
- [ ] **Workload View** — table or card grid showing each staff member and volunteer with their open request count, in-progress count, overdue count; click through to their queue
- [ ] **My Work Queue** (for Staff) — requests currently assigned to the logged-in staff member, sorted by priority + due date
- [ ] Donor Management list (search, sort, view contribution totals)
- [ ] Volunteer Management list (map view + list view)
- [ ] Allocate money → request/expense form
- [ ] Allocate resources → request form
- [ ] Allocation ledger view (full history of where money + resources went)
- [ ] Send targeted notification to user(s)
- [ ] Marketing email composer + send

### Impact & Distribution Dashboard (Staff / Admin / Public)
- [ ] **KPI Summary Cards**: Total money donated, total resources donated, total people helped, total requests fulfilled (all-time and filtered by date range)
- [ ] **Money Flow panel**: Where money was sent — breakdown by request category, by geographic region, by time period; drill-down to individual allocations
- [ ] **Resource Distribution panel**: Where resources went — breakdown by resource type, by region, by time period; drill-down to individual allocations
- [ ] **Geographic Map**: Interactive map showing service delivery points (where requests were fulfilled), color-coded by category or amount; uses volunteer and request GeoLocation data
- [ ] **Timeline Chart**: Line/bar chart of donations received vs. requests fulfilled over time (configurable date range)
- [ ] **Category Breakdown Chart**: Pie/donut chart of spending/resources by category (Food, Shelter, Transportation, etc.)
- [ ] **Top Regions**: Ranked list or heat map of highest-need / most-served geographic areas
- [ ] **Allocation Ledger Table**: Sortable, filterable table showing every money or resource allocation: donor → contribution → request → recipient
- [ ] **Export**: Download dashboard data as CSV or PDF report
- [ ] **Date Range Filter**: Filter all dashboard panels by date range
- [ ] **Public Transparency Page**: Stripped-down read-only version of dashboard visible to unauthenticated users (shows aggregate totals only, no PII)

### Donation Tracking Dashboard (Staff / Admin)
*Tracks who donated, how much, from where, and how the donation came in.*

- [ ] **By Person panel** — searchable, sortable table of every donor: Name, Diocese, City, State, Total Donated, Gift Count, Average Gift, First Gift Date, Last Gift Date; click row to drill into that donor's full contribution history
- [ ] **By Diocese panel** — table grouped by diocese: Diocese Name, City, State, Total Donors, Total Amount, Average Gift; click row to see all donors in that diocese
- [ ] **Diocese Map** — geographic map with pins or heat map showing donor concentration by diocese location
- [ ] **By City panel** — table grouped by city/state: City, State, Total Donors, Total Amount; sortable by amount or donor count
- [ ] **By Channel panel** — pie/donut chart of how donations came in: Online, Check, Cash, In-Person, Mail, Phone, Event, Other; shows count and total $ per channel
- [ ] **By Amount panel** — gift-size distribution bar chart: <$25 / $25–$99 / $100–$499 / $500–$999 / $1,000–$4,999 / $5,000+; shows gift count and total per band
- [ ] **Full Donor Ledger** — paginated, filterable master list of every contribution: donor name, diocese, city, amount, channel, date, status; supports filter by diocese / city / channel / date range / amount range
- [ ] **Donor Detail Drawer/Page** — click any donor to see: profile info, diocese, city, all contributions with dates and channels, total lifetime giving, impact statement

### Event Management Dashboard (Staff / Admin)
*Tracks fundraising events — galas, silent auctions, and other events — and connects attendance and revenue back to the donor tracking dashboard.*

- [ ] **Upcoming Events list** — all scheduled events with date, type, location, goal amount, tickets sold, RSVPs
- [ ] **Event detail page** — per-event view: description, date/time, venue, ticket price(s), capacity, current attendee count, revenue raised to date
- [ ] **Past Events panel** — completed events with final attendance, total revenue raised, comparison to goal
- [ ] **Event Revenue widget** (on main Donation Dashboard) — total raised through events vs. direct donations; event contribution visible in By Channel panel as "Event"

### Event Management Views (Staff / Admin)
- [ ] **Events list page** — upcoming and past events; filter by type, status, date; KPI strip: total events, total revenue raised, total attendees
- [ ] **Create / Edit Event form** — name, type (Gala / Silent Auction / Dinner / etc.), date/time, venue, address, capacity, ticket price, goal amount, description
- [ ] **Event detail / management page**:
  - Summary: date, venue, ticket sales vs. capacity, revenue raised vs. goal, RSVP count
  - Attendee list: searchable table (name, diocese, city, tickets, amount paid, checked-in status); check-in button
  - Silent auction tab (if EventType includes auction): item list, current bids, add item, close bidding, winner list
  - Revenue breakdown: ticket sales + auction proceeds + direct donations linked to this event
  - Export attendee list (CSV)
- [ ] **Public Event Page** — public-facing event listing with RSVP / ticket purchase; visible to unauthenticated users
- [ ] **Donor RSVP / Ticket Purchase flow** — donor selects event, quantity, pays via Stripe; confirmation email with ticket

### Admin Views
- [ ] User management (list, search, change role, deactivate)
- [ ] Diocese management (list, add, edit dioceses)
- [ ] System configuration page
- [ ] Audit log viewer (filterable)
- [ ] Platform health / system status page

---

## Phase 5 — Testing

**Goal**: Meaningful test coverage for all critical paths.

### Unit Tests (Lotv.Core logic)
- [ ] `ServiceRequestService` — submit, state transitions, validation, assignment, escalation
- [ ] `WorkloadService` — workload aggregation, overdue detection, unassigned queue logic
- [ ] `AllocationService` — allocate money, allocate resources, prevent over-allocation
- [ ] `DashboardService` — aggregate stats, money flow, resource flow calculations
- [ ] `ReportingService` — report generation logic
- [ ] `ReceiptService` — tax receipt content generation
- [ ] Value object tests: `Money`, `GeoLocation`, `Address`
- [ ] Enum/domain rule tests

### Unit Tests (Lotv.Api)
- [ ] Controller tests for all endpoint groups (mocked services)
- [ ] Stripe webhook handler — signature verification, event routing
- [ ] Authorization attribute / policy tests

### Integration Tests
- [ ] API integration tests using `WebApplicationFactory` + in-memory or test database
- [ ] Full request submission → fulfillment → allocation flow
- [ ] Donor registration → monetary contribution → receipt generation flow
- [ ] Dashboard endpoint returns correct aggregates

### Test Infrastructure
- [ ] Test data builder / factory (consistent test entity creation)
- [ ] Seed data strategy for integration tests
- [ ] Test coverage reporting (Coverlet)
- [ ] Coverage target: ≥ 80% on Lotv.Core

### Optional
- [ ] E2E browser tests (Playwright) for critical user flows

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
- [ ] Write `Dockerfile` for Lotv.Api
- [ ] Write `Dockerfile` for Lotv.Web (static hosting or ASP.NET hosted)
- [ ] Write `docker-compose.yml` for local dev (API + Web + DB + Redis)

### CI/CD
- [ ] GitHub Actions workflow: build + test on every PR
- [ ] GitHub Actions workflow: deploy to staging on merge to `main`
- [ ] GitHub Actions workflow: deploy to production on release tag
- [ ] Environment configuration: `dev / staging / prod` via environment variables
- [ ] Database migration step in deployment pipeline

### Payment Processor Setup
- [ ] Register Stripe account (or chosen provider)
- [ ] Configure Stripe webhook endpoint in Stripe dashboard
- [ ] Store Stripe keys in secrets manager
- [ ] Enable Stripe test mode for staging

### Monitoring & Reliability
- [ ] Application Insights or equivalent (structured logs + traces + metrics)
- [ ] Alerts: error rate spike, payment failure spike, high latency
- [ ] Uptime monitoring (external probe)
- [ ] Database backup strategy (automated daily backups, retention policy)
- [ ] Disaster recovery runbook in `docs/`

### Security Hardening
- [ ] HTTPS enforced everywhere
- [ ] Security headers (HSTS, X-Content-Type-Options, CSP)
- [ ] Rate limiting on payment and auth endpoints
- [ ] OWASP Top 10 review before launch
- [ ] Dependency vulnerability scan (Dependabot or `dotnet list package --vulnerable`)

### Launch
- [ ] Domain setup and SSL certificate
- [ ] DNS configuration
- [ ] Smoke test checklist
- [ ] Launch checklist sign-off

---

## Discovered / Backlog

*Tasks that don't fit a phase yet, or are post-launch improvements.*

### Financial & Compliance
- [ ] Tax receipt / charitable receipt PDF generation (IRS-compliant for US nonprofits)
- [ ] Payment reconciliation report (compare Stripe records vs. internal contribution records)
- [ ] Donor anonymity option (donor can opt out of public recognition)
- [ ] Financial audit export (for external accountants)
- [ ] GDPR / CCPA compliance review and PII handling policy

### Operations
- [ ] Audit logging — immutable append-only record of all financial allocations (who sent what to where)
- [ ] Resource inventory management (track stock levels of donated physical goods)
- [ ] Marketing email template design and branding
- [ ] Onboarding flows for each user type (guided first-login experience)
- [ ] In-app help / FAQ

### Quality & Accessibility
- [ ] Accessibility audit (WCAG 2.1 AA)
- [ ] Mobile responsiveness review (all views)
- [ ] Performance profiling of dashboard aggregate queries (index strategy for large datasets)
- [ ] Localization / i18n (if serving non-English speakers)

### Future Features
- [ ] Recurring donations (Stripe subscriptions)
- [ ] Wish list / in-kind donation requests (person in need requests specific goods)
- [ ] Volunteer scheduling / calendar
- [ ] SMS check-in for volunteers on active requests
- [ ] Public API for third-party integrations (partner organizations)
- [ ] Online bidding for silent auction (real-time via SignalR — bidders see live updates)
- [ ] Event QR code check-in (scan QR on ticket to check in attendees)
- [ ] Sponsorship tracking (corporate sponsors for events, linked to donor record)
- [ ] Pledge management (donor pledges a future gift, tracked until fulfilled)
