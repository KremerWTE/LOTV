# File Organization Directive — LOTV (Restrictive Tier v1.4)

**Purpose:** Ensure clean project structure and proper file placement
**Version:** v1.4 (Restrictive Tier)
**Enforcement:** MANDATORY

---

## 🎯 Core Principle

**Minimal Project Root** — Keep root directory clean and organized.

**Rule:** NEVER create files in project root without checking the allowed list first.

---

## ✅ Allowed in Project Root

- `README.md`, `MASTER_TODO.md`, `NEXT_STEPS.md`
- `SESSION_STARTUP_DIRECTIVE.md` (legacy, kept for compatibility)
- `Lotv.slnx`, `docker-compose.yml`
- `.gitignore`, `.gitattributes`
- `LOTV Presentation Plan.pdf`

**Everything else → Subdirectories**

---

## 📁 LOTV Directory Structure

```
LOTV/
├── .claude/                    # Claude Code directives (committed; settings.local.json excluded)
├── .github/workflows/          # GitHub Actions CI/CD workflows
├── data/                       # Seed data, reference lookups (.gitignore inside protects sensitive files)
├── docs/                       # Architecture specs, API contracts, design docs, MSA/SOW
│   └── templates/              # Document templates
├── scripts/                    # Dev automation: migrations, seed, deploy utilities
├── sessions/                   # Per-session notes (YYYY-MM-DD-brief-title.md)
├── src/
│   ├── Lotv.Api/               # ASP.NET Core Web API
│   ├── Lotv.Core/              # Domain models, interfaces, shared logic
│   ├── Lotv.Migrations.SqlServer/ # SQL Server-specific EF migrations
│   └── Lotv.Web/               # Blazor WebAssembly frontend
├── tests/
│   └── Lotv.Tests/             # xUnit test project (unit + integration + E2E)
├── tools/                      # One-time utilities (e.g., LegacyImport)
└── [root files per allowed list above]
```

---

## 📝 Planning Document Purposes (CRITICAL)

### MASTER_TODO.md
**Purpose:** Comprehensive task tracking — ALL phases, all TODO items, completed items

**Content:** Every task (planned and ad-hoc), completion status, phase context, session links

**What NOT to include:** Immediate "what to do next" context (→ NEXT_STEPS.md)

### NEXT_STEPS.md
**Purpose:** Quick startup context (~400 tokens)

**Content:**
- Next 5-7 immediate tasks ONLY
- Recent completions (last 2-3 items)
- Current phase and progress
- Quick links to other docs
- Critical reminders

**STRICT CONTENT LIMITS:**
- ❌ NO session summaries
- ❌ NO code blocks or scripts
- ❌ NO detailed analysis
- ❌ NO more than 3 recent completions
- ✅ TARGET: 30-40 lines (~400-500 tokens)

---

## 📝 Session Notes Location (STRICTLY ENFORCED)

**Pattern:** `sessions/YYYY-MM-DD-brief-title.md`

**Examples:**
- ✅ `sessions/2026-09-09-v14-directive-onboarding.md`
- ✅ `sessions/2026-08-31-hosting-blazor-server-conversion-and-ci-fixes.md`
- ❌ `sessions/notes.md` (wrong pattern)
- ❌ `session-2026-09-09.md` (wrong location)

---

## 🔄 File Placement Decision Tree

```
1. Is it configuration or build file?
   YES → Project root (if on allowed list)
   NO → Continue to 2

2. Is it documentation?
   YES → docs/ (or docs/templates/ for templates)
   NO → Continue to 3

3. Is it a session note/summary?
   YES → sessions/YYYY-MM-DD-brief-title.md
   NO → Continue to 4

4. Is it source code or test?
   YES → src/, tests/, or tools/
   NO → Continue to 5

5. Is it a script or automation tool?
   YES → scripts/ or tools/
   NO → Continue to 6

6. Is it a Claude directive?
   YES → .claude/
   NO → STOP and ask where it belongs
```

---

## 📊 Quick Reference

**Root Files (Allowed ONLY):**
- README.md, MASTER_TODO.md, NEXT_STEPS.md, SESSION_STARTUP_DIRECTIVE.md
- Lotv.slnx, docker-compose.yml, .gitignore

**Documentation:**
- Specs/contracts → `docs/`
- Templates → `docs/templates/`

**Session Notes (MANDATORY pattern):**
- `sessions/YYYY-MM-DD-brief-title.md`

**Directives:**
- `.claude/*.md` (committed)
- NEVER commit: `.claude/settings.local.json`

---

**Tier:** Restrictive (Maximum Safety) | **Version:** v1.4
**Adapted for:** LOTV — Lily of the Valley SaaS Platform
**Last Updated:** 2026-09-09
