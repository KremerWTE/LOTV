# Lily of the Valley (LOTV)

A SaaS Social Services Coordination Platform for a national Catholic nonprofit ministry that sends bereavement care packages to families who have experienced pregnancy and infant loss.

## What It Does

LOTV connects five types of users — families in need, donors, volunteers, chapter staff, and HQ admins — across a two-tier org structure (National HQ → Local Chapters). Core capabilities:

- **Case management** — intake via JotForm webhook, Kanban board with 9 process-stage columns, real-time updates via SignalR
- **Bereavement follow-up tracking** — 3-week / 3-month / 6-month / 11-month milestone system (Stephen's Ministry model)
- **Donor management** — contribution tracking by person, diocese, city, channel, and gift-size band; tax receipts (IRS §170 compliant PDF)
- **Event management** — galas, silent auctions, ticket sales, real-time auction bidding
- **Volunteer coordination** — auto-assignment scoring (Haversine proximity + skills + workload)
- **Dashboards** — impact, donation tracking, HQ cross-chapter rollup, public transparency page
- **Payment processing** — Stripe (subscriptions, webhooks, Customer Portal)
- **Notifications** — email (SendGrid), SMS (Twilio), Web Push (VAPID)

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 |
| API | ASP.NET Core Web API (`Lotv.Api`) |
| Frontend | **Blazor Server** (`Lotv.Web`) |
| Domain | Class Library (`Lotv.Core`) |
| Database | SQL Server 2019 (`Lotv.Migrations.SqlServer` for EF migrations) |
| Real-time | SignalR (`RequestsHub`, `AuctionHub`) |
| Auth | ASP.NET Core Identity + JWT |
| Payments | Stripe.net SDK |
| PDF | QuestPDF |
| Testing | xUnit + Playwright (E2E) |
| Solution file | `Lotv.slnx` |

> **Note:** The frontend was converted from Blazor WebAssembly to Blazor Server on 2026-08-31. It is a standard Kestrel app — not static hosting.

## Development Status

| Phase | Name | Status |
|-------|------|--------|
| 0 | Foundation | ✅ Complete |
| 1 | Architecture & Design | ✅ Complete |
| 2 | Core Domain | ✅ Complete |
| 3 | API | ✅ Complete |
| 4 | Frontend | ✅ Complete |
| 5 | Testing | ✅ Complete — 86.4% line / 94.3% branch coverage; 433/433 tests passing |
| 6 | Deployment & Launch | 🔄 In Progress — code complete; blocked on Azure/IIS provisioning and DB credential rotation |

**Production database:** SQL Server 2019 at `10.100.1.87` (on-premises). Real ministry data imported — 1,046 cases, 425 Mother's Day mailing entries, 47 bereavement follow-up trackers.

## Getting Started

### Prerequisites

- .NET 10 SDK
- (Optional) Docker Desktop for local containerized dev

### Build

```bash
dotnet build Lotv.slnx
```

> Always use `Lotv.slnx`. There is no `Lotv.sln` — it does not exist.

### Run locally (direct)

```bash
# API (port 5275)
cd src/Lotv.Api
dotnet run

# Web (port 5001, separate terminal)
cd src/Lotv.Web
dotnet run
```

Local dev uses SQLite. The API must run before the Web app can load.

### Run locally (Docker)

```bash
docker compose up
```

Both containers start with health-check gating. The Web container waits for the API's `/health` endpoint before accepting traffic.

### Run tests

```bash
# Unit + integration tests with coverage
dotnet test tests/Lotv.Tests/Lotv.Tests.csproj \
  --configuration Release \
  --collect "XPlat Code Coverage" \
  --settings tests/Lotv.Tests/coverlet.runsettings

# E2E tests (requires API + Web running)
E2E_BASE_URL=http://localhost:5001 \
E2E_API_URL=http://localhost:5275 \
dotnet test tests/Lotv.E2E/Lotv.E2E.csproj
```

## Branch Strategy

| Branch | Purpose |
|--------|---------|
| `main` | Protected — production state; no direct commits |
| `kremer-dev` | Primary development branch |
| `pateep_dev_branch` | Active working branch |

Merge to `main` via PR. The real org repo is `wtesolutions/LOTV`; `KremerWTE/LOTV` is a personal fork.

## CI/CD

| Workflow | Trigger | Status |
|----------|---------|--------|
| `ci.yml` | PR / push to `main` | ✅ Active |
| `deploy-staging.yml` | Manual | ❌ Blocked — Azure secrets not set |
| `deploy-production.yml` | Semver tag push | ❌ Blocked — Azure secrets not set |

See `docs/ci-cd-pipeline-reference.md` for full pipeline documentation.
See `docs/iis-deployment-notes.md` for IIS vs. Azure deployment decision (pending).

## Key Documents

| Document | Location |
|----------|---------|
| Phase tracker | `MASTER_TODO.md` |
| Quick startup context | `NEXT_STEPS.md` |
| Project structure | `PROJECT_STRUCTURE.md` |
| API contract | `docs/api-contract.md` |
| Data model | `docs/data-model.md` |
| Auth design | `docs/auth-design.md` |
| Environment / secrets reference | `docs/environment-config.md` |
| Gap report | `docs/gap-report-2026-09-09.md` |
| CI/CD reference | `docs/ci-cd-pipeline-reference.md` |
| IIS deployment notes | `docs/iis-deployment-notes.md` |
| Claude Code session directives | `.claude/SESSION_STARTUP_DIRECTIVE.md` |

## Important Constraints

- **JotForm:** The ministry's JotForm account (`261395566857171`) has a signed HIPAA Business Associate Agreement. All programmatic API writes are silently discarded by JotForm's platform. Every form change requires the builder UI — no exceptions.
- **Production DB:** SQL Server at `10.100.1.87` holds real family PII. No AI session should connect directly. Credential rotation to a least-privilege `lotv_app` login is pending (`src/Lotv.Migrations.SqlServer/rotate-app-credential.sql`).
- **Secrets:** Never commit connection strings, API keys, or credentials. Use `dotnet user-secrets` for local dev. Deploy-time secrets are injected into `appsettings.Production.json` by the CI pipeline.
