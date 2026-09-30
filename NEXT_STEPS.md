# Next Steps — LOTV

**Updated:** 2026-09-30 | **Branch:** kremer-dev | **Phase:** Phase 6 — Deployment & Launch (QA readiness)

---

## 🎯 Current Focus: get everything since `770127c` deployed; two questions waiting on ministry/client answers

**Status:** Code complete and pushed to `kremer-dev` through `636cc4f`. 779 unit/integration tests pass, 0 build warnings. Nothing from 2026-09-28 onward has reached production yet.

**Next Tasks (Priority Order):**
1. **PR kremer-dev → stage → main** — the single biggest blocker; production is several sessions behind
2. **Send these two questions** (drafted this session, need your answers before any related build):

   **WW-In Kind workflow** — for Whitney/ministry staff:
   > The Excel workbook has a "WW-In Kind" sheet tracking requests you process directly and hand-deliver (bracelet + items), separate from the normal online request form. A few questions so we know whether this needs its own place in the app: (1) How does a WW-In Kind request usually reach you — phone, in person, email? (2) Does it skip the normal intake form on purpose, or is that just how it's happened so far? (3) Does it need to show up anywhere in the staff dashboard (so other staff can see it), or is it fine staying off-system? (4) Roughly how often does this happen — a few times a year, monthly, weekly?

   **Group training** — for the client:
   > "Group training" has been on the list without a definition for a while. To scope it: (1) Training *of* whom — new volunteers, staff, chapter leads? (2) What kind — a live session/webinar, a certification volunteers complete, or something else? (3) Does it need to be scheduled/tracked in the app (e.g., "completed onboarding training: yes/no" on a volunteer's record), or is it handled entirely outside the app and this is just a note-taking need? (4) Is there an existing process today (even an informal one) we should match, or is this net-new?

3. Static `/apply` intake form's actual question text (title, field labels, options) is still staff-authored-English-only — the form's own chrome now translates, but making the questions themselves editable in Spanish would need a real content feature (a way for staff to author and maintain a Spanish version)
4. Domain/DNS/SSL, uptime monitoring, automated DB backups — all still require actual cloud/infra setup no AI session has access to

**Completed This Session (2026-09-30):**
- ✅ Full page translation (Give/Volunteer/Events/Transparency) + real currency conversion on Transparency
- ✅ Whitney sign-in review, mapped against the ministry's Excel workbook — app already replaces nearly all of it
- ✅ Security: closed anonymous IDORs on both the family-profile AND donor-recurring/avatar self-service endpoints
- ✅ Volunteer dashboards scoped by actual VolunteerRole (Prayer Dashboard vs. My Work Queue), server-side role check, magic-link portal wired up, new-volunteer default fixed
- ✅ Fixed VolunteerPending.razor's nav links (pointed at the wrong identity system)
- ✅ Localized the static intake form's own chrome (loading/error/bracelet/validation)

---

## 🔗 Quick Links

- Full TODO: `MASTER_TODO.md` | Last session: `sessions/2026-09-30-localization-security-and-volunteer-role-scoping.md`
- Directives: `.claude/CRITICAL_RULES_CONSOLIDATED.md`, `.claude/SESSION_STARTUP_DIRECTIVE.md`
- PM plan / risks: `docs/LOTV-PM-Plan.md` (R-32 to R-39)

---

## ⚠️ Critical Rules

**Protected Branches:** NO commits to main, master, dev, *stage*, *prod*, *deploy* — flow is kremer-dev → stage → main via PR
**Current Branch:** kremer-dev (✅ Safe) | **Commits:** `type(scope): description`, no AI co-author
**JotForm:** HIPAA-BAA account — builder UI only | **Prod DB:** `10.100.1.87` — no direct AI access
**Secrets:** never commit passwords/keys; starting passwords go through GitHub secrets
