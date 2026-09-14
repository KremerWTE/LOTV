# CRITICAL RULES — CONSOLIDATED (LOTV)

**Purpose:** All MANDATORY and CRITICAL directives in one place
**Status:** STRICTLY ENFORCED - NO EXCEPTIONS
**Version:** v1.4 (Restrictive Tier)
**Last Updated:** 2026-09-09

---

## ⚠️ CRITICAL - Read This First

This document contains **ALL mandatory rules** for the LOTV project. These rules are **STRICTLY ENFORCED** and must be followed in EVERY session.

**If you only read one directive file, read this one.**

---

## 🚨 1. GIT SAFETY RULES

### 1.1 Branch Safety Check (MANDATORY at EVERY Startup)

```bash
git branch --show-current
```

**Protected Branches (NO COMMITS):**
- **Exact Match:** `main`, `master`, `dev`, `develop`
- **Contains Pattern:** `beta`, `stage`, `staging`, `production`, `prod`, `deploy`, `release`

**Safe Branches for LOTV:**
- ✅ `kremer-dev` — primary working branch
- ✅ `feature/*`, `fix/*`, `bugfix/*`, `hotfix/*`

**If on `main` → MANDATORY Action:**
```bash
git checkout kremer-dev
```

**⚠️ STOP and WARN USER if on protected branch before ANY commit.**

### 1.2 Sync with Remote (REQUIRED at Startup)

```bash
git fetch origin && git pull origin $(git branch --show-current)
```

### 1.3 Prohibited Git Operations (NEVER without Explicit Confirmation)

❌ **Force Push** — `git push --force` or `git push -f`
❌ **Commit to Protected Branches** — main, master, dev, *stage*, *prod*
❌ **Delete Remote Branches** — `git push origin --delete branch-name`
❌ **Amend Pushed Commits** — `git commit --amend` after `git push`
❌ **Add AI Co-Author Attribution** — NO "Co-Authored-By: Claude"
❌ **Commit Secrets/Credentials** — NO connection strings, API keys, PII

### 1.4 Required Git Operations (ALWAYS)

✅ **Branch safety check** — EVERY session start
✅ **Sync with remote** — Before starting work
✅ **Conventional commit format** — ALL commits
✅ **Push all commits** — At session end (push to `kremer-dev`)
✅ **Verify git status** — Before ending session

### 1.5 Conventional Commit Format (MANDATORY)

**Required Format:** `type(scope): description`

**Types:** `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `chore`, `ci`, `style`

**Requirements:**
- Minimum 10 characters
- Describe WHY, not just WHAT
- Be specific and clear

**Examples:**
- ✅ `feat(webhook): wire follow-up tracker creation on intake`
- ✅ `fix(api): correct knownLabels for Quarterly spelling fix`
- ❌ `update code` (no type, too vague)

---

## 📋 2. SESSION DOCUMENTATION RULES

### 2.1 Session Notes (MANDATORY)

**CRITICAL:** Session notes are MANDATORY after:
- ✅ Bug fixes (any severity)
- ✅ New features or enhancements
- ✅ Updates to existing functionality
- ✅ Refactoring or optimization work
- ✅ Configuration or settings changes
- ✅ Any work that modifies code, templates, or directives

**Location:** `sessions/YYYY-MM-DD-brief-title.md`
- Use the existing `sessions/` directory (flat — no recent/archive subdirs for LOTV)

**Use Template:** `.claude/SESSION_SUMMARY_TEMPLATE.md`

### 2.2 Ad-Hoc Work Tracking (MANDATORY)

ALL unplanned work MUST be tracked.

**At Start of Unplanned Work:**
- Add to NEXT_STEPS.md (REQUIRED for all work)
- Add to MASTER_TODO.md if significant (>30 min)

**At End of Work:**
- Update NEXT_STEPS.md — mark complete, move to Recent
- Update MASTER_TODO.md if added
- Create session notes (MANDATORY)

### 2.3 NEXT_STEPS.md Maintenance (CRITICAL)

**Update ALWAYS at:** phase transitions, major milestones, significant completions

**STRICT CONTENT LIMITS:**
- Header: 5 lines (date, phase, branch)
- Current work: 10-15 lines (5-7 tasks)
- Recent completion: 5-10 lines (2-3 items)
- Quick links: 5 lines
- Critical reminders: 5 lines
- **TARGET: 30-40 lines (~400-500 tokens)**

**What to EXCLUDE (MANDATORY):**
- ❌ NO complete session summaries
- ❌ NO code blocks or scripts
- ❌ NO detailed analysis or findings
- ❌ NO historical context beyond last 2-3 items
- ❌ NO more than 3 recent completions

---

## 📂 3. FILE ORGANIZATION RULES

### 3.1 Project Root Files (ONLY These Allowed)

- `README.md`, `MASTER_TODO.md`, `NEXT_STEPS.md`
- `Lotv.slnx`, `docker-compose.yml`, `.gitignore`
- `SESSION_STARTUP_DIRECTIVE.md` (legacy — kept for compatibility)

**Everything else → Subdirectories**

### 3.2 Planning Document Purposes (CRITICAL)

**MASTER_TODO.md** — Comprehensive task tracking (ALL TODO items, completed, in-progress)
**NEXT_STEPS.md** — Quick startup context (~400 tokens, 5-7 immediate tasks ONLY)
**sessions/** — Per-session notes and summaries

### 3.3 Session Notes Location (STRICTLY ENFORCED)

**Pattern:** `sessions/YYYY-MM-DD-brief-title.md`
- ✅ `sessions/2026-09-09-v14-directive-onboarding.md`
- ❌ `sessions/session-notes.md` (wrong pattern)

### 3.4 Claude Configuration Rules (MANDATORY)

**Shared Directives (MUST be committed):**
- ✅ `.claude/CLAUDE.md`
- ✅ `.claude/SESSION_STARTUP_DIRECTIVE.md`
- ✅ `.claude/FILE_ORGANIZATION_DIRECTIVE.md`
- ✅ `.claude/git-workflow-directive.md`
- ✅ `.claude/SESSION_SUMMARY_TEMPLATE.md`
- ✅ `.claude/CRITICAL_RULES_CONSOLIDATED.md`

**User-Specific Settings (MUST NOT be committed):**
- ❌ `.claude/settings.local.json` — must be in `.gitignore`

---

## 🔄 4. SESSION END PROTOCOL

**Required at EVERY session end:**

1. ✅ **Update NEXT_STEPS.md** (if significant work)
2. ✅ **Update MASTER_TODO.md** (if major work — phase completion, significant deliverables)
3. ✅ **Create session summary** (MANDATORY — use SESSION_SUMMARY_TEMPLATE.md)
   - Save to: `sessions/YYYY-MM-DD-brief-title.md`
4. ✅ **Verify checklist:**
   - [ ] NEXT_STEPS.md accurate
   - [ ] MASTER_TODO.md updated (if needed)
   - [ ] Session summary created
   - [ ] All commits pushed to `kremer-dev`
   - [ ] On safe branch
5. ✅ **Push all commits** to `kremer-dev` (REQUIRED)

---

## ⚡ 5. STARTUP PROTOCOL

**Quick Mode (80% of sessions):**
1. `git branch --show-current` — verify on `kremer-dev`
2. `git fetch origin && git pull origin kremer-dev`
3. Read `NEXT_STEPS.md` ONLY
4. Present summary

**Balanced Mode (15%):** NEXT_STEPS.md + relevant MASTER_TODO sections + latest session
**Comprehensive Mode (5%):** All context files + full MASTER_TODO

---

## 📊 6. QUICK REFERENCE CARD

### Git Safety
- ⚠️ **MANDATORY:** Check branch at startup — must be on `kremer-dev`
- ⚠️ **MANDATORY:** Sync with remote before work
- ❌ **FORBIDDEN:** Force push, commit to main/stage/prod, AI attribution
- ✅ **REQUIRED:** Conventional commits, push to `kremer-dev` at session end

### Session End (ALWAYS)
1. Update NEXT_STEPS.md
2. Update MASTER_TODO.md (if major)
3. Create session notes (MANDATORY)
4. Verify checklist
5. Push to `kremer-dev`

### LOTV-Specific Rules
- Always use `dotnet build Lotv.slnx` (not `.sln`)
- JotForm: HIPAA-BAA account — builder UI only for all changes
- Production DB (`10.100.1.87`): no direct AI session access
- `sa` credential rotation + `lotv_app` login still pending (needs prod DB access)

---

## 🚨 ENFORCEMENT

- ❌ Committing to `main` → STOP, switch to `kremer-dev`
- ❌ Missing session notes → CREATE before session end
- ❌ NEXT_STEPS.md over token limit → CLEAN UP immediately
- ❌ Ad-hoc work not tracked → ADD to tracking docs immediately

---

**Version:** v1.4 (Restrictive Tier)
**Created:** 2026-09-09
**Status:** ACTIVE - STRICTLY ENFORCED
