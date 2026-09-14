# Claude Code Directives

**Welcome!** This file directs you to the correct startup directives for this project.

---

## 🚀 Start Here

### For Claude Code Sessions:

1. **Read the startup directive first:**
   - **Location:** `.claude/SESSION_STARTUP_DIRECTIVE.md`
   - **Purpose:** Startup protocol, mode selection, and session workflow

2. **Choose your startup mode:**
   - **Quick Mode** (most sessions): Read `NEXT_STEPS.md` ONLY
   - **Balanced Mode** (phase transitions): Read condensed directives
   - **Comprehensive Mode** (major planning): Read all directives

3. **Follow the protocol:**
   - The SESSION_STARTUP_DIRECTIVE will guide you through the rest
   - DO NOT read files randomly - follow the directive's instructions
   - Each mode specifies exactly which files to read

---

## 🔄 Directive Updates Check (MANDATORY)

**⚠️ CRITICAL:** Before starting any work, check if directives were updated from remote.

**When This Applies:**
- After running `git pull`, `git fetch`, or syncing with remote
- After switching branches that may have different directive versions
- When you see git status showing `.claude/` files were updated

**Required Action:**
If `.claude/` directives were updated from remote sync:
1. ⚠️ **STOP** - Do NOT proceed with cached/old directive understanding
2. 🔄 **REINITIALIZE** - Re-read all relevant directives from scratch
3. ✅ **VERIFY** - Confirm you're using the latest directive version
4. 🚀 **PROCEED** - Now safe to start work with current directives

**How to Check:**
```bash
git status
# or
git diff HEAD@{1} .claude/
```

If `.claude/` files show as modified after sync, you MUST reinitialize before working.

---

## 🚨 CRITICAL RULES - READ FIRST

**⚠️ MANDATORY:** All critical and mandatory rules consolidated in ONE place.

**Location:** `.claude/CRITICAL_RULES_CONSOLIDATED.md`

**This document contains ALL mandatory rules for:**
- ✅ Git safety (branch checks, protected branches, commit format)
- ✅ Session documentation (mandatory session notes, ad-hoc work tracking)
- ✅ File organization (root files, documentation structure, planning docs)
- ✅ Session end protocol (checklist, push requirements)
- ✅ Planning document purposes (MASTER_TODO vs NEXT_STEPS)

**🔴 If you only read ONE directive file, read this one.**

**Quick Access:** `.claude/CRITICAL_RULES_CONSOLIDATED.md`

### Inline Critical Rules Summary

**Branch Safety (MANDATORY at startup):**
```bash
git branch --show-current
```
Protected branches (NO commits): main, master, dev, *beta*, *stage*, *prod*, *deploy*

**Session Notes (MANDATORY after):**
- ✅ Bug fixes (any severity)
- ✅ New features or enhancements
- ✅ Updates to existing functionality
- ✅ Refactoring or optimization
- ✅ Configuration/settings changes
- ✅ Directive/template changes

**Ad-Hoc Work Tracking (MANDATORY):**
- ALL unplanned work MUST be tracked in NEXT_STEPS.md and/or MASTER_TODO.md
- Document in session notes: What? Why? What changed?
- Update planning docs to reflect new state

**Planning Documents:**
- **MASTER_TODO.md** - Comprehensive task tracking (all TODO items)
- **NEXT_STEPS.md** - Minimal info (5-7 tasks, ~400 tokens)

**File Organization:**
- Root files: ONLY allowed list (README, MASTER_TODO, NEXT_STEPS, configs)
- Session notes: `sessions/YYYY-MM-DD-brief-title.md`
- ❌ NO `.claude/settings.local.json` in git

**Session End (ALWAYS):**
1. Update MASTER_TODO.md (if major)
2. Update NEXT_STEPS.md (if significant)
3. Create session notes (MANDATORY)
4. Verify checklist
5. Push to kremer-dev branch

For complete details, see `.claude/CRITICAL_RULES_CONSOLIDATED.md`

---

## 📂 Available Directives

All directives are located in the `.claude/` directory:

- **CRITICAL_RULES_CONSOLIDATED.md** - ALL mandatory rules in one place (read this first!)
- **SESSION_STARTUP_DIRECTIVE.md** - Startup protocol and modes
- **FILE_ORGANIZATION_DIRECTIVE.md** - Where to put files
- **git-workflow-directive.md** - Git safety rules and commit standards
- **SESSION_SUMMARY_TEMPLATE.md** - End-of-session documentation template

---

## ⚡ Quick Mode (Recommended for Most Sessions)

If you're starting a new session and just need to continue work:

1. Read `.claude/SESSION_STARTUP_DIRECTIVE.md`
2. Follow Quick Mode instructions (Step 3):
   - Read `NEXT_STEPS.md` ONLY
   - Do NOT read MASTER_TODO.md or other planning files
   - Load additional files ONLY if needed for your specific task

**Quick Mode saves ~93% of startup tokens** (400 tokens vs 5,500 tokens)

---

## 🎯 Important Rules

- ✅ **Always** start by reading `.claude/SESSION_STARTUP_DIRECTIVE.md`
- ✅ **Follow** the mode-specific file loading instructions
- ❌ **Never** read files randomly without checking the directive first
- ❌ **Never** skip the startup protocol (leads to context errors)

---

## LOTV Project Notes

- **Branch strategy:** feature work on `kremer-dev`; merge to `main` via PR
- **Solution file:** always use `Lotv.slnx` (not `.sln`)
- **Production DB:** SQL Server at `10.100.1.87` — no direct AI session access
- **JotForm writes:** HIPAA-BAA account — all form changes via builder UI only

---

**Version:** v1.4 (Restrictive Tier - Enhanced with Critical Rules)
**Adapted for:** LOTV — Lily of the Valley SaaS Platform
**Last Updated:** 2026-09-09
