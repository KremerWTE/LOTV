# Next Steps — LOTV

**Updated:** 2026-09-30 | **Branch:** kremer-dev | **Phase:** Phase 6 — Deployment & Launch (QA readiness)

---

## 🎯 Current Focus: get everything since `770127c` deployed, then decide on the money-adjacent security follow-up

**Status:** Code complete and pushed to `kremer-dev` through `38bf752`. 773 unit/integration tests pass, 0 build warnings. Nothing from 2026-09-28 onward (Build Day planner, two volunteer roles, prayer-only requests, the UX audit fixes, full-site localization/currency, the family-profile IDOR fix, volunteer role-scoped dashboards) has reached production yet.

**Next Tasks (Priority Order):**
1. **PR kremer-dev → stage → main** — the single biggest blocker; production is several sessions behind
2. **Decide on the sibling anonymous-no-ownership-check endpoints** (`/recurring/{id}` PATCH, pause/resume/cancel, donor avatar PUT) — same pattern just closed on the family-profile endpoint, but these are money-adjacent; needs a go/no-go before touching auth on them
3. **`VolunteerPending.razor`** mixes the staff-JWT and magic-link identity systems under one `/volunteer/*` URL prefix — decide whether to fix or document as-is
4. **"WW-In Kind" informal-request workflow** (from the ministry's Excel workbook) has no equivalent in the app — needs a decision from ministry staff, not code
5. Static `/apply` intake form (`prayer-care-intake.html`) still has zero localization — separate, larger job (plain JS off an API-served schema)
6. Group training — requirement still undefined (sessions? co-assigned volunteers? certifications?)

**Completed This Session (2026-09-30):**
- ✅ Full page translation (Give/Volunteer/Events/Transparency) + real currency conversion on Transparency
- ✅ Whitney sign-in review, mapped against the ministry's Excel workbook — app already replaces nearly all of it
- ✅ Security: closed an anonymous IDOR on the family-profile self-service endpoint; fixed `/my-profile`'s blank-form/false-success bugs
- ✅ Volunteer dashboards scoped by actual VolunteerRole (Prayer Dashboard vs. My Work Queue), server-side role check added, magic-link portal pages wired up, new-volunteer default fixed to Prayer Dashboard

---

## 🔗 Quick Links

- Full TODO: `MASTER_TODO.md` | Last session: `sessions/2026-09-30-localization-security-and-volunteer-role-scoping.md`
- Directives: `.claude/CRITICAL_RULES_CONSOLIDATED.md`, `.claude/SESSION_STARTUP_DIRECTIVE.md`
- PM plan / risks: `docs/LOTV-PM-Plan.md` (R-32 to R-37)

---

## ⚠️ Critical Rules

**Protected Branches:** NO commits to main, master, dev, *stage*, *prod*, *deploy* — flow is kremer-dev → stage → main via PR
**Current Branch:** kremer-dev (✅ Safe) | **Commits:** `type(scope): description`, no AI co-author
**JotForm:** HIPAA-BAA account — builder UI only | **Prod DB:** `10.100.1.87` — no direct AI access
**Secrets:** never commit passwords/keys; starting passwords go through GitHub secrets
