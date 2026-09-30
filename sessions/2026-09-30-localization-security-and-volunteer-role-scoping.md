# Session 2026-09-30 — Localization/currency fixes, family-profile IDOR, volunteer role-scoping

**Branch:** kremer-dev | **Starting point:** `770127c` (end of the 2026-09-29 UX audit session)

---

## Further-testing follow-up, and a deep-dive QA pass

- Closed out the three "needs further testing" items from the 2026-09-29 audit: mobile viewport (390×844) confirmed clean across Home, the request form, Donate, Volunteer, Events, and Transparency; a real submission run through to confirmation on a scratch DB (`WantsPackage`, `Reason`, `Status`, bracelet entry all verified correct); the Events "No Upcoming Events" empty state confirmed intentional (not a data gap) — no code change needed.
- Ran a fresh deep-dive QA pass at the user's request ("issues stickiness and other things not right or look weird like 0 dioceses"). The 0-dioceses tile turned out already fixed and hidden in production. Found two real, verified issues instead:
  1. **Language switcher only translated ~10% of the site.** Home page and nav chrome translated correctly; every other page (Give, Volunteer, Events, Transparency, and the static request form) stayed in English regardless of the selected language — including the primary "Request a Comfort Package" CTA destination.
  2. **Currency switcher was fully decorative.** A real `CurrencyService` with live conversion rates existed, but nothing on any public page actually used it — the Give page's amounts come from the Givebutter widget (unaffected by anything LOTV renders), and Transparency's dollar figures were hardcoded string formatting.
- The "stickiness" report itself didn't hold up: a mid-scroll screenshot artifact, not reproduced on a settled page; flagged as likely a scroll-animation/screenshot-timing fluke rather than a real bug.

## Fixed: full page translation + real currency conversion

- Wired `Give.razor`, `VolunteerSignup.razor`, `Events.razor`, and `Transparency.razor` through `LocalizationService`, same as `Home.razor` already was — ~90 new key/value pairs (English + Spanish) added to `LocalizationService.cs` covering role cards, form labels, event cards/RSVP modal, and transparency stats/table/CTAs.
- `Transparency.razor`'s dollar figures now go through `CurrencyService.Format()`, so switching currency actually changes what's displayed there. `Give.razor` documented (comment, not silently ignored) why its amounts can't respect the switcher — Givebutter is a third-party widget with no supported runtime-currency override.
- Not done: the static `/apply` intake form (`prayer-care-intake.html`) still has zero localization — separate, larger job (plain JS driven off an API-served schema, not Blazor), out of scope for this pass.
- Build: 0 warnings/errors. 764/764 tests pass.

## Whitney sign-in review, mapped against the ministry's Excel workbook

- Set up a scratch local environment (LocalDB + a test HQAdmin account, seeded via the app's own QA sample data) to sign in and review the sign-in process, the Cases/Case Detail/Possible-Duplicates workflow, and profile editing — user explicitly declined using real production credentials.
- Confirmed the app already replaces almost everything the "Prayer Care Package Request Database.xlsx" workbook was tracking by hand: Case Detail mirrors the "2026" sheet's columns; Possible Duplicates automates the "Prayer_Care_Package_Cleaned_v4_" sheet's manual Review/Delete dedup; Mother's/Father's Day Mailing and Bereavement Follow-Up directly replace the "MDFD Mailing 2026" and "SM (2026)" sheets; Inventory/Build Day Planner replaces "Box Totals." The "WW-In Kind" sheet (informal requests Whitney processes directly, bypassing normal intake) has no clean equivalent yet — flagged as a question for her, not something built.
- Two editing surfaces confirmed present: family profile edit (`/admin/families/{id}/edit`, contact/address only by design) and staff profile/role edit (`/admin/users`). Found one inconsistency: `/my-profile` (built for public account holders) could be reached by a signed-in staff session and showed fields irrelevant to them — investigated further below.

## Fixed: a real anonymous IDOR under `/my-profile`, not just a UI leak

- Investigating the `/my-profile` inconsistency turned up something worse than cosmetic: `PATCH /api/public/v1/families/{id}/profile` was `AllowAnonymous` with **no ownership check at all** — anyone who knew or guessed a family's numeric ID could rewrite that grieving family's name, email, phone, or address with no login. Confirmed nothing in production currently emails or displays a working `/my-profile?FamilyId=N` link to a real family, so this wasn't actively being hit — but the endpoint itself was wide open.
- Also found `MyProfile.razor` itself was broken for everyone: no `OnInitializedAsync`, always rendered blank, and `Save` reported false success (`_saved = true`) even when nothing was persisted. Volunteer/donor portal links passing `VolunteerId=`/`DonorId=` were silently dropped (params never declared).
- **Fixed:**
  - Anonymous callers to the PATCH endpoint must now supply `ConfirmEmail` matching the email on file (case-insensitive) — not real authentication, but requires already knowing the family's email, which nothing else in the public API discloses. A signed-in staff caller (the admin `FamilyEdit.razor` page reuses this same endpoint) sends its Bearer token and skips the check.
  - `/my-profile`: a signed-in staff session is redirected to `/admin/dashboard` instead of seeing the page; Donor gets the one field with a real backend (avatar); Volunteer gets an honest "not available yet, contact us"; Save only ever reports success when the API call actually succeeded.
- **Explicitly not touched:** the same `AllowAnonymous`-with-no-ownership-check pattern exists on `PATCH /recurring/{id}`, `POST /recurring/{id}/pause|resume|cancel`, and `PUT /donors/{id}/avatar` — money-adjacent, flagged for a follow-up decision rather than changed without sign-off.
- New tests: `FamilyProfileSelfServiceTests` (4). 764/764 tests pass throughout.

## Fixed: volunteers only see their own dashboard(s), scoped by actual role

- **The ask:** "volunteers such as prayer request and package request should only get access to the prayer package dashboard or the prayer dashboard." Audit (via a background research agent) found every `Volunteer`-role login landed on the identical sidebar/queue regardless of whether they were a Package Assembler or a Prayer Ambassador — the only role-aware code was `PrayerList.razor` showing an error message, not a route/nav gate. The "wrong URL" lane guard existed but always bounced back to My Work Queue regardless of the volunteer's actual role.
- **Built:**
  - `/admin/prayer-list` reframed as a real **Prayer Dashboard** (stat cards: families praying for, candidates available), also answering at `/admin/prayer-dashboard`.
  - `AdminLayout` now loads the signed-in volunteer's own record once and decides sidebar links + landing page per person: Prayer-Ambassador-only sees only the Prayer Dashboard; Package-Assembler-only sees only My Work Queue; both-role (an employee is the realistic case for this) sees both; the lane guard redirects to whichever applies.
  - **Server-side enforcement added**, not just UI: the prayer-team join/leave and `my-prayer-list`/`prayer-candidates` routes now check the caller's own `VolunteerRole`. Previously a Package-Assembler-only volunteer calling those routes directly (bypassing the UI gate) could still join a prayer team.
- **Also fixed in passing:** the magic-link self-service volunteer portal (`/volunteer/my-assignments`, `/volunteer/available`, `/volunteer/history`) was non-functional — it called API methods that only ever attached the staff login token, and two pages called the one endpoint explicitly forbidden to the Volunteer role. Added four new magic-link-scoped public endpoints (assignments, available-in-my-chapter, claim, status-update with ownership + valid-transition checks) and rewired all three pages, plus carried the volunteer ID through every nav link between them (previously lost on each click). `VolunteerDashboard.razor` — an orphaned second hub showing unfiltered data to whoever landed on it — is now a thin redirect to the real hub, `VolunteerPortal.razor`.
- **Follow-up correction same session:** "an employee could have both dashboards but usually a volunteer will only have one usually the prayer dashboard." A brand-new volunteer with no record yet was defaulting entirely to Package Assembler, with no self-service path to become a Prayer Ambassador (only staff could add it). Fixed: someone with no record sees both links (either dashboard's own "create my record" button lets them pick) but now **defaults to landing on the Prayer Dashboard**, matching the real-world skew. `POST /api/v1/volunteers/me` takes an optional `Role`, still defaulting to Package Assembler when omitted so existing callers are unaffected.
- **Not touched:** `VolunteerPending.razor` still lives under the `/volunteer/*` URL prefix but actually requires the staff login, not the magic-link session — a pre-existing mismatch between the two identity systems, out of the approved scope.
- New tests: `VolunteerRoleAndSelfServiceTests` (9 — role gate both ways, assignments/available scoping, claim conflict, status ownership + invalid-transition rejection, default-role creation). 773/773 tests pass.

## Open Items

- [ ] PR kremer-dev → stage → main — none of this session's work (nor the 2026-09-28/29 work before it) is deployed to production yet.
- [ ] Decide on the sibling `AllowAnonymous`-with-no-ownership-check endpoints (`/recurring/{id}` PATCH, pause/resume/cancel, donor avatar PUT) — same pattern as the family-profile fix, money-adjacent, needs a go/no-go before changing.
- [ ] `VolunteerPending.razor` mixes the staff-JWT and magic-link identity systems under the same `/volunteer/*` URL prefix — worth a real fix or a documented decision.
- [ ] Static `/apply` intake form (`prayer-care-intake.html`) still has zero localization.
- [ ] The "WW-In Kind" informal-request workflow (Whitney's Excel sheet) has no clean equivalent in the app — needs a decision, not code, from ministry staff.

---

**Session End** | **Branch:** kremer-dev ✅ Safe
