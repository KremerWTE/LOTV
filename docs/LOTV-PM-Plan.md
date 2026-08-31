# LOTV — Project Management Plan
**Lily of the Valley | SaaS Social Services Coordination Platform**
**Document Version:** 1.0 | **Date:** 2026-03-02 | **Status:** Active
**Project:** SOW-LOTV-001-FullPlatform | **Governed By:** MSA-LOTV

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Project Overview](#2-project-overview)
3. [Stakeholders & Roles](#3-stakeholders--roles)
4. [Project Governance](#4-project-governance)
5. [Scope Management](#5-scope-management)
6. [Work Breakdown Structure](#6-work-breakdown-structure)
7. [Schedule & Milestones](#7-schedule--milestones)
8. [Resource Plan](#8-resource-plan)
9. [Risk Register](#9-risk-register)
10. [Communication Plan](#10-communication-plan)
11. [Change Management](#11-change-management)
12. [Quality Plan](#12-quality-plan)
13. [Dependency Map](#13-dependency-map)
14. [Budget & Payment Schedule](#14-budget--payment-schedule)
15. [Client Deliverables Tracker](#15-client-deliverables-tracker)
16. [Assumptions & Constraints](#16-assumptions--constraints)
17. [Exclusions](#17-exclusions)
18. [Definition of Done](#18-definition-of-done)
19. [Handoff & Closeout](#19-handoff--closeout)

---

## 1. Executive Summary

LOTV (Lily of the Valley) is a platform for a **centralized nonprofit** that coordinates social services delivery through a **National HQ → Local Chapters** organization. It connects people in need, donors, volunteers, and staff through a unified system for service request routing, donation tracking, fundraising event management, and real-time impact reporting.

The platform is built on .NET 9 with an ASP.NET Core REST API backend, a Blazor Server frontend (converted from Blazor WebAssembly 2026-08-31 — see R-22 — so both apps share the same Kestrel/ASP.NET Core hosting model), and a shared domain library. Hosting targets Microsoft Azure. Payments are processed via Stripe. Three key automation features differentiate the platform: **auto-assignment** of volunteers to requests by location + skills, a **real-time operations board** via SignalR, and **scheduled digest reports** emailed to HQ and chapter leads. Development is organized into six phases from foundation through production launch, governed by MSA-LOTV and SOW-LOTV-001.

The engagement spans **four service tracks** delivered in parallel with the platform build: (1) **Low-Hanging Tech** — Microsoft nonprofit rate consolidation, newsletter deliverability hardening (DMARC/DKIM), HIPAA-compliant web form migration, and distribution list audit; (2) **Web Redevelopment** — rebuild lotvministry.org on a mobile-friendly CMS and create a dedicated donor-facing fundraising site; (3) **Content Creation** — staff directory, blog/resource library, and social media automation; (4) **Platform Development** — the full custom .NET 9 SaaS dashboard application.

**Current Status:** Phase 0 (Foundation) complete. Phase 1 (Architecture & Design) is the immediate next step.

| Item | Detail |
|---|---|
| Platform | .NET 9 — ASP.NET Core + Blazor Server (converted from WASM 2026-08-31, see R-22) |
| Org Model | Centralized nonprofit: National HQ → Local Chapters (2-tier) |
| Repository | `github.com/KremerWTE/LOTV` — branch `kremer-dev` |
| Solution File | `Lotv.slnx` (new XML-based .NET 9 format) |
| Hosting | Microsoft Azure (App Service, Blob Storage, Key Vault) |
| Payments | Stripe (PaymentIntent, webhooks, refunds) |
| Real-Time | SignalR — `RequestsHub` broadcasts operations board events |
| Scheduler | Hangfire — daily digest + weekly summary report jobs |
| Phases | 6 phases: Foundation → Architecture → Domain → API → Frontend → Testing → Launch |
| Roles | Person in Need, Donor, Volunteer, ChapterStaff, ChapterAdmin, HQAdmin |
| API Endpoints | 60+ REST endpoints + SignalR hub |
| Domain Entities | 35+ entities across 5 domain groups |

---

## 2. Project Overview

### 2.1 Platform Purpose

LOTV is operated by a single centralized nonprofit organization structured as **National HQ → Local Chapters**. HQ has full visibility across all chapters; each chapter operates its own service area and sees only its own data.

The platform enables:
- **Chapters** to receive and auto-route service requests from people in need to qualified local volunteers
- **Donors** to make monetary and resource donations, receive receipts, and see their personal impact
- **Volunteers** to be automatically matched to requests by proximity and skills — no manual dispatch bottleneck
- **Chapter Staff/Admins** to oversee a **real-time operations board** (live Kanban via SignalR) of all requests in their chapter
- **HQ Admins** to see a cross-chapter dashboard with per-chapter drill-down and roll-up metrics
- **All leaders** to receive **automated daily digests and weekly summaries** via email — no manual report generation
- Fundraising event management with ticket sales, silent auctions, and check-in
- Full financial tracking: donations, allocations, receipts, and donor impact

### 2.2 Solution Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│  Client Layer                                                    │
│  Browser / PWA  ←→  Lotv.Web (Blazor Server :5205 dev)          │
│  SignalR Client (Microsoft.AspNetCore.SignalR.Client)            │
└──────────────────────────┬───────────────────────────────────────┘
                           │ HTTP/HTTPS REST  +  WebSocket (SignalR)
┌──────────────────────────▼───────────────────────────────────────┐
│  Application Layer                                               │
│  Lotv.Api (ASP.NET Core :7072)                                   │
│  JWT Auth (6 roles, chapter-scoped) · FluentValidation           │
│  Serilog · Rate Limiting · Chapter Query Filter Middleware        │
│  ┌─────────────────┐   ┌──────────────────┐                     │
│  │  REST Controllers│   │  RequestsHub      │ ← SignalR           │
│  │  60+ endpoints  │   │  /hubs/requests   │   per-chapter       │
│  └─────────────────┘   └──────────────────┘   groups + hq-all   │
│  ┌────────────────────────────────────────┐                     │
│  │  Background Jobs (Hangfire)             │                     │
│  │  AutoAssignmentJob · DailyDigestJob     │                     │
│  │  WeeklySummaryJob · HQWeeklyReportJob   │                     │
│  └────────────────────────────────────────┘                     │
└──────────┬────────────────────────────────┬──────────────────────┘
           │ references                     │ references
┌──────────▼──────────────┐   ┌─────────────▼───────────────────┐
│  Lotv.Core              │   │  Lotv.Tests (xUnit + Playwright) │
│  Entities · Interfaces  │   │  Unit · Integration · E2E        │
│  DTOs · Enums · VOs     │   │                                  │
└──────────┬──────────────┘   └──────────────────────────────────┘
           │
┌──────────▼──────────────────────────────────────────────────────┐
│  Infrastructure Layer                                            │
│  SQL DB (EF Core + Chapter global filter)                        │
│  Azure Blob · Stripe · Email/SMS · Redis (Hangfire storage)      │
└──────────────────────────────────────────────────────────────────┘
```

### 2.3 Project References

| Project | Type | References |
|---|---|---|
| `Lotv.Api` | ASP.NET Core Web API | → `Lotv.Core` |
| `Lotv.Web` | Blazor Server (was WebAssembly until 2026-08-31, see R-22) | → `Lotv.Core` |
| `Lotv.Core` | Class Library | (none) |
| `Lotv.Tests` | xUnit Test Project | → `Lotv.Core`, `Lotv.Api` |

### 2.4 Engagement Service Tracks

The full WTE Solutions engagement with LOTV Ministry covers four coordinated service tracks:

| Track | Name | Deliverables | Charitable / Discounted? |
|---|---|---|---|
| **Track 1** | Low-Hanging Tech | Microsoft nonprofit hosting, DMARC/DKIM, HIPAA web forms, distribution list audit | Consulting included |
| **Track 2** | Web Redevelopment | Content site (lotvministry.org on CMS), Donor site (lilyofthevalleyministry.com) | Website build as charitable contribution |
| **Track 3** | Content Creation | Staff directory, blog/resource library, video studio access, social media automation | Charitable contribution + discounted editing |
| **Track 4** | Platform Development | Full .NET 9 SaaS dashboard application (Phases 0–6) | $10k investment, 30% charitable reduction |

> **Note:** Tracks 1–3 run alongside or prior to Track 4. Track 2 depends on LOTV's 501(c)(3) status changeover for the donor site domain transfer.

---

## 3. Stakeholders & Roles

### 3.1 Project Team (WTE)

| Role | Responsibilities |
|---|---|
| **Lead Developer / Project Manager** | Architecture, full-stack development, delivery management, client communication |
| **Backend Developer** | ASP.NET Core API, EF Core, Stripe integration, background jobs |
| **Frontend Developer** | Blazor Server components, responsive UI, dashboards, role-based views |
| **QA / Test Engineer** | Unit tests, integration tests, Playwright E2E, coverage reporting |
| **DevOps / Infrastructure** | Azure setup, CI/CD pipelines, Docker, monitoring, secrets management |

### 3.2 Client Team

| Role | Responsibilities |
|---|---|
| **Project Sponsor** | Executive authority, contract approval, milestone sign-off |
| **Product Owner** | Day-to-day requirements, business rules documentation, UAT participation |
| **Diocese Administrator(s)** | Domain expertise, test data, UAT for staff/admin roles |
| **IT / Infrastructure Contact** | Azure account provisioning, domain/DNS access, Stripe account setup |

### 3.3 User Roles (Platform)

| Role | Scope | Description |
|---|---|---|
| **Person in Need** | Chapter | Submits service requests, tracks fulfillment status |
| **Donor** | Chapter | Makes monetary/resource donations, views impact, downloads receipts |
| **Volunteer** | Chapter | Receives auto-assigned requests, accepts/declines, fulfills, submits completion reports |
| **ChapterStaff** | Chapter only | Manages chapter requests, monitors real-time operations board, oversees allocations |
| **ChapterAdmin** | Chapter only | Full chapter access + user management, chapter config, chapter reports |
| **HQAdmin** | All chapters | Cross-chapter dashboard, all-chapter operations board, HQ reports, global user/chapter management |

---

## 4. Project Governance

### 4.1 Decision Authority

| Decision Type | Authority |
|---|---|
| Architecture decisions (ADRs) | WTE Lead Dev + Client Product Owner sign-off |
| Scope changes / Change Orders | Both parties must sign Change Order before work begins |
| Milestone acceptance | Client has 5 business days to accept or raise defects |
| Production go-live | Requires written Client launch approval |
| Emergency hotfixes post-launch | WTE may deploy with verbal approval; written confirmation within 24h |

### 4.2 Escalation Path

```
Developer Issue  →  WTE Lead Dev  →  WTE Principal  →  Client Sponsor
       ↑                                                      ↑
   (Technical)                                           (Business/Contract)
```

Unresolved disputes escalate to mandatory mediation, then binding arbitration (North Carolina).

### 4.3 Meeting Cadence

| Meeting | Frequency | Participants | Purpose |
|---|---|---|---|
| Sprint Review / Demo | End of each phase | Both teams | Demo deliverables, gather feedback |
| Status Sync | Weekly (async or 30 min) | PM + Product Owner | Progress, blockers, action items |
| Architecture Review | Phase 1 | Both teams + stakeholders | ADR review and sign-off |
| UAT Kickoff | Phase 5 | Both teams | UAT logistics, test scenarios |
| Launch Readiness Review | Phase 6 | Both teams | Go/No-go decision |

### 4.4 Branching & Version Control

| Branch | Purpose |
|---|---|
| `main` | Stable, production-ready code. Protected. PRs only. |
| `kremer-dev` | Active feature development branch |
| `feature/*` | Individual features branched from `kremer-dev` |
| `hotfix/*` | Emergency production fixes branched from `main` |

**Commit convention:** Conventional Commits — `feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`
**No AI attribution** in commit messages. No sensitive data committed.

---

## 5. Scope Management

### 5.1 In Scope

**Track 1 — Low-Hanging Tech**
- Evaluate and transition to Microsoft 365 nonprofit pricing for Office and email hosting
- Configure DMARC/DKIM/SPF records to improve newsletter deliverability
- Migrate web form collection to a HIPAA-compliant, segmented solution for better distribution list management
- Audit and categorize existing contact/distribution database into segmented lists

**Track 2 — Web Redevelopment**
- Rebuild lotvministry.org on a mobile-friendly, easy-to-manage CMS (Duda) with updated branding, content automation, and staff directory
- Create a separate donor-focused website (lilyofthevalleyministry.com, pending 501(c)(3) status changeover and domain transfer) targeting fundraising and volunteering — separate content from lotvministry.org, tied to the Private Foundation entity
- First year hosting included FREE; two sites hosted for the price of one going forward

**Track 3 — Content Creation**
- Build staff/team directory on CMS with easy self-service updating
- Develop blog and resource content area for families in need and parish staff (articles + videos)
- Provide free access to WTE recording studio for family and parish video resource creation
- Discounted video editing at 50% off standard rate ($60/hr vs. $120/hr standard)
- Develop and recommend social media automation strategy to improve posting consistency and efficiency

**Track 4 — Platform Development (.NET 9 SaaS)**
- Full .NET 9 platform (API + Blazor WASM + Core Domain + Tests)
- **HQ → Chapter org model**: HQAdmin sees all; Chapter roles see own chapter only via EF Core global query filter
- Service request intake, auto-routing, auto-assignment, fulfillment lifecycle
- **Auto-assignment engine**: scores volunteers by proximity (Haversine) + skills match + workload; auto-assigns or queues for manual dispatch
- **Real-time operations board**: SignalR `RequestsHub` — per-chapter groups + `hq-all` group; live Kanban for chapter staff; cross-chapter board for HQ
- **Scheduled reports**: Hangfire jobs — daily digest (6 AM per chapter) + weekly summary (Monday 7 AM) emailed to chapter leads and HQ
- Donor registration, monetary donations, resource donations, receipts
- Stripe payment processing (PaymentIntent, webhooks, refunds)
- Volunteer dispatch and work queue management
- Chapter Staff dashboards: real-time Kanban, workload, unassigned queue, allocation ledger
- HQ Dashboard: cross-chapter summary table, per-chapter drill-down, HQ-wide KPI strip
- Impact & Distribution Dashboard (KPIs, money flow, map, timeline) — chapter-scoped or HQ all-chapters
- Donation Tracking Dashboard (by person, diocese, city, channel, amount band)
- Fundraising event management (RSVP, tickets, check-in, silent auction)
- Admin: user management, chapter management, audit log, system config
- Email notifications (transactional + scheduled reports)
- JWT authentication with 6-role authorization + chapter-scoped query middleware
- EF Core with database migrations, global query filter, and chapter seed data
- Azure deployment (App Service, Blob Storage, Key Vault, Redis for Hangfire)
- GitHub Actions CI/CD pipeline (build, test, deploy)
- Dockerfiles and docker-compose for local dev
- HTTPS, security headers, OWASP Top 10 review, dependency scanning
- ≥80% test coverage on `Lotv.Core`; integration + E2E tests
- Handoff documentation package

### 5.2 Out of Scope (Explicit Exclusions)

See [Section 17 — Exclusions](#17-exclusions) for the complete list.

### 5.3 Change Order Process

1. Either party identifies a scope change
2. WTE prepares a written Change Order: description, effort estimate, cost, schedule impact
3. Client reviews within 5 business days
4. Both parties sign before any out-of-scope work begins
5. Change Order is appended to SOW-LOTV-001

---

## 6. Work Breakdown Structure

---

### Track 1 — Low-Hanging Tech ⬜ PENDING

#### 1T.1 Microsoft 365 Nonprofit Transition
- [ ] Audit current Microsoft/email licensing and costs
- [ ] Apply for Microsoft nonprofit pricing eligibility
- [ ] Compare current vs. nonprofit rate pricing for all Office and email licenses
- [ ] Plan and execute migration to nonprofit-rate licensing
- [ ] Document recurring savings for client

#### 1T.2 Newsletter Deliverability
- [ ] Audit current email DNS records (SPF, DKIM, DMARC)
- [ ] Configure DMARC policy on sending domain
- [ ] Configure DKIM keys for email service provider
- [ ] Verify SPF record alignment
- [ ] Test deliverability improvement with inbox placement tools
- [ ] Document sending domain configuration for client

#### 1T.3 HIPAA-Compliant Web Form Migration
- [ ] Inventory all existing web forms collecting sensitive information
- [ ] Select HIPAA-compliant form/data collection solution
- [ ] Migrate forms to compliant platform with segmented data routing
- [ ] Configure distribution list segments (families, parishes, donors, volunteers)
- [ ] Test form submission → list routing flow

#### 1T.4 Distribution List Audit
- [ ] Export and review existing contact database
- [ ] Categorize contacts into distribution segments (families, parishes, donors, staff, volunteers)
- [ ] Remove duplicates and invalid addresses
- [ ] Deliver cleaned, segmented list to client
- [ ] Document list management process and recommendations

---

### Track 2 — Web Redevelopment ⬜ PENDING

> **Dependency:** Donor website domain transfer requires LOTV 501(c)(3) status changeover before lilyofthevalleyministry.com can be repurposed.

#### 2T.1 Content Website — lotvministry.org (Duda CMS)
- [ ] Audit existing lotvministry.org content and structure
- [ ] Design new site architecture (navigation, page hierarchy)
- [ ] Build site on Duda CMS with mobile-responsive design
- [ ] Migrate and refresh existing content
- [ ] Build staff/team directory with self-service updating
- [ ] Build resource area for articles and videos (families + parishes)
- [ ] Configure content automation / scheduling features
- [ ] QA across devices and browsers
- [ ] Client review and sign-off
- [ ] Go-live and DNS cutover

#### 2T.2 Donor Website — lilyofthevalleyministry.com
- [ ] Confirm 501(c)(3) status changeover timing with client
- [ ] Design donor-specific site architecture (fundraising, volunteering, impact)
- [ ] Build site on Duda CMS with content distinct from lotvministry.org
- [ ] Integrate fundraising CTAs and donation links (ties to LOTV platform and/or Private Foundation)
- [ ] Configure volunteer recruitment pages
- [ ] QA across devices and browsers
- [ ] Client review and sign-off
- [ ] Go-live following domain transfer

#### 2T.3 Hosting & Ongoing
- [ ] Configure hosting for both sites (2 sites, 1 hosting rate)
- [ ] First year hosting — no charge
- [ ] Document renewal pricing ($39/month or annual equivalent)
- [ ] Handoff CMS credentials and training to client content manager

---

### Track 3 — Content Creation ⬜ PENDING

#### 3T.1 Staff Directory
- [ ] Collect staff profile information and headshots from client
- [ ] Build directory module in Duda CMS (self-service updatable)
- [ ] Add to lotvministry.org
- [ ] Train client staff on updating directory entries

#### 3T.2 Blog & Resource Library
- [ ] Define content categories (families in need, parish staff, donors, volunteers)
- [ ] Establish editorial template and style guide
- [ ] Build blog/resource section in Duda CMS
- [ ] Create initial content stubs or seed articles (client provides content; WTE structures/formats)
- [ ] Set up video embed capability for family/parish video resources

#### 3T.3 Video Resource Production
- [ ] Schedule client recording sessions at WTE studio
- [ ] Produce video resources for families and parish staff
- [ ] Edit and deliver videos (50% discounted rate — $60/hr)
- [ ] Embed videos in resource library on lotvministry.org

#### 3T.4 Social Media Automation
- [ ] Audit existing social media accounts and posting cadence
- [ ] Recommend automation tooling (Buffer, Hootsuite, or equivalent)
- [ ] Configure automation workflows for content distribution
- [ ] Document process and train client team

---

### Track 4 — Platform Development (.NET 9 SaaS)

### Phase 0 — Foundation ✅ COMPLETE

- [x] Initialize git repository (`main` + `kremer-dev` branches)
- [x] Create `Lotv.slnx` solution (XML-based .NET 9 format)
- [x] Scaffold `Lotv.Api` (ASP.NET Core Web API, net9.0)
- [x] Scaffold `Lotv.Web` (Blazor WebAssembly, net9.0)
- [x] Scaffold `Lotv.Core` (Class Library, net9.0)
- [x] Scaffold `Lotv.Tests` (xUnit, net9.0)
- [x] Wire project references (Api→Core, Web→Core, Tests→Core+Api)
- [x] Verify zero build errors (`dotnet build Lotv.slnx`)
- [x] Create `MASTER_TODO.md` (6-phase tracker)
- [x] Create `SESSION_STARTUP_DIRECTIVE.md`
- [x] Author `MSA-LOTV.md` and `.docx`
- [x] Author `SOW-LOTV-001-FullPlatform.md` and `.docx`
- [x] Configure `.gitignore` (builds, secrets, OS files)
- [x] Configure `data/.gitignore` (sensitive data protection)
- [x] Initial commit + structure commit pushed to GitHub
- [x] Configure `.claude/settings.local.json`

---

### Phase 1 — Architecture & Design ⬜ NEXT

#### 1.1 Domain Modeling
- [ ] Entity Relationship Diagram (ERD) — all 35+ entities
- [ ] Diocese data model with organizational hierarchy
- [ ] Define all enums: `RequestStatus`, `RequestPriority`, `DonationChannel`, `AllocationStatus`, `UserRole`, `EventType`, `AuctionStatus`
- [ ] Define value objects: `Address`, `Money`, `ContactInfo`
- [ ] Define escalation rules and thresholds (Client input required)
- [ ] Silent auction workflow diagram
- [ ] Event revenue model (tickets, donations, auction proceeds)

#### 1.2 API Contract
- [ ] OpenAPI specification (YAML/JSON) for all 60+ endpoints
- [ ] Request/response schemas for all DTOs
- [ ] Authentication flow diagram (registration → JWT → refresh)
- [ ] Role-permission matrix (all 5 roles × all endpoint groups)

#### 1.3 Technical Decisions (ADRs)
- [ ] **ADR-001:** Database selection (SQL Server / PostgreSQL / SQLite dev)
- [ ] **ADR-002:** Authentication strategy (ASP.NET Core Identity + JWT)
- [ ] **ADR-003:** Email provider (SendGrid / Postmark / MailKit)
- [ ] **ADR-004:** Blob storage strategy (Azure Blob / local dev fallback)

#### 1.4 Infrastructure Planning
- [ ] Azure resource diagram (App Service, SQL, Blob, Key Vault, CDN)
- [ ] Environment matrix (dev / staging / prod) with config strategy
- [ ] Secrets management plan (Key Vault + environment variables)
- [ ] CI/CD pipeline design (GitHub Actions workflow structure)

#### 1.5 Deliverables
- [ ] ERD document in `docs/`
- [ ] OpenAPI spec in `docs/`
- [ ] 4 ADR documents in `docs/adr/`
- [ ] Client approval of ERD and architecture before Phase 2 begins

---

### Phase 2 — Core Domain (Lotv.Core) ⬜ PENDING

#### 2.1 Anchor / Identity Entities
- [ ] `Diocese` — id, name, region, contact, address
- [ ] `ApplicationUser` — base identity, role, diocese affiliation
- [ ] `PersonInNeed` — extends ApplicationUser, request history
- [ ] `Donor` — extends ApplicationUser, diocese, DonationChannel, contribution history
- [ ] `Volunteer` — extends ApplicationUser, skills, availability, service area
- [ ] `Employee` — extends ApplicationUser, title, department

#### 2.2 Service Request Entities
- [ ] `ServiceRequest` — category, urgency, status, description, diocese, submitter
- [ ] `ServiceFulfillment` — linked request, assignee, outcome, completion date
- [ ] `RequestNote` — note text, author, timestamp, visibility
- [ ] `RequestActivity` — audit trail, status changes, actor
- [ ] `RequestAssignment` — volunteer/staff, accept/decline, assigned date

#### 2.3 Donation & Allocation Entities
- [ ] `MonetaryContribution` — donor, amount, channel, date, Stripe ref
- [ ] `ResourceDonation` — donor, item type, quantity, condition, status
- [ ] `MoneyAllocation` — source contribution, target request/expense, amount, date
- [ ] `ResourceAllocation` — source donation, target request, quantity, date
- [ ] `Expense` — description, amount, category, approved by, date

#### 2.4 Event Entities
- [ ] `FundraisingEvent` — name, date, venue, capacity, ticket price, diocese
- [ ] `EventAttendee` — event, user, ticket type, check-in status, payment ref
- [ ] `SilentAuctionItem` — event, description, starting bid, reserve, winner
- [ ] `AuctionBid` — item, bidder, amount, timestamp

#### 2.5 Dashboard & Reporting DTOs
- [ ] `ImpactSummary` — KPI aggregates
- [ ] `DonationByPersonRow`, `DonationByDioceseRow`, `DonationByCityRow`
- [ ] `DonationByChannelRow`, `DonationByAmountBandRow`
- [ ] `MoneyFlowRow`, `ResourceDistributionRow`
- [ ] `GeographicMapPoint`, `TimelineEventRow`

#### 2.6 Service Interfaces
- [ ] `IServiceRequestService`, `IWorkloadService`
- [ ] `IDonorService`, `IVolunteerService`, `IUserService`
- [ ] `IPaymentService`, `IReceiptService`
- [ ] `IResourceService`, `IAllocationService`
- [ ] `IDashboardService`, `IReportingService`
- [ ] `INotificationService`, `IAuditService`, `IEventService`

#### 2.7 Unit Tests
- [ ] Unit tests for all entity validation logic
- [ ] Unit tests for all service interface mock implementations
- [ ] Domain logic tests (escalation rules, allocation logic, status transitions)
- [ ] Achieve ≥80% code coverage on `Lotv.Core`

---

### Phase 3 — API (Lotv.Api) ⬜ PENDING

#### 3.1 Infrastructure Setup
- [ ] EF Core DbContext (`LotvDbContext`) with all entity configurations
- [ ] Database migrations (initial + diocese seed data)
- [ ] ASP.NET Core Identity configuration
- [ ] JWT bearer authentication middleware
- [ ] Role-based authorization policies (5 roles)
- [ ] FluentValidation pipeline behavior
- [ ] Global exception handler middleware
- [ ] Serilog structured logging
- [ ] Rate limiting middleware
- [ ] CORS policy configuration
- [ ] Health check endpoint (`/health`)

#### 3.2 Service Request Endpoints
- [ ] `POST /api/requests` — submit request
- [ ] `GET /api/requests` — list all (Staff)
- [ ] `GET /api/requests/unassigned` — unassigned queue
- [ ] `GET /api/requests/{id}` — detail
- [ ] `POST /api/requests/{id}/assign` — assign to volunteer/staff
- [ ] `POST /api/requests/{id}/accept` — volunteer accepts
- [ ] `POST /api/requests/{id}/decline` — volunteer declines
- [ ] `POST /api/requests/{id}/complete` — mark complete
- [ ] `POST /api/requests/{id}/escalate` — escalate
- [ ] `POST /api/requests/{id}/notes` — add note
- [ ] `GET /api/requests/{id}/activity` — activity timeline

#### 3.3 Donor & Contribution Endpoints
- [ ] `POST /api/donors/register`
- [ ] `GET /api/donors` (Staff)
- [ ] `GET /api/donors/{id}`
- [ ] `POST /api/contributions/monetary`
- [ ] `POST /api/contributions/resource`
- [ ] `GET /api/contributions/{donorId}`
- [ ] `GET /api/contributions/{id}/receipt`
- [ ] `GET /api/donors/{id}/impact`

#### 3.4 Payment Endpoints (Stripe)
- [ ] `POST /api/payments/intent` — create PaymentIntent
- [ ] `POST /api/payments/webhook` — Stripe webhook handler
- [ ] `POST /api/payments/{id}/refund`
- [ ] `GET /api/payments/{id}`

#### 3.5 Volunteer Endpoints
- [ ] `POST /api/volunteers/register`
- [ ] `GET /api/volunteers` (Staff)
- [ ] `GET /api/volunteers/{id}/queue` — work queue
- [ ] `GET /api/requests/available` — available to claim
- [ ] `GET /api/staff/workload` — staff workload view

#### 3.6 Event & Auction Endpoints
- [ ] `POST /api/events`, `GET /api/events`, `GET /api/events/{id}`, `PUT /api/events/{id}`
- [ ] `POST /api/events/{id}/rsvp` — RSVP/ticket
- [ ] `GET /api/events/{id}/attendees`
- [ ] `POST /api/events/{id}/checkin`
- [ ] `POST /api/events/{id}/auction/items`, `GET /api/events/{id}/auction/items`
- [ ] `POST /api/auction/items/{id}/bid`
- [ ] `GET /api/auction/items/{id}/bids`

#### 3.7 Dashboard Endpoints
- [ ] `GET /api/dashboard/impact`
- [ ] `GET /api/dashboard/money-flow`
- [ ] `GET /api/dashboard/resources`
- [ ] `GET /api/dashboard/map`
- [ ] `GET /api/dashboard/timeline`
- [ ] `GET /api/dashboard/categories`
- [ ] `GET /api/dashboard/allocation-ledger`
- [ ] `GET /api/dashboard/donation-tracking`

#### 3.8 Donation Tracking Endpoints
- [ ] `GET /api/donations/by-person`
- [ ] `GET /api/donations/by-diocese`
- [ ] `GET /api/donations/by-city`
- [ ] `GET /api/donations/by-channel`
- [ ] `GET /api/donations/by-amount-band`
- [ ] `GET /api/donations/ledger`

#### 3.9 Reporting & Admin Endpoints
- [ ] `GET /api/reports/impact`, `/donor`, `/export`
- [ ] `GET /api/users`, `POST /api/users/{id}/roles`
- [ ] `GET /api/dioceses`, `POST /api/dioceses`
- [ ] `GET /api/allocations/ledger`
- [ ] `GET /api/audit`

#### 3.10 Integrations
- [ ] Stripe SDK wired (PaymentIntent, webhook verification, refunds)
- [ ] Email service implementation (transactional — registration, receipt, status updates)
- [ ] Azure Blob Storage (receipt PDFs, resource donation attachments)
- [ ] Background job service (notification dispatch, scheduled reports)
- [ ] SMS integration (optional — Change Order required)

---

### Phase 4 — Frontend (Lotv.Web) ⬜ PENDING

#### 4.1 App Shell
- [ ] Responsive navigation shell with role-aware sidebar
- [ ] Auth state provider (JWT storage, refresh, role claims)
- [ ] Route guards (redirect to login for unauthenticated; 403 for insufficient role)
- [ ] Toast notification system
- [ ] Chart component integration (KPI cards, bar charts, pie charts, line charts)
- [ ] Loading state and error boundary components

#### 4.2 Person in Need Views
- [ ] Submit Request form (multi-step: category → details → review → submit)
- [ ] My Requests list with status badges
- [ ] Status tracking detail view with activity timeline
- [ ] Profile management

#### 4.3 Donor Views
- [ ] Donor registration form
- [ ] Monetary donation form with Stripe Elements checkout
- [ ] Resource donation logging form
- [ ] Contribution history table
- [ ] Receipt download (PDF)
- [ ] My Impact statement page

#### 4.4 Volunteer Views
- [ ] Volunteer registration form
- [ ] Available requests — map + list toggle
- [ ] Pending assignment decisions (accept / decline)
- [ ] My Work Queue (active assignments)
- [ ] Fulfillment completion form

#### 4.5 Staff Views
- [ ] All Requests dashboard (table with filter/sort/search)
- [ ] Unassigned Queue (priority-sorted)
- [ ] Kanban board (drag-and-drop columns)
- [ ] Request detail view (timeline, notes, assignment history)
- [ ] Workload view (per-volunteer capacity grid)
- [ ] Allocation ledger table

#### 4.6 Impact & Distribution Dashboard
- [ ] KPI card row (requests fulfilled, donations raised, volunteers active, events held)
- [ ] Money flow chart (in/out over time)
- [ ] Resource distribution chart
- [ ] Geographic map with service density points
- [ ] Activity timeline (filterable)
- [ ] Category breakdown (pie/donut chart)
- [ ] Full allocation ledger with drill-down

#### 4.7 Donation Tracking Dashboard
- [ ] Donations by person (sortable table + donor detail drill-down)
- [ ] Donations by diocese (grouped/ranked)
- [ ] Donations by city (map or ranked list)
- [ ] Donations by channel (Online / Check / Cash / In-Kind)
- [ ] Donations by amount band (histogram)
- [ ] Full donor ledger (all transactions, exportable)

#### 4.8 Event Management Views
- [ ] Upcoming events list
- [ ] Event detail page (info, revenue summary, attendee count)
- [ ] Attendee list with check-in controls
- [ ] Silent auction item list + bid history
- [ ] RSVP / ticket purchase flow
- [ ] Event creation/edit form (Admin/Staff)

#### 4.9 Admin Views
- [ ] User management (list, create, edit, deactivate, assign roles)
- [ ] Diocese management (list, add, edit)
- [ ] System configuration (escalation thresholds, notification templates)
- [ ] Audit log (searchable, filterable by user/action/date)

---

### Phase 5 — Testing ⬜ PENDING

#### 5.1 Unit Tests (Lotv.Core)
- [ ] `ServiceRequestService` — routing logic, status transitions, escalation
- [ ] `AllocationService` — allocation rules, balance validation
- [ ] `DashboardService` — aggregation calculations
- [ ] `WorkloadService` — capacity logic
- [ ] `ReceiptService` — receipt generation logic
- [ ] All entity validation rules

#### 5.2 Integration Tests
- [ ] Full service request lifecycle (submit → assign → accept → complete)
- [ ] Donation + receipt generation flow
- [ ] Dashboard data aggregation end-to-end
- [ ] Stripe payment webhook processing
- [ ] Event + auction + check-in lifecycle

#### 5.3 End-to-End Tests (Playwright)
- [ ] E2E smoke: Person submits request, staff assigns, volunteer completes
- [ ] E2E smoke: Donor donates, receives receipt
- [ ] E2E smoke: Staff views Impact Dashboard
- [ ] E2E smoke: Event RSVP + check-in

#### 5.4 Coverage & Reporting
- [ ] Generate HTML coverage report
- [ ] Verify ≥80% coverage on `Lotv.Core`
- [ ] Test factory and seed data utilities in `Lotv.Tests`

#### 5.5 UAT Support
- [ ] Deploy to staging environment
- [ ] Provide UAT test scenarios per user role (5 roles)
- [ ] Bug tracking and triage during Client UAT window
- [ ] Defect fix cycle before Phase 6

---

### Phase 6 — Deployment & Launch ⬜ PENDING

#### 6.1 Containerization
- [ ] `Dockerfile` for `Lotv.Api`
- [ ] `Dockerfile` for `Lotv.Web`
- [ ] `docker-compose.yml` for full local dev stack (API + Web + DB + Redis)

#### 6.2 CI/CD Pipeline
- [ ] GitHub Actions workflow: `ci.yml` — build + test on every PR
- [ ] GitHub Actions workflow: `deploy-staging.yml` — deploy on merge to `kremer-dev`
- [ ] GitHub Actions workflow: `deploy-prod.yml` — deploy on merge to `main` (manual approval gate)

#### 6.3 Azure Infrastructure
- [ ] App Service plan + API deployment slot
- [x] Hosting decided 2026-08-31: Azure App Service (both apps — Web is Blazor Server now, a normal Kestrel app like the API, no Static Web App needed). Deploy workflows updated; resource provisioning still open, see R-20
- [x] Database decided: SQL Server 2019, self-hosted at `10.100.1.87` (not Azure SQL/PostgreSQL as originally scoped, see R-17)
- [ ] Azure Blob Storage (receipts, attachments)
- [ ] Azure CDN for static assets
- [ ] Azure Key Vault (all secrets, connection strings, API keys)
- [ ] Redis Cache (optional — session, rate limiting)

#### 6.4 Environment Configuration
- [ ] `appsettings.Production.json` structure (no secrets committed)
- [ ] Dev / Staging / Production environment variable matrix documented
- [ ] EF Core migration run on deploy (or pre-deploy script)
- [ ] Diocese seed data loaded on first deploy

#### 6.5 Security Hardening
- [ ] HTTPS enforced; HTTP redirects
- [ ] Security response headers (HSTS, X-Frame-Options, CSP, etc.)
- [ ] OWASP Top 10 review checklist
- [ ] Dependency vulnerability scan (NuGet audit)
- [ ] Stripe webhook signature verification
- [ ] PCI DSS compliance confirmation (Stripe handles card data; LOTV is out of scope for PCI)

#### 6.6 Monitoring & Operations
- [ ] Application health check endpoint verified
- [ ] Azure Monitor / Application Insights wired
- [ ] Alerting configured (error rate, latency, availability)
- [ ] Database backup policy configured
- [ ] Log retention policy set

#### 6.7 Handoff Documentation
- [ ] Deployment guide (step-by-step, all environments)
- [ ] Environment variables reference (all keys, no values)
- [ ] Architecture diagram (final, updated)
- [ ] Post-launch support contact and SLA (per MSA)

---

## 7. Schedule & Milestones

### 7.1 Phase Duration Estimates

| Phase | Name | Estimated Duration | Dependencies |
|---|---|---|---|
| 0 | Foundation | Complete | — |
| 1 | Architecture & Design | 1–2 weeks | Client: ERD/ADR approval |
| 2 | Core Domain | 2–3 weeks | Phase 1 approved |
| 3 | API | 3–5 weeks | Phase 2 complete; Client: Stripe + email keys |
| 4 | Frontend | 3–5 weeks | Phase 3 API available; Client: branding |
| 5 | Testing | 2–3 weeks | Phase 4 complete; Client: UAT participation |
| 6 | Deployment & Launch | 1–2 weeks | Phase 5 complete; Client: Azure + DNS access |

> **Note:** Durations are estimates. Actual schedule depends on team size, Client response times, and change order volume. Client response SLA of 5 business days applies at every gate.

### 7.2 Milestone Gates

| Milestone | Phase Trigger | Gate Condition |
|---|---|---|
| **M0** | Contract execution | Deposit paid; work begins |
| **M1** | End of Phase 1 | ERD + ADRs accepted by Client |
| **M2** | End of Phase 2 | Domain model + test coverage accepted |
| **M3** | End of Phase 3 | API deployed to staging; accepted by Client |
| **M4** | End of Phase 4 | Frontend deployed to staging; accepted by Client |
| **M5** | End of Phase 5 | Coverage report + UAT sign-off |
| **M6** | End of Phase 6 | Production live; Client launch approval |

### 7.3 Payment Terms

- 30-day net payment terms per invoice
- 1.5%/month late fee after 30 days
- Non-payment 60+ days → WTE may suspend access
- Non-payment 90+ days → WTE may terminate agreement

---

## 8. Resource Plan

### 8.1 Development Environment

| Tool | Purpose |
|---|---|
| .NET 9 SDK | Build and run all projects |
| Visual Studio / VS Code / Rider | IDE |
| Docker Desktop | Local container stack |
| Git + GitHub CLI | Version control |
| Azure CLI | Cloud resource management |
| Stripe CLI | Webhook testing locally |
| Playwright CLI | E2E test runner |
| Hangfire Dashboard | Local job monitoring (`/hangfire`) |
| Azure SignalR Service (prod) | Managed SignalR backplane for multi-instance scale |
| Redis (local via Docker) | Hangfire job storage + optional session cache |

### 8.2 Build & Test Commands

```bash
# Build entire solution
dotnet build Lotv.slnx

# Run all tests
dotnet test Lotv.slnx

# Run with coverage
dotnet test Lotv.slnx --collect:"XPlat Code Coverage"

# Run API locally
cd src/Lotv.Api && dotnet run

# Run Web locally
cd src/Lotv.Web && dotnet run
```

### 8.3 Key Configuration Files

| File | Purpose |
|---|---|
| `Lotv.slnx` | Solution definition (.NET 9 XML format) |
| `src/Lotv.Api/appsettings.json` | API configuration |
| `src/Lotv.Api/appsettings.Development.json` | Dev overrides |
| `src/Lotv.Api/Properties/launchSettings.json` | Local run profiles |
| `src/Lotv.Web/Properties/launchSettings.json` | Web run profiles |
| `.claude/settings.local.json` | Claude Code permissions |
| `data/.gitignore` | Sensitive data file protection |

### 8.4 Local Dev Ports

| Service | HTTP | HTTPS |
|---|---|---|
| `Lotv.Api` | 5275 | 7072 |
| `Lotv.Web` | 5205 | 7146 |

---

## 9. Risk Register

| ID | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R-01 | Client delays on deliverables (chapter list, branding, keys) | High | High | 5-business-day SLA in SOW; phase gates block on Client items |
| R-02 | Scope creep from evolving business rules | High | Medium | Formal Change Order process; written requirements before implementation |
| R-03 | Stripe integration complexity (webhook reliability) | Low | High | Stripe CLI for local testing; idempotency keys; webhook signature verification |
| R-04 | EF Core migration conflicts in team development | Medium | Medium | One migration author per sprint; migration review in PR |
| R-05 | Azure cost overrun in staging | Low | Medium | Budget alerts on Azure subscription; tear down non-prod after hours |
| R-06 | Test coverage shortfall delaying Phase 5 | Medium | Medium | Coverage gates in CI; track coverage trend throughout Phase 2–3 |
| R-07 | Blazor WASM initial load performance | N/A | N/A | **No longer applicable 2026-08-31**: `Lotv.Web` converted to Blazor Server, no client-side runtime download at all — see R-22. Left in the register rather than deleted, as a record of a risk that resolved via an architecture change rather than direct mitigation |
| R-08 | Client UAT drags beyond 5-day SLA | Medium | High | UAT plan delivered 2 weeks before Phase 5; per-role test scripts prepared |
| R-09 | DNS/domain transfer delays at launch | Low | High | Begin DNS coordination 2 weeks before Phase 6 |
| R-10 | Sensitive data accidentally committed | Low | Critical | `.gitignore` enforced; pre-commit hook for secret scanning; `data/.gitignore` active |
| R-11 | SignalR connection scaling under load (many concurrent chapter users) | Medium | Medium | Azure SignalR Service (managed backplane) for production; local dev uses in-process |
| R-12 | Auto-assignment scoring produces poor matches (volunteer quality issues) | Medium | High | Tunable weights for proximity/skills/workload exposed in admin config; staff can always override; unmatched requests fall to manual queue |
| R-13 | Scheduled report emails delivered to spam / not received | Low | Medium | SPF/DKIM configured; test email delivery in staging; dedicated transactional email domain |
| R-14 | Chapter query filter misconfiguration exposes cross-chapter data | Low | Critical | EF Core global query filter unit tested; integration test suite asserts cross-chapter data isolation |
| R-15 | Real ministry PII (family names, addresses, loss stories) mishandled during data migration | Low | Critical | Source spreadsheet and all local databases used during import gitignored; import tool reads real data only from untracked local paths; no PII ever written to a committed file (added 2026-07-27) |
| R-16 | Shared `sa` SQL Server credential used ad hoc during production DB setup | Medium | High | Credential passed via environment variables only, never committed. **2026-08-31**: rotation script prepared (`src/Lotv.Migrations.SqlServer/rotate-app-credential.sql`, creates a scoped `lotv_app` login); **open action**: run it against the live `10.100.1.87` database and update connection-string secrets — needs prod DB access not available to the assistant (added 2026-07-27) |
| R-17 | EF migration history incompatible with SQL Server (scaffolded against SQLite) | Low | Medium | **Fixed 2026-08-31**: new `src/Lotv.Migrations.SqlServer` project holds a SQL-Server-specific `InitialCreate` migration scaffolded fresh against `UseSqlServer` (confirmed correct `nvarchar(max)`/`int` types, not SQLite's `TEXT`); `Program.cs` points at it via `MigrationsAssembly`. Took 3 CI iterations to get the assembly actually loadable at runtime (no circular ProjectReference possible, so CI now explicitly builds the migrations project before the migration step runs) — verified via 4 real triggered runs against `wtesolutions/LOTV`. **Open action**: run `src/Lotv.Migrations.SqlServer/baseline-existing-database.sql` once against the live database (marks `InitialCreate` as already-applied instead of re-running its DDL) before the deploy workflow's `dotnet ef database update` step ever touches it — needs prod DB access not available to the assistant (added 2026-07-27) |
| R-18 | JotForm intake webhook silently drops/corrupts data when the live form's question labels drift from the hardcoded `knownLabels` list in `Program.cs` | Low | High | **Fixed 2026-08-12**: `knownLabels` reconciled against a live API pull of the form's actual questions; the deeper root cause (splitting each field on its first colon, which breaks for labels with a colon mid-sentence like "Children for Bracelet: ...") was also fixed, not just patched around. `PackageRequest.ChildrenInitials` (a separate field from `Family.ChildrenInitials` that Kanban actually renders) is now populated too. 7 new integration tests, verified live via a real webhook POST + Playwright screenshot of the resulting Kanban card. Residual risk: the "Children for Bracelet" widget field's boundary fix is still unverified against a real submission — zero live submissions have hit the form to date |
| R-19 | Third-party form-builder tool (JotForm) edits are not reliably atomic — an edit call can report success while silently corrupting unrelated settings | Medium | High | Discovered 2026-08-05: a targeted email-settings edit corrupted ~40 unrelated form-level properties in one call. Mitigation going forward: verify every JotForm edit via a read-only API re-fetch before trusting it; prefer manual builder-UI edits for email/notification settings until tool reliability is re-established. **2026-08-12**: confirmed via read-only API check that HIPAA mode is still on and the autoresponder/notification sender-name fixes from the 2026-08-05 checklist were never applied — the notification email's "From" field is literally the merge-tag `{husbandsName}`, likely a leftover corruption artifact; six narrow `api_request` PUT/POST attempts to fix it programmatically all 400'd, so this still needs a manual builder-UI fix or an authenticated browser session; **open action**: confirm ministry has completed the manual revision-history rollback to 2026-07-22 11:59:27 (added 2026-08-05), and apply the 3 pending email/HIPAA settings fixes |
| R-20 | `docker compose up` had never actually been run before 2026-08-13 despite the Dockerfiles/compose file being marked complete in Phase 6 — deployment path was unverified | Low | Medium | **Fixed 2026-08-13**: ran the full stack for the first time and found 4 real bugs (RZ9985 Razor ambiguity under `dotnet publish`, non-root container/volume permissions, missing `wget` breaking the healthcheck, stale `gzip_static` serving the wrong API URL). All fixed and reverified live. **2026-08-31**: hosting decided — Azure App Service, code-based (no Docker in the deploy path per instruction); `deploy-staging.yml`/`deploy-production.yml` rewritten to `dotnet publish` + zip-deploy via `azure/webapps-deploy`. **Open action**: provision the actual App Service resources and add 7 GitHub secrets (`AZURE_CLIENT_ID`/`TENANT_ID`/`SUBSCRIPTION_ID`, 4× `AZURE_WEBAPP_*_NAME`) plus `DB_CONNECTION_STRING` on `wtesolutions/LOTV` — needs Azure account access not available to the assistant. Verified the pipeline itself runs cleanly up to that point via 4 real triggered CI runs (added 2026-08-13) |
| R-21 | Real intake (JotForm webhook or public `/apply`) never created a bereavement `FollowUpTracker` — only the one-time historical import ever populated that table, discovered by cross-checking webhook field coverage against the ministry's actual live-use spreadsheet rather than assuming the JotForm schema was sufficient ground truth | Low | High | **Fixed 2026-08-31**: added `CreateFollowUpTrackerIfLossKnownAsync`, called from both intake paths, gated on `Family.DateOfLoss` being set. Milestone offsets (+21 days, +3/+6/+11 calendar months) matched exactly against real due-date deltas in the ministry's live `SM (2026)` tracking sheet. Surfaced and fixed a second bug in the process: `GET /api/v1/follow-up-trackers` 500'd with an infinite `Tracker.Milestones` ↔ `Milestone.FollowUpTracker` reference cycle the instant a tracker was created with both sides tracked in the same EF context (the historical import apparently never triggered EF's relationship fixup the same way) — fixed with a scoped `[JsonIgnore]` on the unused back-reference rather than a global `ReferenceHandler` change. 4 new tests, verified live via Playwright. **Open, unfixed**: the same spreadsheet audit found "How did you hear about us?" is rich free text in real usage (actual referrer names/context) but the live JotForm form only offers a fixed dropdown — real referral detail will be lost going forward; needs either an "Other, specify" field added to the live form or an explicit sign-off that the trade-off is acceptable. Also handed the ministry manual field-add steps once builder access is available (added 2026-08-31) |
| R-22 | `Lotv.Web` converted from Blazor WebAssembly to Blazor Server (2026-08-31, per direct instruction to run the same hosting tech as `Lotv.Api` — no more Windows-plan requirement, no Docker) — a hosting-model change touching every page's render path | Low | High | Repo-wide grep confirmed zero synchronous JS interop existed anywhere before starting (the one WASM→Server failure mode that can't be worked around), which de-risked the conversion significantly. Found and fixed 3 real regressions via an actual browser walkthrough, not just code review: (1) `LocalizationService` was `Singleton`, invalid once real multi-user server hosting made it consume the Scoped `IJSRuntime` — ASP.NET Core's DI validator caught it at startup; (2) the entire auth session-restore flow broke on any direct/reload navigation to a protected route, because Blazor Server's static prerender pass has no JS interop available yet (this app's auth reads a refresh token from `sessionStorage`) — fixed by disabling prerendering and restoring a real `#app` content-readiness boundary; (3) 6 PDF/CSV/QR download links across 5 pages used relative `/api/...` hrefs that resolve against the Web app's own origin, not the API's — confirmed pre-existing (checked the deleted `nginx.conf`, it never had an `/api/` proxy rule either) rather than introduced by the conversion, fixed with a new `ApiService.BaseUrl` helper. Verified: 433/433 unit tests, 60/60 E2E tests, Kanban drag-and-drop and CSV export both confirmed working live, ~35 of 312 admin routes walked with zero further errors found. **Open**: the remaining ~275 routes haven't been individually walked (added 2026-08-31) |
| R-23 | E2E test suite broke as a direct consequence of R-22's conversion — not caught until `CI — Build & Test` actually ran, since Lotv.Tests (433 tests) never exercises `Lotv.Web` at all | Low | Low | **Fixed 2026-08-31**: all 7 `MobileResponsivenessTests` timed out on a WASM-only `#app:not(:empty)` selector that no longer meant anything post-conversion — fixed alongside R-22's `#app` restoration. That fix then unmasked 3 further test failures that had been silently failing-to-even-run since before this session: a hardcoded, non-responsive two-column `grid-template-columns:1fr 1fr` inline style on 3 forms' name-field rows overflowed a 390px mobile viewport — confirmed via the last known-green pre-conversion CI run (2026-07-20) that this predates the conversion entirely. Fixed with `repeat(auto-fit,minmax(140px,1fr))`. Verified via 5 real triggered CI runs against `wtesolutions/LOTV`, including one opened purely to confirm the fix (closed without merging afterward) (added 2026-08-31) |
| R-24 | `Lotv.Api` code-coverage reporting showed a misleading 5% ❌ health flag on every PR, dominated by ~90,000 lines of auto-generated EF Core migration code (never meant to be unit-tested) counted in the denominator | Low | Low | **Fixed 2026-08-31**: added `tests/Lotv.Tests/coverlet.runsettings` excluding `Lotv.Api.Migrations.*` and `Lotv.Migrations.SqlServer.Migrations.*`. Verified: real application coverage is 46.7%, not 5.3%. Doesn't affect CI pass/fail (the coverage thresholds are cosmetic-only). Real, separate follow-up if desired: 46.7% itself has room to grow (added 2026-08-31) |

---

## 10. Communication Plan

### 10.1 Status Reporting

| Report | Frequency | Channel | Audience |
|---|---|---|---|
| Weekly Status Update | Weekly | Email / Slack | PM + Client Product Owner |
| Phase Completion Summary | End of each phase | Email + demo | All stakeholders |
| Risk Log Update | Biweekly | Async document | PM + Client Sponsor |
| Bug/Defect Report | During Phase 5 UAT | Bug tracker | Both teams |

### 10.2 Communication Channels

| Channel | Purpose |
|---|---|
| **Email** | Formal communications, milestone invoices, Change Orders |
| **Slack / Teams** | Day-to-day async collaboration |
| **GitHub Issues** | Bug tracking, task tracking |
| **GitHub Pull Requests** | Code review, phase merge approvals |
| **Video Call (Zoom / Teams)** | Phase demos, architecture reviews, UAT kickoff |

### 10.3 Document Storage

| Location | Content |
|---|---|
| `docs/` | MSA, SOW, ADRs, architecture diagrams, API contracts |
| `sessions/` | Per-session development notes (YYYY-MM-DD-title.md) |
| `MASTER_TODO.md` | 6-phase task tracker (updated each session) |
| GitHub Wiki (optional) | Onboarding, environment setup guides |

---

## 11. Change Management

### 11.1 What Triggers a Change Order

- New features not listed in SOW-LOTV-001 In-Scope section
- Changes to agreed architecture (post-ADR sign-off)
- Additional user roles or permission structures
- New integrations (CRM, SMS provider, mobile app)
- Significant rework caused by Client-provided incorrect requirements
- Any item listed in [Section 17 — Exclusions](#17-exclusions)

### 11.2 Change Order Workflow

```
1. Request identified (either party)
2. WTE prepares Change Order document within 5 business days:
   - Description of change
   - Effort estimate (hours or fixed fee)
   - Schedule impact
   - Cost
3. Client reviews within 5 business days
4. Both parties sign Change Order (electronic signature accepted)
5. Work begins; Change Order appended to SOW-LOTV-001
```

### 11.3 Emergency Changes

For critical production defects within the 30-day warranty period:
- WTE corrects at no charge if caused by WTE workmanship
- Client-caused defects or new requirements are billed at standard rate

---

## 12. Quality Plan

### 12.1 Code Quality Standards

- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled
- No compiler warnings in release builds
- FluentValidation on all API inputs (no raw model state validation)
- Global exception handler — no unhandled exceptions reach clients
- All secrets via configuration / Key Vault — none hardcoded

### 12.2 Test Coverage Requirements

| Scope | Target |
|---|---|
| `Lotv.Core` unit tests | ≥80% line coverage |
| `Lotv.Api` integration tests | Key flows covered (request lifecycle, payment, dashboard) |
| E2E smoke tests | 4 critical user journeys |
| Overall solution | ≥80% on Core; integration coverage on API |

### 12.3 Security Checklist

- [ ] OWASP Top 10 reviewed before launch
- [ ] Dependency vulnerability scan (`dotnet list package --vulnerable`)
- [ ] No secrets in source control (verified via audit)
- [ ] JWT tokens: short expiry, refresh token rotation
- [ ] Stripe webhooks: signature verified on every call
- [ ] HTTPS enforced; HTTP → HTTPS redirect
- [ ] HSTS header enabled
- [ ] Content Security Policy configured
- [ ] SQL injection: EF Core parameterized queries only
- [ ] Rate limiting on public endpoints

### 12.4 Compliance Requirements

| Standard | Scope |
|---|---|
| **PCI DSS** | Stripe handles all card data; LOTV stores no card numbers |
| **WCAG 2.1 AA** | Blazor Server frontend accessibility |
| **OWASP Top 10** | API security review before launch |
| **GDPR/CCPA** | Data residency US; privacy policy required (Client provides content) |
| **HTTPS / TLS** | All production traffic |

---

## 13. Dependency Map

### 13.1 Phase Dependencies

```
Phase 0 (Complete)
    └─→ Phase 1 (Architecture)
            └─→ Phase 2 (Core Domain)  ← needs Client: ERD approval
                    └─→ Phase 3 (API)  ← needs Client: Stripe + email keys
                            └─→ Phase 4 (Frontend)  ← needs Client: branding
                                    └─→ Phase 5 (Testing)  ← needs Client: UAT
                                            └─→ Phase 6 (Launch)  ← needs Client: Azure + DNS
```

### 13.2 Critical Path Items

| Item | Blocks | Owner |
|---|---|---|
| Client ERD approval | Phase 2 start | Client |
| Client Stripe test keys | Phase 3 API build | Client |
| Client email provider key | Phase 3 API build | Client |
| Client branding / logo | Phase 4 UI build | Client |
| Client Azure subscription | Phase 6 infra setup | Client |
| Client DNS access | Phase 6 go-live | Client |
| ADR-001 (database selection) | Phase 3 EF Core setup | WTE |
| OpenAPI spec sign-off | Phase 3 build start | Both |

### 13.3 External Service Dependencies

| Service | Phase Introduced | Risk |
|---|---|---|
| Stripe | Phase 3 | API key availability; webhook testing |
| Email Provider | Phase 3 | Account provisioning; DNS SPF/DKIM |
| Azure App Service | Phase 6 | Provisioning lead time |
| Azure SQL | Phase 6 | Provisioning; connection string management |
| GitHub Actions | Phase 6 | Workflow authorization; secrets setup |

---

## 14. Budget & Payment Schedule

### 14.1 Service Track Pricing

#### Track 1 — Low-Hanging Tech
| Item | Pricing |
|---|---|
| Microsoft 365 nonprofit rate transition | Consulting included in engagement |
| DMARC/DKIM/SPF configuration | Consulting included in engagement |
| HIPAA-compliant form migration | Consulting included in engagement |
| Distribution list audit and segmentation | Consulting included in engagement |
| Office/email hosting (ongoing) | Microsoft nonprofit rates (Client pays license fees directly) |

#### Track 2 — Websites + Software
| Item | Pricing |
|---|---|
| Content website build (lotvministry.org on Duda) | **Charitable contribution** (value: $3,500–$6,000) |
| Donor website build (lilyofthevalleyministry.com on Duda) | **Charitable contribution** (value: $3,500–$6,000) |
| First year hosting — both sites | **FREE** |
| Hosting renewal (Year 2+) | $39/month or annual equivalent — 2 sites for the price of 1 |

#### Track 3 — Content Resources
| Item | Pricing |
|---|---|
| Staff directory build + resource library structure | **Charitable contribution** |
| WTE recording studio access | **FREE** (for family and parish video resource creation) |
| Video editing | **50% discount** — $60/hr (standard rate: $120/hr) |
| Social media automation consulting | Included in engagement |

#### Track 4 — Dashboard Platform Development
| Item | Pricing |
|---|---|
| Full platform development (Phases 0–6) | $10,000 investment |
| Charitable contribution discount | 30% reduction |
| **Net client investment** | **~$7,000** |
| Payment structure | Divided over 3 months — approximately **$2,000–$2,500/month** |

> **WTE's goal:** Provide as many services as possible as a charitable contribution to LOTV Ministry, with discounted rates for tech services.

### 14.2 Milestone Payment Table (Track 4 — Platform)

| Milestone | Description | Trigger | Amount |
|---|---|---|---|
| M0 | Project Start Deposit | Contract execution | $[AMOUNT] |
| M1 | Architecture Approved | Client acceptance of Phase 1 | $[AMOUNT] |
| M2 | Core Domain Complete | Client acceptance of Phase 2 | $[AMOUNT] |
| M3 | API Complete | Client acceptance of Phase 3 | $[AMOUNT] |
| M4 | Frontend Complete | Client acceptance of Phase 4 | $[AMOUNT] |
| M5 | Testing Complete | UAT sign-off + coverage report | $[AMOUNT] |
| M6 | Production Launch | Go-live + Client launch approval | $[AMOUNT] |
| **TOTAL** | | | **~$7,000 net** |

### 14.3 Reimbursable Expenses

The following are billed as pass-through at cost with 30-day receipts:
- Software licenses (if purchased on Client's behalf)
- Cloud infrastructure costs (Azure subscription charges)
- Third-party API fees (email provider, SMS if added)

### 14.4 Liability Caps

Per MSA-LOTV:
- Per-occurrence cap: 12-month fees or **$150,000** (whichever is less)
- Annual aggregate cap: **$300,000**
- Consequential damages: excluded for both parties

---

## 15. Client Deliverables Tracker

| # | Deliverable | Required For | Status | Due Date |
|---|---|---|---|---|
| 1 | Diocese list (names, regions) | Phase 2 domain model | ⬜ Pending | Before Phase 2 |
| 2 | Organization name, logo, branding | Phase 4 frontend | ⬜ Pending | Before Phase 4 |
| 3 | Stripe test API keys | Phase 3 API build | ⬜ Pending | Before Phase 3 |
| 4 | Stripe live API keys | Phase 6 production | ⬜ Pending | Before Phase 6 |
| 5 | Email provider account + API key | Phase 3 API build | ⬜ Pending | Before Phase 3 |
| 6 | Azure subscription access | Phase 6 infra | ⬜ Pending | Before Phase 6 |
| 7 | Domain registrar access | Phase 6 DNS | ⬜ Pending | Before Phase 6 |
| 8 | Architecture approval (ERD + ADRs) | Phase 2 start gate | ⬜ Pending | End of Phase 1 |
| 9 | Domain model / ERD approval | Phase 2 start gate | ⬜ Pending | End of Phase 1 |
| 10 | Business rules documentation | Phase 2 / Phase 3 | ⬜ Pending | Phase 1 |
| 11 | UI feedback on mockups | Phase 4 final build | ⬜ Pending | Phase 4 |
| 12 | UAT participation (all 5 roles) | Phase 5 completion | ⬜ Pending | Phase 5 |
| 13 | Public-facing content (About, Privacy, ToS) | Phase 4 | ⬜ Pending | Phase 4 |
| 14 | Test data (dioceses, users, requests) | Phase 3 testing | ⬜ Pending | Before Phase 3 |
| 15 | Production launch approval (written) | Phase 6 go-live | ⬜ Pending | Phase 6 |
| 16 | 501(c)(3) status confirmation | Donor website domain transfer (Track 2) | ⬜ Pending | Before Track 2 donor site launch |
| 17 | lilyofthevalleyministry.com domain registrar access | Donor website go-live | ⬜ Pending | Before Track 2 donor site launch |
| 18 | Staff profile info and headshots | Staff directory (Track 3) | ⬜ Pending | Before Track 3 directory build |
| 19 | Website content (About, articles, resource text) | Tracks 2 & 3 | ⬜ Pending | Phased — per track |
| 20 | Recording session scheduling | Video production (Track 3) | ⬜ Pending | Track 3 |

---

## 16. Assumptions & Constraints

### 16.1 Technical Assumptions

- .NET 9 SDK available in all development environments
- Microsoft Azure is the target cloud provider (default; may be changed via Change Order)
- Stripe is the exclusive payment processor for this engagement
- English language only; no i18n/l10n in this SOW
- Single production environment + single staging environment
- WTE manages the GitHub repository and CI/CD pipeline configuration
- Data residency: United States

### 16.2 Web & Content Assumptions

- LOTV Ministry will complete its 501(c)(3) status changeover; donor site domain transfer cannot proceed until this is confirmed
- Client will provide all written content (articles, bios, resource text) for Tracks 2 and 3 on agreed timelines; delays in content delivery may delay site go-live
- Client designates at least one staff member as the ongoing CMS content manager for lotvministry.org
- Duda CMS is the agreed platform for both websites; changes to CMS platform require a Change Order
- Microsoft 365 nonprofit pricing eligibility is subject to Microsoft's qualification requirements; WTE will guide the process but cannot guarantee approval

### 16.3 Process Assumptions

- Client response SLA: **5 business days** for all review/approval requests
- Milestone acceptance window: **5 business days** after delivery
- Client participates in UAT for all 5 user roles during Phase 5
- Architecture decisions (ADRs) are binding once signed off — changes require Change Orders
- No AI attribution in commit messages or documentation

### 16.3 Constraints

| Constraint | Impact |
|---|---|
| `.slnx` solution format | Must use `dotnet build Lotv.slnx` — `Lotv.sln` does not exist |
| Sensitive data | Cannot be committed to git; `data/.gitignore` and `.gitignore` enforce this |
| Feature work only on `kremer-dev` | Merges to `main` via PR only |
| PCI scope | Stripe handles all card data; LOTV must not store raw card numbers |

---

## 17. Exclusions

The following items are explicitly **out of scope** for SOW-LOTV-001 and require a Change Order to add:

| Exclusion | Notes |
|---|---|
| Mobile native apps (iOS/Android) | Web-responsive only in this SOW |
| Real-time auction bidding via SignalR | SignalR IS in scope for the operations board. This exclusion applies only to live public auction bidding. Potential Phase 7 via Change Order. |
| Recurring donation subscriptions | Stripe Billing not included |
| Third-party CRM integration (Salesforce, HubSpot, etc.) | REST API provided for future integration |
| Volunteer background check integration | Third-party screening not in scope |
| Custom SMS provider | Email included; SMS optional via Change Order |
| Hardware / on-premises infrastructure | Cloud-only deployment |
| Training beyond handoff documentation | Written docs + recorded walkthrough delivered; formal training sessions are a Change Order |
| Public-facing copywriting / editorial content | Client provides all written content for websites and resources; WTE provides structure and formatting only |
| Ongoing social media management | WTE consults and configures automation; ongoing posting and content production is Client responsibility |
| Video production beyond studio access | Studio time is free; video concept, scripting, and on-camera talent are Client responsibility; WTE provides editing at discounted rate |
| Donor website launch before 501(c)(3) changeover | lilyofthevalleyministry.com domain transfer depends on Client completing nonprofit status transition |

---

## 18. Definition of Done

A phase or task is **Done** when all of the following are true:

### Code
- [ ] All acceptance criteria from WBS are implemented
- [ ] Code reviewed and approved via Pull Request
- [ ] No compiler warnings in release build
- [ ] All existing tests pass (`dotnet test Lotv.slnx`)
- [ ] New tests written for new code (unit and/or integration as appropriate)
- [ ] Code coverage at or above threshold for affected project

### Quality
- [ ] FluentValidation rules in place for all new API inputs
- [ ] No hardcoded secrets or connection strings
- [ ] Serilog logging on all significant operations (errors, warnings, key events)
- [ ] Global exception handler catches and logs unhandled exceptions

### Delivery
- [ ] Deployed to staging environment
- [ ] Demonstrated to Client (or async video walkthrough provided)
- [ ] Client feedback incorporated or deferred with written justification
- [ ] `MASTER_TODO.md` updated to reflect completed items
- [ ] Session notes written to `sessions/YYYY-MM-DD-title.md`
- [ ] Changes committed with conventional commit messages and pushed to `kremer-dev`

### Milestone-Specific
- [ ] Client acceptance received within 5-business-day window
- [ ] Invoice issued per payment schedule

---

## 19. Handoff & Closeout

At the completion of Phase 6, WTE delivers the following handoff package:

### 19.1 Documentation Package

| Document | Content |
|---|---|
| Deployment Guide | Step-by-step instructions for all three environments |
| Environment Variables Reference | All required env vars, placeholder values, where to find real values |
| Architecture Diagram | Final updated diagram reflecting production infrastructure |
| API Reference | OpenAPI spec + endpoint summary |
| Database Schema | EF Core entity model + migration history |
| Third-Party Accounts | Stripe, email provider, Azure — admin access handoff checklist |

### 19.2 Code & Repository

- Full git history on `main` branch
- All CI/CD workflow files in `.github/workflows/`
- Dockerfiles and `docker-compose.yml` in repository root
- All secrets removed from code; Key Vault configured

### 19.3 Post-Launch Support (Per MSA-LOTV)

- **30-day warranty period** after production launch
- Defects caused by WTE workmanship corrected at no charge during warranty
- After warranty: T&M or retainer per Attachment C Rate Card
- Emergency contact process defined in MSA Attachment D

### 19.4 Intellectual Property

Upon final payment (M6):
- Client owns all custom LOTV application code
- Client owns all data, configuration, and infrastructure
- WTE retains pre-existing frameworks, utilities, and reusable libraries
- Third-party open-source components remain under their respective licenses
- SBOM (Software Bill of Materials) delivered with handoff package

---

*Document maintained by WTE. Update after each phase completion and any Change Order execution.*
*Last updated: 2026-03-02 | Active branch: `kremer-dev` | Phase 0 of 6 complete.*
