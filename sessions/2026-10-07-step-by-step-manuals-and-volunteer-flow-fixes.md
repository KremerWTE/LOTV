# Session Summary — step-by-step manuals rebuilt from the live app; three volunteer/board bugs found and fixed

**Date:** 2026-10-07 | **Branch:** kremer-dev | **Status:** ✅ Complete. 796/796 tests pass. App fixes, tests, tooling and notes are committed; the generated manuals (`docs/manuals/`) are deliberately **not** committed (per instruction) and exist only in the working tree.

## Request
Take the eight manuals and break every process into click-by-click steps with many more screenshots, so someone could complete the task using only the manual.

## What was built
- **Generator, not hand-edited docs:** `tools/manuals/` drives the running app with Playwright. Each step boxes the target (numbered red box), takes the screenshot, performs the action, and writes the matching Markdown — text and pictures cannot drift apart. See `tools/manuals/README.md`.
- **Output:** editable Markdown in `docs/manuals/src/`, 261 screenshots in `docs/manuals/images/<manual>/` (was 15 total), and rebuilt `.docx` + `.pdf` for all eight manuals (same filenames, so they replace the old ones).
- Every manual now has: numbered Processes with Goal / Who / Before you start / Time, one Step per click with a picture, "You should see", "If it doesn't look like this", and a "Check your work" checklist.
- Data: a throwaway SQLite DB outside the repo (`Database:Provider` forced off SqlServer) + the app's own QA Sample Data + 5 demo accounts. Nothing touched the repo's tracked `lotv-dev.db` or production.

## Bugs found by following the manuals literally — and fixed
1. **"Create my volunteer record" did nothing for a Volunteer-role user** (403, silent). `POST /api/v1/volunteers/me` sat inside the staff-only `/volunteers` group; the GET beside it was already mapped with the Volunteer policy. Existing tests registered the user as ChapterStaff, hiding it. Fixed in `Program.cs`; new test `CreateMyVolunteer_WorksForAPlainVolunteerRoleAccount`.
2. **A volunteer's tracking number / shipped date were silently never saved**, so "Shipped" was then refused ("tracking number required"). `PATCH /api/v1/requests/{id}` required "Staff", though `AccessRules` already lists it as a call a volunteer may make on their own case. Now "CaseWork" (the group filter still limits volunteers to cases assigned to them); a volunteer's `InternalNotes` is ignored. `MyQueue.razor` now shows an error instead of ignoring a failed save. New test covers own case / other's case / notes. (User approved this authorization change.)
3. **Board Portal "Sign Out" did not sign out.** `Auth.Logout()` is fire-and-forget and the forced reload beat it, so the saved token signed the user straight back in. `BoardPortal.razor` now awaits `LogoutAsync()`.

## Where the old manuals were wrong (corrected)
- Prayer Dashboard list is headed "Families You Could Pray For" and the button is "I'll pray for this family".
- User Management is a tab inside **System Admin**, not a sidebar link; Login As confirm button is "Sign in as them".
- The Pipeline has **9** columns (New, Assigned, Volunteer Accepted, Packing, Notes, Shipping, On Hold, Fulfilled, Cancelled), not 6.
- Case status chain: from InProgress you can go to AwaitingShipment *or* Fulfilled; Shipped only via AwaitingShipment (full table now in guides 01 and 05).
- Finished (Fulfilled) cases leave a volunteer's My Work Queue. Packing items are added by staff (volunteers have no inventory access by design — `/api/v1/inventory` is asserted Forbidden for volunteers); volunteers tick items off.
- Public form: choosing a package makes the bracelet child row **required**.

## Findings left for a decision (not changed)
- A **Board** user who types `/admin/users` can *view* the staff list (the old Board guide said it was unreachable). Edits are still refused. Worth deciding whether the page should hide itself from Board.
- Volunteers see **Unassign / Put On Hold / Cancel Case** buttons in Quick Actions on their own case page (guide 03 tells them not to use them); server-side effect not verified.
- Headless Chrome could not paint the mid-drag column highlight on the Pipeline, so guide 07 describes it (confirmed in `lotv-admin.css`) rather than showing it.

## Files
`tools/manuals/*` (lib, build, run_all, seed, reset_env, fast_reset, specs/m01–m08, README), `docs/manuals/src/*.md`, `docs/manuals/images/**`, `docs/manuals/0*.docx|pdf`, `src/Lotv.Api/Program.cs`, `src/Lotv.Web/Pages/Admin/MyQueue.razor`, `src/Lotv.Web/Pages/BoardPortal.razor`, `tests/Lotv.Tests/Integration/{AccessRuleTests,VolunteerRoleAndSelfServiceTests}.cs`.

## Open
- [ ] Manuals (`docs/manuals/src`, `images`, `.docx`, `.pdf`) intentionally left uncommitted on kremer-dev. Regenerate any time with `tools/manuals/run_all.py`.
- [ ] The three app fixes only reach production on the next deploy (PR kremer-dev → stage → main).
- [ ] Re-run `python tools/manuals/run_all.py` after any UI change.
