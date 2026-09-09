# Project Structure — LOTV

**Project:** Lily of the Valley (LOTV) — SaaS Social Services Coordination Platform
**Stack:** .NET 9 · ASP.NET Core Web API · Blazor Server · SQL Server 2019 · xUnit · Playwright
**Last Updated:** 2026-09-09

---

## Repository Root

```
LOTV/
├── .claude/                        # Claude Code session directives (committed)
├── .github/
│   └── workflows/                  # GitHub Actions CI/CD workflows
├── data/                           # Seed data and reference lookups (protected by inner .gitignore)
├── docs/                           # Architecture specs, API contracts, design docs, reports
│   ├── adr/                        # Architecture Decision Records (ADR-001 through ADR-005)
│   └── templates/                  # Document templates
├── scripts/                        # Dev automation: migrations, seed, deploy utilities
├── sessions/                       # Per-session notes (YYYY-MM-DD-brief-title.md)
├── src/                            # Application source code
├── tests/                          # Test projects
├── tools/                          # One-time utilities (e.g., LegacyImport)
│
├── .dockerignore
├── .gitignore
├── docker-compose.yml              # Local dev stack (API + Web + SQLite volume)
├── Lotv.slnx                       # .NET 9 solution file (always use this — not .sln)
├── MASTER_TODO.md                  # Phased task tracker — all phases, all tasks
├── NEXT_STEPS.md                   # Quick startup context (~400 tokens, 5-7 immediate tasks)
├── README.md
└── SESSION_STARTUP_DIRECTIVE.md    # Legacy startup directive (superseded by .claude/)
```

---

## Source Projects (`src/`)

```
src/
├── Lotv.Api/                       # ASP.NET Core Web API (.NET 9)
│   ├── Controllers/                # REST endpoint controllers
│   ├── Data/                       # EF Core DbContext, SeedData, migrations config
│   ├── Hubs/                       # SignalR hubs (RequestsHub, AuctionHub)
│   ├── Services/                   # Service implementations
│   ├── Middleware/                 # Security headers, global exception handler
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── appsettings.Staging.json
│   ├── appsettings.Production.json  # Placeholder — real values injected at deploy time
│   └── Program.cs                  # App bootstrap, DI registration, JotForm webhook handler
│
├── Lotv.Core/                      # Domain models, interfaces, shared logic (.NET 9)
│   ├── Models/                     # All entity types (Family, PackageRequest, Donor, Volunteer, etc.)
│   ├── Interfaces/                 # Service contracts (IServiceRequestService, IDonorService, etc.)
│   ├── Services/                   # Domain service implementations
│   ├── DTOs/                       # Data transfer objects
│   └── Enums/                      # Domain enumerations
│
├── Lotv.Migrations.SqlServer/      # SQL Server-specific EF Core migrations (.NET 9)
│   ├── Migrations/                 # InitialCreate + subsequent migrations
│   ├── baseline-existing-database.sql  # Run once on prod DB before first CI deploy
│   └── rotate-app-credential.sql      # Credential rotation script (sa → lotv_app)
│
└── Lotv.Web/                       # Blazor Server frontend (.NET 9)
    ├── Components/                 # Shared Razor components (charts, modals, toasts)
    ├── Pages/                      # All 312 @page Razor views organized by role/feature
    │   ├── Admin/                  # Staff and admin views (Dashboard, Kanban, Queue, etc.)
    │   ├── Donor/                  # Donor self-service portal
    │   ├── Public/                 # Public-facing pages (Home, Apply, Give, Events, etc.)
    │   └── Volunteer/              # Volunteer views
    ├── Services/                   # Client-side services (ApiService, AuthService, SignalRService)
    ├── Layout/                     # AdminLayout, PublicLayout, auth guards
    ├── wwwroot/                    # Static assets (CSS, JS, images)
    └── Program.cs                  # Blazor Server bootstrap, HttpClient, auth config
```

---

## Test Projects (`tests/`)

```
tests/
├── Lotv.Tests/                     # xUnit — unit + integration tests
│   ├── Unit/                       # Service unit tests (Core logic)
│   │   ├── ServiceRequestServiceTests.cs      (16 tests)
│   │   ├── WorkloadServiceTests.cs            (10 tests)
│   │   ├── AllocationServiceTests.cs          (10 tests)
│   │   ├── DashboardServiceTests.cs           (15 tests)
│   │   ├── ReportingServiceTests.cs           (15 tests)
│   │   ├── AutoAssignmentServiceTests.cs      (14 tests)
│   │   └── ModelCoverageTests.cs              (77 tests — all domain model types)
│   ├── Integration/                # API integration tests (WebApplicationFactory + SQLite)
│   │   ├── AuthTests.cs
│   │   ├── RequestTests.cs
│   │   ├── DashboardTests.cs
│   │   ├── PublicApiTests.cs                  (35 tests)
│   │   ├── JotFormWebhookTests.cs             (11 tests — webhook parsing + tracker creation)
│   │   └── ControllerAuthorizationTests.cs    (20+ tests)
│   └── coverlet.runsettings        # Excludes EF migrations from coverage count
│
└── Lotv.E2E/                       # Playwright end-to-end tests (Chromium)
    ├── BrowserFixture.cs           # Shared Chromium instance
    ├── E2ETestBase.cs              # Per-test context, page, helpers
    ├── PublicPagesTests.cs
    ├── AuthFlowTests.cs
    ├── ApplyFlowTests.cs
    ├── DonationFlowTests.cs
    ├── VolunteerFlowTests.cs
    ├── AdminPagesTests.cs
    ├── MobileResponsivenessTests.cs
    └── AccessibilityTests.cs       # WCAG 2.1 AA checks
```

**Test coverage (last measured):** 86.4% line / 94.3% branch / 80.5% method on `Lotv.Core`
**Total tests:** 433/433 passing

---

## GitHub Actions Workflows (`.github/workflows/`)

| File | Trigger | Purpose | Runnable |
|------|---------|---------|---------|
| `ci.yml` | PR to `main`/`kremer-dev`; push to `main` | Build, publish, unit+integration+E2E tests, coverage comment | ✅ Yes |
| `deploy-staging.yml` | Manual (`workflow_dispatch`) | Test gate → EF migrate → publish → Azure App Service staging | ❌ Blocked (no Azure secrets) |
| `deploy-production.yml` | Semver tag push (`v*.*.*`) | Test gate → EF migrate → publish → Azure App Service prod + GitHub Release | ❌ Blocked (no Azure secrets) |

See `docs/ci-cd-pipeline-reference.md` for full pipeline documentation.
See `docs/iis-deployment-notes.md` for IIS migration analysis (pending decision).

---

## Claude Code Directives (`.claude/`)

| File | Purpose |
|------|---------|
| `CLAUDE.md` | Entry-point — how to start a session |
| `SESSION_STARTUP_DIRECTIVE.md` | Startup modes (Quick/Balanced/Comprehensive), session end protocol |
| `CRITICAL_RULES_CONSOLIDATED.md` | All mandatory rules in one file — git safety, session docs, file org |
| `FILE_ORGANIZATION_DIRECTIVE.md` | Where every file type belongs |
| `git-workflow-directive.md` | Git safety rules, branch strategy, commit format |
| `SESSION_SUMMARY_TEMPLATE.md` | Template for per-session notes |
| `settings.local.json` | ⚠️ NOT committed — user-specific MCP tool permissions |

---

## Documentation (`docs/`)

| File/Folder | Purpose |
|-------------|---------|
| `api-contract.md` | REST API endpoint reference (60+ endpoints) |
| `data-model.md` | Entity relationship diagram and data model |
| `auth-design.md` | Authentication strategy, role hierarchy, permission matrix |
| `auto-assignment-algorithm.md` | Volunteer scoring algorithm design |
| `environment-config.md` | Full secrets and environment variable reference |
| `smoke-test-checklist.md` | Pre-launch verification checklist (8 sections) |
| `disaster-recovery-runbook.md` | DR procedures |
| `owasp-review.md` | OWASP Top 10 review; 5 high-priority pre-launch items |
| `privacy-compliance.md` | GDPR/CCPA compliance notes, PII handling |
| `ci-cd-pipeline-reference.md` | CI/CD workflow documentation |
| `iis-deployment-notes.md` | IIS migration analysis vs. current Azure App Service approach |
| `gap-report-2026-09-09.md` | Project gap analysis (this report) |
| `LOTV-PM-Plan.md` | Project Management Plan (WBS, risk, comms, milestones) |
| `MSA-LOTV.md` / `.docx` | Master Services Agreement |
| `SOW-LOTV-001-FullPlatform.md` / `.docx` | Statement of Work |
| `LOTV-Project-Plan.html` | Self-contained HTML project site |
| `adr/` | Architecture Decision Records (ADR-001 through ADR-005) |
| `mockup-*.html` | UI mockups for client review |

---

## Key Configuration Files

| File | Purpose |
|------|---------|
| `Lotv.slnx` | .NET 9 solution file — **always use this** (`dotnet build Lotv.slnx`) |
| `docker-compose.yml` | Local dev: API + Web + SQLite volume, healthcheck-gated startup |
| `.gitignore` | .NET standard + LOTV-specific exclusions (PII source files, `.claude/settings.local.json`) |
| `data/` | Reference data (dioceses, seed CSVs) — inner `.gitignore` blocks raw exports and databases |

---

## Infrastructure

| Component | Technology | Status |
|-----------|-----------|--------|
| Backend API | ASP.NET Core Web API (.NET 9) | ✅ Complete |
| Frontend | Blazor Server (.NET 9) | ✅ Complete (converted from WASM 2026-08-31) |
| Domain | Class Library (.NET 9) | ✅ Complete |
| Auth | ASP.NET Core Identity + JWT | ✅ Complete |
| Database | SQL Server 2019 at `10.100.1.87` | ✅ Live (real ministry data imported) |
| Real-time | SignalR (`RequestsHub`, `AuctionHub`) | ✅ Complete |
| Payments | Stripe.net SDK | ✅ Code complete — account setup pending |
| Email | SendGrid (or chosen provider) | ✅ Code complete — credentials pending |
| SMS | Twilio | ✅ Code complete — credentials pending |
| Push Notifications | Web Push (VAPID) | ✅ Code complete — VAPID keys pending |
| PDF Receipts | QuestPDF | ✅ Complete |
| Background Jobs | .NET hosted services (FxRefresh, MagicLinkCleanup, WebhookCleanup) | ✅ Complete |
| EF Migrations | `Lotv.Migrations.SqlServer` project | ✅ Complete — baseline script pending on prod |
| CI/CD | GitHub Actions | ✅ Built — Azure infra pending |
| Hosting | Azure App Service (planned) or IIS (decision pending) | ⏳ Not yet provisioned |

---

## Branch Strategy

| Branch | Purpose |
|--------|---------|
| `main` | Protected — production state; no direct commits |
| `kremer-dev` | Primary development branch (push then PR to main) |
| `pateep_dev_branch` | Active working branch (created 2026-09-09) |
| `stage` | Staging environment branch |
