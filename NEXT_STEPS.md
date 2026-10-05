# Next Steps — LOTV

**Updated:** 2026-10-05 | **Branch:** kremer-dev | **Phase:** Phase 6 — Deployment & Launch (QA readiness)

---

## 🎯 Current Focus: get a yes/no on the sidebar scope, fix production's WebSocket connectivity, get code changes deployed, two questions waiting on ministry/client answers

**Status:** Code complete and pushed to `kremer-dev` through `8eb07d4`. 787 unit/integration tests pass, 0 build warnings. Nothing from 2026-09-28 onward has reached production yet.

**Next Tasks (Priority Order):**
1. **Confirm the sidebar scope** — restricting the sidebar to Prayer Request Package + System Admin for everyone except `chris.kremer` also hides Chapter/HQ Dashboard, Donations, Volunteers, and Reports entirely from every other staff member. Flagged twice with no answer yet — worth a direct yes/no before another staff member is surprised by missing nav items.
2. **Fix production's WebSocket/sticky-sessions config (R-40)** — found live during a QA review: `https://lotv.wte.net/` took anywhere from ~3 seconds to over 3 minutes to show any content on repeated fresh loads, every time. Browser console explicitly says why: `Failed to start the transport 'WebSockets' ... If you have multiple servers check that sticky sessions are enabled`. This affects the ENTIRE site (public + staff portal) on every fresh page load, right now, independent of any deploy — likely explains some of the "flakiness" chalked up to browser-automation tooling in earlier QA sessions. Needs whoever has `wte_apps3` IIS access to check: (1) LOTV_WEB app pool isn't a multi-worker-process "web garden", (2) if there's a load balancer/ARR in front, enable sticky sessions, (3) WebSocket Protocol Windows feature is enabled. Full detail in `docs/LOTV-PM-Plan.md` R-40.
3. **PR kremer-dev → stage → main** — production is several sessions behind on code (separate from #2, which is a hosting config issue, not code)
4. **Confirm the shipping-label carrier platform** before phase two — a "Shippo" lead from Chris is unverified, not a decision. Phase one (trigger + placeholder label, no carrier) is already built and merged.
5. **Decide the volunteer landing-page inconsistency** — a brand-new volunteer lands on My Work Queue after a direct login, but on the Prayer Dashboard if signed in via Login As (or redirected there by the lane-enforcement guard), unless they're a Package Assembler. Found while building the staff manuals; documented there, not yet fixed either way.
6. **Send these two questions** (drafted an earlier session, need your answers before any related build):

   **WW-In Kind workflow** — for Whitney/ministry staff:
   > The Excel workbook has a "WW-In Kind" sheet tracking requests you process directly and hand-deliver (bracelet + items), separate from the normal online request form. A few questions so we know whether this needs its own place in the app: (1) How does a WW-In Kind request usually reach you — phone, in person, email? (2) Does it skip the normal intake form on purpose, or is that just how it's happened so far? (3) Does it need to show up anywhere in the staff dashboard (so other staff can see it), or is it fine staying off-system? (4) Roughly how often does this happen — a few times a year, monthly, weekly?

   **Group training** — for the client:
   > "Group training" has been on the list without a definition for a while. To scope it: (1) Training *of* whom — new volunteers, staff, chapter leads? (2) What kind — a live session/webinar, a certification volunteers complete, or something else? (3) Does it need to be scheduled/tracked in the app (e.g., "completed onboarding training: yes/no" on a volunteer's record), or is it handled entirely outside the app and this is just a note-taking need? (4) Is there an existing process today (even an informal one) we should match, or is this net-new?

7. Static `/apply` intake form's actual question text (title, field labels, options) is still staff-authored-English-only — the form's own chrome now translates, but making the questions themselves editable in Spanish would need a real content feature (a way for staff to author and maintain a Spanish version)
8. Domain/DNS/SSL, uptime monitoring, automated DB backups — all still require actual cloud/infra setup no AI session has access to
9. Cosmetic polish from an earlier QA review: ~11 admin list pages flash empty content before data loads (no `_loading` guard); an unused `NavMenu.razor` scaffold leftover could be deleted

**Completed This Session (2026-10-01 to 2026-10-05):**
- ✅ Fixed 3 functional + 3 cosmetic PCP intake-form bugs; root-caused and fixed the missing-bracelet regression (a fail-closed conditional-field bug), with a new regression test
- ✅ Sidebar restricted to Prayer Request Package + System Admin for everyone except `chris.kremer` (scope still unconfirmed — see #1 above); built a real temp-password capability after refusing two requests to handle a colleague's actual password directly
- ✅ Added a "Package only" intake option that opts a case out of the passive Prayer Ambassador queue
- ✅ Widened "Login As" to cover other HQAdmins and deactivated accounts; built and then correctly reverted a riskier ChapterAdmin extension after flagging the privilege-escalation concern
- ✅ Shipping-label generation, phase one (trigger + placeholder label; real carrier integration still pending platform confirmation)
- ✅ Built and live-tested 8 step-by-step staff/volunteer/admin/board manuals (`docs/manuals/`, docx + pdf) — found one real app inconsistency along the way (volunteer landing page differs between direct login and Login As)
- ✅ 787/787 tests passing throughout

**Completed Prior Session (2026-09-30):**
- ✅ Full page translation (Give/Volunteer/Events/Transparency) + real currency conversion on Transparency
- ✅ Whitney sign-in review, mapped against the ministry's Excel workbook — app already replaces nearly all of it
- ✅ Security: closed anonymous IDORs on both the family-profile AND donor-recurring/avatar self-service endpoints
- ✅ Volunteer dashboards scoped by actual VolunteerRole (Prayer Dashboard vs. My Work Queue), server-side role check, magic-link portal wired up, new-volunteer default fixed
- ✅ Fixed VolunteerPending.razor's nav links (pointed at the wrong identity system)
- ✅ Localized the static intake form's own chrome (loading/error/bracelet/validation)
- ✅ Full QA review: found + fixed 9 broken nav links and a silent avatar-upload failure; found (can't fix — needs IIS access) production's WebSocket/sticky-sessions issue, now R-40

---

## 🔗 Quick Links

- Full TODO: `MASTER_TODO.md` | Last session: `sessions/2026-10-05-pcp-form-fixes-sidebar-scoping-temp-password-login-as-shipping-labels-and-manuals.md`
- Directives: `.claude/CRITICAL_RULES_CONSOLIDATED.md`, `.claude/SESSION_STARTUP_DIRECTIVE.md`
- PM plan / risks: `docs/LOTV-PM-Plan.md` (R-32 to R-39)

---

## ⚠️ Critical Rules

**Protected Branches:** NO commits to main, master, dev, *stage*, *prod*, *deploy* — flow is kremer-dev → stage → main via PR
**Current Branch:** kremer-dev (✅ Safe) | **Commits:** `type(scope): description`, no AI co-author
**JotForm:** HIPAA-BAA account — builder UI only | **Prod DB:** `10.100.1.87` — no direct AI access
**Secrets:** never commit passwords/keys; starting passwords go through GitHub secrets
