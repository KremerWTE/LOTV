# Statement of Work
## SOW[####]: Lily of the Valley (LOTV) — Full Platform Development

**FOR INTERNAL PURPOSES ONLY**

---

| Field | Detail |
|---|---|
| **Statement of Work #** | SOW[####] |
| **Issue Date** | [DATE] |
| **Project Name** | Lily of the Valley (LOTV) — SaaS Social Services Coordination Platform |
| **Governed By** | Master Services Agreement MSA[####] between WTE Solutions and [CLIENT ORG] |
| **Project Manager** | Chris Kremer |
| **Lead Developer** | [WTE Lead Developer Name] |

---

## Client Information

| Field | Detail |
|---|---|
| **Client** | [CLIENT ORGANIZATION LEGAL NAME] |
| **Primary Contact** | [Name], [Title] |
| **Email Address** | [Email] |
| **Phone #** | [Phone] |
| **Billing Contact** | [Name] |
| **Billing Email** | [Email] |
| **Billing Address** | [Street, City, State, ZIP] |
| **Business EIN #** | On File |
| **Site URL (future)** | [e.g., app.lilyofthevalley.org] |

---

## Project Overview

WTE Solutions will design, develop, test, and deploy the **Lily of the Valley (LOTV)** platform — a .NET 9 SaaS application for social services coordination. The platform connects five user types (People in Need, Donors, Volunteers, Staff, and Administrators) and provides:

- Service request intake, routing, assignment, and fulfillment with full task management
- Monetary and resource donation tracking with tax receipt generation
- Fundraising event management (galas, silent auctions, dinners, and other events)
- Donation Tracking Dashboard by person, diocese, city, donation channel, and amount
- Impact & Distribution Dashboard showing where money and resources were sent
- Geographic maps for volunteer matching and service delivery visualization
- Staff kanban board, workload view, and request queue
- Stripe payment processing for donations and event tickets
- Email and SMS notifications for all user types

---

## Technology Stack

| Layer | Technology |
|---|---|
| Backend API | ASP.NET Core Web API (.NET 9) |
| Frontend | Blazor WebAssembly (.NET 9) |
| Domain Logic | .NET 9 Class Library |
| Testing | xUnit, WebApplicationFactory, Playwright (E2E) |
| Database | EF Core + [SQL Server / PostgreSQL — TBD in Phase 1] |
| Authentication | [ASP.NET Core Identity + JWT / Azure AD B2C — TBD in Phase 1] |
| Payment Processing | Stripe |
| Email | [SendGrid / Azure Communication Services — TBD in Phase 1] |
| Cloud Hosting | [Azure App Services / Azure Container Apps — TBD in Phase 1] |
| File / Blob Storage | [Azure Blob Storage — TBD in Phase 1] |
| CI/CD | GitHub Actions |
| Repository | GitHub |

---

## Scope of Work

Work is organized into six (6) phases, each representing a billable milestone. Detailed task breakdowns are maintained in `MASTER_TODO.md` in the project repository.

---

### Phase 1 — Architecture & Design
**Deliverable:** All major technical decisions documented and approved in writing before coding begins.

**WTE Deliverables:**
- Domain model and entity relationship diagram (ERD) documented in `docs/data-model.md`
- REST API contract document (all endpoints, auth flow, request/response shapes) in `docs/api-contract.md`
- Authentication strategy selection and documentation (`docs/auth-design.md`)
- Database and ORM selection with rationale
- Architecture Decision Records (ADRs) for: authentication, database, payment processor, and multi-tenancy
- Diocese data model definition (diocese as lookup entity linked to donor profiles)
- Event revenue model definition (how ticket/auction revenue links to contribution records)
- Silent auction workflow definition (bidding model, winner notification, payment collection)
- Payment processor account setup guidance
- Escalation rules definition for overdue service requests

**Client Deliverables Required:**
- Approved list of diocese names, cities, and states to seed the initial database
- Organization name, logo, and branding assets
- Confirmation of hosting preference (Azure, AWS, or other)
- Stripe account creation (or authorization for WTE to set up on Client's behalf)
- Email provider account creation (or authorization for WTE to set up)
- Written approval of architecture decisions before Phase 2 begins

---

### Phase 2 — Core Domain (Lotv.Core)
**Deliverable:** All domain entities, enumerations, value objects, and service interfaces defined, implemented, and unit-tested.

**WTE Deliverables:**

*Lookup Entity:*
- `Diocese` entity seeded with Client-provided diocese list

*User Entities:*
- `ApplicationUser`, `PersonInNeed`, `Donor` (with DioceseId, City, State), `Volunteer`, `Employee/StaffMember`

*Request & Task Management Entities:*
- `ServiceRequest` with Priority, DueDate, AssignedToId, AssignedToType
- `ServiceFulfillment`, `RequestNote`, `RequestActivity`, `RequestAssignment`

*Donation & Allocation Entities:*
- `MonetaryContribution` with DonationChannel (Online / Check / Cash / InPerson / Mail / PhoneCall / Event / Other), CheckNumber, EventId
- `ResourceDonation`, `MoneyAllocation`, `ResourceAllocation`, `Expense`

*Event Entities:*
- `FundraisingEvent`, `EventAttendee`, `SilentAuctionItem`, `AuctionBid`

*Reporting / Dashboard DTOs:*
- `ImpactSummary`, `DonorImpactStatement`, `AllocationRecord`
- `DonationByPersonRow`, `DonationByDioceseRow`, `DonationByCityRow`, `DonationByChannelRow`, `DonationByAmountBand`

*Enumerations:* UserRole, RequestStatus, RequestPriority, AssignmentStatus, ActivityType, RequestCategory, ResourceType, DonationChannel, ContributionStatus, AllocationStatus, EventType, EventStatus, AuctionItemStatus

*Value Objects:* Address, GeoLocation, Money, ContactInfo, DateRange

*Service Interfaces:* IServiceRequestService, IWorkloadService, IDonorService, IVolunteerService, IPaymentService, IResourceService, IAllocationService, IDashboardService, INotificationService, IReportingService, IUserService, IReceiptService, IAuditService, IEventService

*Shared:* Result\<T\>, PagedResult\<T\>, ValidationResult

*Unit Tests:* ≥ 80% coverage on Lotv.Core including ServiceRequestService, AllocationService, DashboardService, WorkloadService, ReceiptService

**Client Deliverables Required:**
- Written approval of domain model/ERD before entity coding begins
- Business rules for any custom logic (e.g., specific request routing rules, tax receipt format requirements)

---

### Phase 3 — API (Lotv.Api)
**Deliverable:** Fully functional, authenticated, and authorized REST API covering all platform capabilities.

**WTE Deliverables:**

*Authentication & Authorization:*
- User registration with role selection, JWT issuance/refresh, password reset
- Role-based authorization policies: PersonInNeed, Donor, Volunteer, Staff, Admin

*Service Request & Task Management Endpoints:*
- Full CRUD + status, priority, due date, assignment, accept/decline, escalation, notes, activity log
- Unassigned queue, overdue queue, workload endpoints

*Donor, Contribution & Diocese Endpoints:*
- Donor CRUD, contribution history, personal impact statement
- Resource donation logging, full contribution list
- Diocese lookup CRUD + donor list by diocese + summary

*Payment Endpoints:*
- Stripe PaymentIntent creation, webhook receiver (with signature verification), refund

*Volunteer Endpoints:*
- Registration, profile, available-near-location, assigned requests

*Allocation Endpoints:*
- Money → request/expense allocation, resource → request allocation, ledger

*Event Endpoints:*
- Event CRUD, attendee registration, check-in, auction items, bidding, close auction, revenue summary

*Dashboard & Reporting Endpoints:*
- Impact summary, money flow, resource distribution, geographic map, timeline
- Donation tracking: by person, by diocese, by city, by channel, by amount band, diocese drill-through
- Event dashboard summary
- Impact report, full donor report, export (CSV/PDF), audit log

*Infrastructure:*
- EF Core DbContext + initial migrations, diocese seed data
- Stripe SDK integration, email service integration, SMS integration (optional)
- Blob storage for tax receipts and documents
- Background job infrastructure for email queue and receipt generation
- FluentValidation, Serilog, global exception handler, rate limiting, CORS, health check endpoint

*Unit and Integration Tests:*
- Controller unit tests for all endpoint groups
- Stripe webhook handler tests
- Integration tests: full request → fulfillment → allocation flow; donation → receipt flow; dashboard endpoint aggregate accuracy

**Client Deliverables Required:**
- Stripe account keys (test environment)
- Email provider API key
- Confirmation of database hosting (Azure SQL / PostgreSQL)
- Test donor and diocese data for integration testing

---

### Phase 4 — Frontend (Lotv.Web)
**Deliverable:** Blazor WebAssembly application with all role-based views, dashboards, and event management.

**WTE Deliverables:**

*App Shell:*
- Responsive navigation, authentication state provider, role-based route guards, toast notifications, loading components, reusable chart components

*Person in Need Views:*
- Submit request form, My Requests dashboard, request status tracking, profile management

*Donor Views:*
- Donor registration/profile, monetary donation form (Stripe Elements), resource donation form, confirmation/receipt page (PDF download), contribution history, My Impact page

*Volunteer Views:*
- Registration/profile, available requests near me (map + list), pending assignment accept/decline screen, My Work Queue, request completion form, history

*Staff Views:*
- All Requests dashboard (filterable/sortable with status badges and overdue indicators)
- Unassigned Queue with one-click assign
- **Kanban Board** — requests in columns by status (Submitted | Triaged | Matched | In Progress | Fulfilled)
- Request detail / case management (assign, priority, due date, status, notes thread, activity log, allocations)
- **Workload View** — open/in-progress/overdue counts per staff member and volunteer
- My Work Queue (staff's own assigned requests)
- Donor management, Volunteer management, Allocation ledger
- Marketing email composer + send

*Impact & Distribution Dashboard:*
- KPI summary cards, money flow panel, resource distribution panel, geographic map, timeline chart, category breakdown chart, allocation ledger table, date range filter, export, public transparency page

*Donation Tracking Dashboard:*
- **By Person** — searchable/sortable donor table with diocese, city, total, gift count, avg gift
- **By Diocese** — aggregate table + geographic map with diocese location pins
- **By City** — city/state aggregate table
- **By Channel** — pie/donut chart (Online / Check / Cash / In-Person / Mail / Event / Other)
- **By Amount** — gift-size distribution bar chart (<$25 / $25–$99 / $100–$499 / $500–$999 / $1,000–$4,999 / $5,000+)
- Full Donor Ledger — paginated, filterable master contribution list
- Donor detail drawer — full profile, diocese, all contributions, lifetime giving, impact

*Event Management Views:*
- Events list with KPI strip, Create/Edit event form, Event detail page (attendee list, check-in, auction tab, revenue breakdown), public event listing + RSVP/ticket purchase flow, confirmation email with ticket

*Admin Views:*
- User management, Diocese management, system configuration, audit log viewer, platform health

**Client Deliverables Required:**
- Approved UI mockups or wireframe feedback (WTE will produce initial wireframes for review)
- Logo and branding assets (colors, fonts, imagery guidelines)
- Content for public-facing pages (About, FAQ, public event listings)
- Domain name and DNS access for deployment

---

### Phase 5 — Testing
**Deliverable:** Documented test coverage report demonstrating ≥ 80% coverage on Lotv.Core; all critical path integration tests passing; E2E smoke tests passing in staging environment.

**WTE Deliverables:**
- Unit tests: ServiceRequestService, AllocationService, DashboardService, WorkloadService, ReceiptService, value object tests (complete)
- Integration tests: full request lifecycle, donation + receipt flow, dashboard aggregates, payment webhook handling
- Test data builder/factory with seed data strategy
- Coverlet coverage report
- E2E smoke tests (Playwright) for: submit request, make donation, event RSVP, staff request assignment, dashboard load

**Client Deliverables Required:**
- Staging environment approval and feedback within five (5) business days of WTE notifying readiness
- User acceptance testing (UAT) participation from at least one representative per user role

---

### Phase 6 — Deployment & Launch
**Deliverable:** Application live in production with CI/CD pipeline, monitoring, and security hardening complete.

**WTE Deliverables:**
- Dockerfile(s) for Lotv.Api and Lotv.Web; docker-compose.yml for local dev
- GitHub Actions CI/CD: build+test on PR, deploy to staging on main merge, deploy to production on release tag
- Environment configuration: dev / staging / production
- Database migration pipeline step
- Azure Blob Storage configuration for receipts and documents
- CDN configuration for Blazor WASM static assets
- Secrets management configuration (Azure Key Vault or equivalent)
- Redis setup (if chosen for caching)
- Stripe webhook registration in production Stripe dashboard
- Application Insights / monitoring setup: error rate alerts, payment failure alerts, uptime monitoring
- Database backup strategy implementation
- HTTPS enforcement + security headers (HSTS, X-Content-Type-Options, CSP)
- OWASP Top 10 pre-launch review
- Dependency vulnerability scan (Dependabot)
- DNS configuration and SSL certificate
- Launch checklist sign-off document
- Disaster recovery runbook in `docs/`
- Handoff documentation: deployment guide, environment variable list (without secrets), architecture diagram

**Client Deliverables Required:**
- Hosting account access (Azure / AWS subscription)
- Domain registrar access for DNS configuration
- Production Stripe account keys
- Production email provider keys
- Final launch approval sign-off

---

## Client Deliverables Summary

The following items must be provided by Client to enable WTE to execute this SOW. Delays in Client-provided items may impact project schedule with no penalty to WTE.

| # | Deliverable | Required For | Priority |
|---|---|---|---|
| 1 | Diocese list (name, city, state) | Phase 2 | High |
| 2 | Organization name, logo, branding assets | Phase 4 | High |
| 3 | Stripe account (test keys by Phase 3; production keys by Phase 6) | Phase 3 | High |
| 4 | Email provider account and API key | Phase 3 | High |
| 5 | Cloud hosting account (Azure or other) | Phase 6 | High |
| 6 | Domain name and DNS access | Phase 6 | High |
| 7 | Written approval of architecture decisions (Phase 1 output) | Phase 2 start | High |
| 8 | Written approval of domain model/ERD | Phase 2 start | High |
| 9 | Business rules for custom logic (routing, tax receipts, etc.) | Phase 2 | Medium |
| 10 | Wireframe / UI feedback (WTE-provided for review) | Phase 4 | Medium |
| 11 | UAT participation (one representative per user role) | Phase 5 | High |
| 12 | Content for public-facing pages | Phase 4 | Medium |
| 13 | Test donor, diocese, and request data for integration testing | Phase 3 | Medium |
| 14 | Launch approval sign-off | Phase 6 | High |

---

## Milestones and Payment Schedule

| Milestone | Description | Amount | Due |
|---|---|---|---|
| **M0 — Project Start / Deposit** | Agreement executed, project kickoff scheduled | $[AMOUNT] | Upon SOW execution |
| **M1 — Architecture Approved** | Phase 1 complete; architecture decisions approved in writing | $[AMOUNT] | Upon Client acceptance |
| **M2 — Core Domain Complete** | Phase 2 complete; entities, interfaces, unit tests passing | $[AMOUNT] | Upon Client acceptance |
| **M3 — API Complete** | Phase 3 complete; all endpoints functional, integration tests passing | $[AMOUNT] | Upon Client acceptance |
| **M4 — Frontend Complete** | Phase 4 complete; all views functional in staging | $[AMOUNT] | Upon Client acceptance |
| **M5 — Testing Complete** | Phase 5 complete; coverage report delivered, UAT sign-off | $[AMOUNT] | Upon Client acceptance |
| **M6 — Production Launch** | Phase 6 complete; application live; handoff docs delivered | $[AMOUNT] | Upon launch |
| **TOTAL** | | **$[TOTAL]** | |

*Milestone amounts to be determined based on WTE Rate Card (Attachment C) and final scope agreement. Time & materials projects will be invoiced monthly for hours worked.*

---

## Hourly Rates (if Time & Materials)

Per Attachment C of the governing MSA. Emergency and after-hours work billed at 2x standard rate.

---

## Assumptions and Exclusions

**Assumptions:**
- Client will use GitHub for source code repository; WTE will manage the repository
- Cloud hosting will be on Microsoft Azure unless otherwise agreed in writing
- English language only (no localization) unless explicitly added to scope
- Stripe will be used for payment processing
- A single production environment; staging is included
- Client will provide a responsive feedback turnaround of five (5) business days on milestone reviews

**Exclusions (not included in this SOW without separate agreement):**
- Mobile native applications (iOS/Android) — platform is web-based and responsive
- Real-time auction bidding via SignalR (identified as a future feature; can be added via change order)
- Recurring donation subscriptions (Stripe subscriptions — future feature)
- Third-party CRM integration (e.g., Salesforce)
- Content creation (copywriting, photography, video)
- Volunteer background check integration
- Custom SMS provider integration (email notifications are included; SMS is optional add-on)
- Hardware, devices, or on-premises infrastructure
- Training beyond standard handoff documentation

---

## Change Order Process

Any changes to scope, timeline, or budget must be agreed upon in writing via a signed Change Order before work begins. WTE will provide a written estimate for any requested change within five (5) business days of the written request. Changes that reduce scope will be addressed through a credit memo or adjusted milestone amount.

---

## Payment Terms

Billing information is on file. WTE will invoice upon completion and written acceptance of each milestone defined above. For time & materials work, invoices are issued monthly for hours worked in the prior month.

This Statement of Work is governed by Master Services Agreement MSA[####] between the parties, which is hereby incorporated by reference. By signing below, both parties agree to the scope, deliverables, milestones, and payment terms stated above.

---

## Signatures

| WTE Solutions, a PointShop, Inc. Company | [CLIENT ORGANIZATION NAME] |
|---|---|
| Signature: _________________________ | Signature: _________________________ |
| Name: Eric Garrison | Name: _________________________ |
| Title: President | Title: _________________________ |
| Date: _________________________ | Date: _________________________ |

*PLEASE RETURN THIS SIGNED STATEMENT OF WORK VIA ADOBE SIGN. YOUR PROJECT WILL BE SCHEDULED ONCE YOUR SIGNED AGREEMENT HAS BEEN ACCEPTED AND ANY PRE-PAYMENT TERMS HAVE BEEN SATISFIED. WTE WILL CONTACT YOU IN THE EVENT OF CHANGES TO THE ESTIMATED COST OF SERVICES.*

---

| Document Preparer | Chris Kremer | Project Manager | Chris Kremer |
|---|---|---|---|
