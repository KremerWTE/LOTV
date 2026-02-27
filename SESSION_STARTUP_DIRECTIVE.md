# LOTV Session Startup Directive
**Project: Lily of the Valley (LOTV)** — SaaS Social Services Coordination Platform

---

## Purpose
This document defines the mandatory startup protocol for every Claude Code session on the LOTV project. Follow every step before beginning any implementation work.

---

## Startup Protocol

### Step 1: Read Context Files
Read the following files at the start of every session:
- `MASTER_TODO.md` — current phase and open tasks
- `MEMORY.md` (if present in `.claude/projects/`) — accumulated project knowledge
- Any relevant session notes in `sessions/` for recent context

### Step 2: Check Project Health
Run the following to establish baseline status:
```bash
dotnet build Lotv.slnx
dotnet test Lotv.slnx
git status
git log --oneline -10
```
Note any build errors, test failures, or uncommitted changes before proceeding.

### Step 3: Identify Current Phase
Determine which phase of `MASTER_TODO.md` is active and what tasks are in progress or blocked.

### Step 4: Produce Startup Summary
Before beginning work, output a summary in this format:

```
## Session Startup Summary — LOTV
- Build status: [PASS / FAIL — describe errors]
- Test status:  [PASS / FAIL — describe failures]
- Git status:   [clean / N uncommitted files / branch name]
- Current phase: [Phase X — Name]
- Active tasks: [list from MASTER_TODO]
- Blocked tasks: [list, with reason]
- Planned work this session: [brief description]
```

---

## Project Context

**Lily of the Valley (LOTV)** is a .NET 9 SaaS platform connecting people in need with community support resources.

### User Types / Roles
| Role | Description |
|---|---|
| **Person in Need** | Service recipient requesting help (food, shelter, transportation, etc.) |
| **Donor** | Individual or organization contributing money or resources |
| **Local Helper / Volunteer** | Community member providing hands-on services in their area |
| **Employee / Staff** | Internal user managing requests, coordinating services |
| **Admin** | Platform administrator with full system access |

### Core Feature Areas
- **Request Management** — intake, routing, assignment, escalation, and fulfillment of service requests
- **Donor Management** — monetary + resource contribution tracking, receipts, tax receipts, donor communications; donors are associated to a diocese and city
- **Volunteer Coordination** — matching helpers to open requests by location/skill
- **Staff Dashboard** — case management (kanban + queue views), workload tracking, marketing emails, reporting
- **Impact & Distribution Dashboard** — real-time view of money and resources sent, broken down by recipient, category, region, and time period; public transparency page
- **Donation Tracking Dashboard** — donations broken down by person, diocese, city, amount band, and donation channel (online / check / cash / in-person / event / etc.)
- **Event Management** — gala, silent auction, dinner, and other fundraising events; ticket sales, attendee check-in, auction bidding, event revenue reporting; event revenue flows back into donation tracking
- **Notifications** — multi-channel alerts (email, SMS) for all user types
- **Payment Processing** — secure donation intake (Stripe or equivalent), webhook handling, reconciliation, event ticket payments

### Technology Stack
- **Backend**: ASP.NET Core Web API (.NET 9) — `Lotv.Api`
- **Frontend**: Blazor WebAssembly (.NET 9) — `Lotv.Web`
- **Domain Logic**: Class Library (.NET 9) — `Lotv.Core`
- **Testing**: xUnit — `Lotv.Tests`
- **Auth**: TBD (likely ASP.NET Core Identity + JWT or Azure AD B2C)
- **Database**: TBD (likely EF Core + SQL Server or PostgreSQL)

---

## Repository Structure

```
LOTV/
├── .claude/                    # Claude Code settings (settings.local.json)
├── .github/
│   └── workflows/              # GitHub Actions CI/CD workflows
├── data/                       # Seed data, reference lookups, export templates
│                               # (.gitignore inside protects sensitive files)
├── docs/                       # Architecture specs, API contracts, design docs, MSA/SOW
│   └── templates/              # Document templates
├── scripts/                    # Dev automation: migrations, seed, deploy utilities
├── sessions/                   # Per-session notes and summaries (YYYY-MM-DD-title.md)
├── src/
│   ├── Lotv.Api/               # ASP.NET Core Web API
│   ├── Lotv.Core/              # Domain models, interfaces, shared logic
│   └── Lotv.Web/               # Blazor WebAssembly frontend
├── tests/
│   └── Lotv.Tests/             # xUnit test project
├── .gitignore
├── Lotv.slnx                   # .NET 9 solution file (XML format)
├── MASTER_TODO.md              # Phased development task tracker
├── README.md
└── SESSION_STARTUP_DIRECTIVE.md  ← this file
```

---

## Key Files

| File / Folder | Purpose |
|---|---|
| `Lotv.slnx` | Solution root (.NET 9 XML format — always use this, not `.sln`) |
| `src/Lotv.Api/` | ASP.NET Core Web API project |
| `src/Lotv.Web/` | Blazor WebAssembly frontend |
| `src/Lotv.Core/` | Domain models, interfaces, shared logic |
| `tests/Lotv.Tests/` | xUnit test project |
| `MASTER_TODO.md` | Phased development task tracker (update every session) |
| `docs/` | Architecture specs, API contracts, MSA/SOW, design docs |
| `docs/templates/` | Reusable document templates |
| `sessions/` | Per-session notes — one file per session (`YYYY-MM-DD-title.md`) |
| `.github/workflows/` | GitHub Actions workflows (CI, deploy) |
| `scripts/` | Dev automation scripts (migration helpers, seed, deploy utilities) |
| `data/` | Seed data, reference dioceses, lookups, export templates |
| `.claude/settings.local.json` | Claude Code permission settings |
| `.gitignore` | .NET + project-specific git exclusions |

---

## Critical Rules

1. **No sensitive data in commits** — no connection strings, secrets, API keys, or PII in source
2. **Conventional commits** — use `feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:` prefixes
3. **No AI attribution** — do not add "Generated by Claude" or similar to code or docs
4. **Ask before deleting** — never delete files, migrations, or database objects without confirmation
5. **Small, focused commits** — commit logically grouped changes; avoid giant all-in-one commits
6. **Update MASTER_TODO after every session** — keep phase status and task completion accurate
7. **Always use `Lotv.slnx`** — `dotnet build Lotv.sln` will fail; the solution is `.slnx` format
8. **Data folder is protected** — `data/.gitignore` prevents raw exports/databases from being committed; only `.md` reference docs go in `data/`
9. **Branch strategy** — feature work on `kremer-dev`; merge to `main` via PR after review

---

## Session End Protocol

Before closing a session:
1. Commit any completed work with a conventional commit message
2. Update `MASTER_TODO.md` — mark completed tasks, add newly discovered tasks
3. Write a session note to `sessions/YYYY-MM-DD-brief-title.md`
4. Update `MEMORY.md` with any new stable patterns, decisions, or lessons learned
5. Push to `kremer-dev` branch: `git push origin kremer-dev`
