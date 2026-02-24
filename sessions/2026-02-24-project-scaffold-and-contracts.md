# Session Notes — 2026-02-24
**Title:** Project Scaffold, Plan Build-Out, and Contract Documents
**Duration:** Full session

---

## What Was Accomplished

### 1. Project Scaffold (Phase 0 — complete)
- Created `.NET 9` solution (`Lotv.slnx`) with four projects:
  - `src/Lotv.Api` — ASP.NET Core Web API
  - `src/Lotv.Web` — Blazor WebAssembly
  - `src/Lotv.Core` — Class Library (domain logic)
  - `tests/Lotv.Tests` — xUnit
- Added project references: Api → Core, Web → Core, Tests → Core + Api
- `dotnet build Lotv.slnx` — 0 errors, 0 warnings ✅
- `dotnet test Lotv.slnx` — 1 passed, 0 failed ✅
- Created `.claude/settings.local.json` (allows `dotnet:*` and `git:*`)
- Created `SESSION_STARTUP_DIRECTIVE.md`
- Created `MASTER_TODO.md`
- Created `docs/` and `sessions/` placeholder folders
- Created `.gitignore` for .NET projects
- **Note:** Solution file is `Lotv.slnx` (not `.sln`) — .NET 9 XML format

### 2. MASTER_TODO.md — Full Plan Built Out
Iteratively expanded across the session based on user feedback. Final plan covers:

- **Phase 0** — Foundation (complete)
- **Phase 1** — Architecture & Design (diocese model, event revenue model, escalation rules, payment processor, secrets management, API versioning, 4 ADRs)
- **Phase 2** — Core Domain (all entities including Diocese, RequestNote, RequestActivity, RequestAssignment, FundraisingEvent, SilentAuctionItem, AuctionBid, DonationByPersonRow, DonationByDioceseRow, DonationByChannelRow, etc.; 14 service interfaces; full enum/value object set)
- **Phase 3** — API (60+ endpoints covering requests, task management, donors, dioceses, contributions, payments/Stripe, volunteers, allocations, events/auctions, dashboard, donation tracking, reporting)
- **Phase 4** — Frontend (all role-based views: Person in Need, Donor, Volunteer, Staff with Kanban + Workload + My Queue, Impact Dashboard, Donation Tracking Dashboard by person/diocese/city/channel/amount, Event Management views)
- **Phase 5** — Testing
- **Phase 6** — Deployment & Launch

### 3. Key Features Added Through Review
| Feature | Added In |
|---|---|
| Impact & Distribution Dashboard | Review #1 |
| Request task management (assign, accept/decline, priority, due date, escalation, notes, activity log) | Review #2 |
| Staff Kanban board, Workload View, My Work Queue, Unassigned Queue | Review #2 |
| Donation tracking by person, diocese, city, channel, amount band | Review #3 |
| Diocese as first-class entity on Donor profile | Review #3 |
| DonationChannel enum (Online/Check/Cash/InPerson/Mail/Event/Other) | Review #3 |
| Event management (Gala, Silent Auction, Dinner, etc.) with ticket sales, check-in, auction bidding | Review #3 |

### 4. Contract Documents Created
- `docs/MSA-LOTV.md` / `docs/MSA-LOTV.docx` — Master Services Agreement adapted from MSA1984 (Pivot Clinical Services template)
- `docs/SOW-LOTV-001-FullPlatform.md` / `docs/SOW-LOTV-001-FullPlatform.docx` — Full platform SOW adapted from SOW1982 (New Energy LLC template), covering all 6 phases with milestone payment structure and 14 client deliverables
- `docs/templates/` — source reference templates archived here

### 5. Folder Cleanup
- Deleted Word temp lock files (`~$*.docx`)
- Moved source template docs to `docs/templates/`
- Deleted `src/Lotv.Core/Class1.cs` (scaffold placeholder)
- Created `.gitignore`

---

## Decisions Made / Notes for Next Session
- Solution file is `Lotv.slnx` — always use this, not `Lotv.sln`
- Phase 0 is fully complete except git init + initial commit (still pending)
- Phase 1 (Architecture & Design) is the next phase — key decisions needed before coding:
  - Auth strategy (ASP.NET Core Identity + JWT vs. Azure AD B2C)
  - Database (SQL Server vs. PostgreSQL)
  - Payment processor (Stripe recommended)
  - Email provider
  - Hosting (Azure recommended)
  - Diocese seed list from client
- Contract documents have placeholders: client org name, SOW number, effective date, rates, milestone amounts — need to be filled in before sending

---

## Phase 0 Status at End of Session
- [x] All scaffold tasks complete
- [x] Build passing
- [x] Tests passing
- [x] .gitignore created
- [ ] `git init` — not yet done
- [ ] Initial commit — not yet done
