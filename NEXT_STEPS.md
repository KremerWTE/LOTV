# Next Steps — LOTV

**Updated:** 2026-10-05 | **Branch:** kremer-dev | **Phase:** Phase 6 — Deployment & Launch (QA readiness)

---

## 🎯 Current Focus: IIS WebSocket fix, deploy, carrier name, two questions waiting on ministry/client answers

**Status:** Code complete and pushed to `kremer-dev` through `8eb07d4`. 787 unit/integration tests pass, 0 build warnings. Nothing from 2026-09-28 onward has reached production yet.

**Next Tasks (Priority Order):**
1. **Enable WebSocket Protocol on `wte_apps3` + check for ARR/load-balancer affinity (R-40)** — the deploy workflow now pins the LOTV_WEB pool to one worker process and warns if the feature is off, but only someone with IIS access can enable it. Takes effect on the next deploy.
2. **PR kremer-dev → stage → main** — production is several sessions behind on code
3. **Shippo direct push — built, waiting on credentials.** Reaching Packing now sends the order to the ministry's Shippo account via API (orders only; staff buy labels in Shippo). Inert until GitHub secrets are set: `SHIPPO_API_TOKEN` (start with a `shippo_test_` token) + `SHIPPO_FROM_STREET1/CITY/STATE/ZIP` (+ optional `SHIPPO_FROM_NAME/COMPANY/STREET2/PHONE/EMAIL`, `SHIPPO_DEFAULT_WEIGHT_LB`). Failures never block a case; case page shows status + "Send to Shippo" retry. CSV export stays as fallback. Verify once against a real Shippo test account; also confirm the ministry's HIPAA/BAA coverage for Shippo.
4. ~~Sidebar scope~~ confirmed. ~~Volunteer landing~~ done: everyone lands on My Work Queue.
5. (see 6 below)
6. **Send these two questions** (drafted an earlier session, need your answers before any related build):

   **WW-In Kind workflow** — for Whitney/ministry staff:
   > The Excel workbook has a "WW-In Kind" sheet tracking requests you process directly and hand-deliver (bracelet + items), separate from the normal online request form. A few questions so we know whether this needs its own place in the app: (1) How does a WW-In Kind request usually reach you — phone, in person, email? (2) Does it skip the normal intake form on purpose, or is that just how it's happened so far? (3) Does it need to show up anywhere in the staff dashboard (so other staff can see it), or is it fine staying off-system? (4) Roughly how often does this happen — a few times a year, monthly, weekly?

   **Group training** — for the client:
   > "Group training" has been on the list without a definition for a while. To scope it: (1) Training *of* whom — new volunteers, staff, chapter leads? (2) What kind — a live session/webinar, a certification volunteers complete, or something else? (3) Does it need to be scheduled/tracked in the app (e.g., "completed onboarding training: yes/no" on a volunteer's record), or is it handled entirely outside the app and this is just a note-taking need? (4) Is there an existing process today (even an informal one) we should match, or is this net-new?

7. Static `/apply` intake form's actual question text (title, field labels, options) is still staff-authored-English-only — the form's own chrome now translates, but making the questions themselves editable in Spanish would need a real content feature (a way for staff to author and maintain a Spanish version)
8. Domain/DNS/SSL, uptime monitoring, automated DB backups — all still require actual cloud/infra setup no AI session has access to
9. Cosmetic polish from an earlier QA review: ~11 admin list pages flash empty content before data loads (no `_loading` guard)

**Completed 2026-10-05 (later):**
- ✅ Sidebar scope confirmed; volunteers all land on My Work Queue; deploy guard for R-40; NavMenu scaffold deleted (`sessions/2026-10-05-volunteer-landing-and-websocket-deploy-guard.md`)

**Completed Earlier (2026-10-01 to 2026-10-05):**
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
