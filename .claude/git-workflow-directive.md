# Git Workflow Directive — LOTV (Restrictive Tier v1.4)

**Purpose:** Enforce safe git operations and prevent destructive actions
**Version:** v1.4 (Restrictive Tier)
**Enforcement:** STRICTLY ENFORCED

---

## 🛡️ Protected Branches — NO COMMITS

**NEVER commit directly to these branches without EXPLICIT user confirmation:**

### Exact Match (Case-Insensitive)
- `main`, `master`, `dev`, `develop`

### Contains Pattern (Case-Insensitive)
- Anything containing: `beta`, `stage`, `staging`, `production`, `prod`, `deploy`, `release`

---

## ✅ Safe Branches — OK to Commit

- `kremer-dev` — **primary LOTV working branch**
- `feature/*`, `fix/*`, `bugfix/*`, `hotfix/*`

---

## ⚠️ Branch Safety Check (MANDATORY at Startup)

```bash
git branch --show-current
```

**If on `main` (most likely scenario):**
```bash
git checkout kremer-dev
```

**If `kremer-dev` doesn't exist locally:**
```bash
git checkout -b kremer-dev origin/kremer-dev
```

---

## 📋 Commit Standards (MANDATORY)

### Conventional Commit Format

**Required:** `type(scope): description`

**Types:**
- `feat` — New feature
- `fix` — Bug fix
- `docs` — Documentation only
- `style` — Formatting
- `refactor` — Code restructuring
- `perf` — Performance improvement
- `test` — Adding/updating tests
- `chore` — Maintenance tasks
- `ci` — CI/CD changes

**Examples:**
- ✅ `feat(webhook): create follow-up tracker on JotForm intake`
- ✅ `fix(api): correct knownLabels for Quarterly spelling`
- ✅ `docs(sessions): add 2026-09-09 onboarding session notes`
- ❌ `update code` (no type, too vague)
- ❌ `fix` (no description)

**Requirements:**
- Minimum 10 characters
- Describe WHY, not just WHAT
- NO "Co-Authored-By: Claude" — ABSOLUTELY FORBIDDEN

---

## 🚨 Prohibited Operations

**NEVER do these without EXPLICIT user confirmation:**

- ❌ Force push (`git push --force` or `git push -f`)
- ❌ Commit to protected branches
- ❌ Delete remote branches
- ❌ Amend pushed commits
- ❌ Add AI co-author attribution
- ❌ Commit secrets/credentials (connection strings, API keys, JWTs)

### LOTV Security Note

Files like `appsettings.Development.json` may contain sensitive local dev config.
`dotnet user-secrets` is used for `GiveButter:ApiKey`, `WebhookSecret`, and similar.
Do NOT commit any secrets — use `dotnet user-secrets` or environment variables.

---

## ✅ Required Operations

### 1. Sync Before Starting Work
```bash
git fetch origin
git pull origin kremer-dev
```

### 2. Check Branch Safety at Startup
```bash
git branch --show-current
# Must be: kremer-dev (or feature/fix branch)
```

### 3. Use Conventional Commits
```bash
git commit -m "type(scope): description"
```

### 4. Push All Commits at Session End
```bash
git push origin kremer-dev
```

### 5. Verify Git Status
```bash
git status
```

---

## 🔄 LOTV-Specific Branch Strategy

- **Feature work** → `kremer-dev`
- **Merge to `main`** → via PR after review (`wtesolutions/LOTV` repo)
- **Two remotes**: `origin` (personal fork `KremerWTE/LOTV`) and `wtesolutions` (org repo)
- Push both: `git push origin kremer-dev && git push wtesolutions kremer-dev`

---

## 📊 Quick Reference

### Protected Patterns
- **Exact:** main, master, dev, develop
- **Contains:** beta, stage, staging, production, prod, deploy, release

### Safe Patterns (LOTV)
- **Primary:** `kremer-dev`
- **Others:** feature/*, fix/*, bugfix/*, hotfix/*

### Prohibited (NEVER without confirmation)
- ❌ Force push
- ❌ Commit to protected branches
- ❌ Delete remote branches
- ❌ Amend pushed commits
- ❌ AI co-author attribution
- ❌ Commit secrets

### Commit Format
```
type(scope): description

Types: feat, fix, docs, style, refactor, perf, test, chore, ci
Minimum: 10 characters
Focus: WHY, not just WHAT
```

---

**Tier:** Restrictive (Maximum Safety) | **Version:** v1.4
**Adapted for:** LOTV — Lily of the Valley SaaS Platform
**Last Updated:** 2026-09-09
