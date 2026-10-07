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

## Follow-up: lily branding + Chapter Dashboard link removed (same day)
- **Sunflower → lily:** the 🌻 brand mark (login, sidebar, public header, onboarding, donor and transparency pages — 23 spots) is now a lily-of-the-valley image: `wwwroot/images/lily.svg` (colour) and `lily-white.svg` (for the green round badges). Browser-tab/app icons (`favicon.png`, `icon-192.png`, new `icon-512.png`) were the default Blazor logo and are now the lily; `manifest.webmanifest` points at the right sizes. Artwork was drawn in-house (SVG) — swap in an official logo file at the same paths whenever one exists.
- **Emails:** every email header (`RequestEmails.Wrap`) now shows the lily as a PNG (`wwwroot/images/lily-email.png`, white lily on the blue header; clients block SVG). The URL is absolute and built from `App:WebBaseUrl` at startup (`RequestEmails.LogoUrl`, set in `Program.cs`), so the Web site must serve `/images/lily-email.png` publicly in each environment.
- **Chapter Dashboard:** removed the sidebar link (Overview now shows only HQ Dashboard). The `/admin/dashboard` page itself still exists because it is the post-login landing page and ~10 links point at it; its title/heading now reads **HQ Chapter Dashboard** (AdminLayout, Dashboard.razor, onboarding label). `SidebarLayoutTests.Overview_HasOnlyTheHqDashboard` updated.
- Build: 0 errors / 0 warnings (Release, no-incremental).

## Follow-up 2: shared case toolbar, clickable list boxes, Edit Request Form moved
- **One toolbar, four pages:** new `Shared/CaseToolbar.razor` is the single button row on Package Pipeline, Unassigned Queue, My Work Queue and Family Cases (list view): ⚠ Needs info · 📦 Export for Shippo · List View · 🔚 Unassigned Queue · 📋 My Work Queue · + New Request (page-specific Refresh stays first; the current page's button is marked `btn-current`). Export for Shippo logic moved out of `Kanban.razor` into the component; Needs info now filters Family Cases and My Work Queue too. **+ New Request** opens the panel on Family Cases and goes to `/admin/cases?new=1` from the other pages. Volunteers keep their simple My Work Queue header. The per-page "Package Pipeline" button was dropped from the Queue and Cases headers to match the requested set (still in the sidebar).
- **Family Cases boxes filter the list:** Open Cases and Overdue are now click/keyboard buttons (`kpi-click`, active ring). The Open Cases number now counts anything not Fulfilled/Cancelled so it equals what the Open filter shows (it previously left out Shipped and On Hold).
- **Edit Request Form** moved from the sidebar to a **System Admin** tab (HQAdmin only, as before). The old URL `/admin/forms/prayer-care-intake` still works. E2E `FormEditorTests` / `SidebarLayoutTests` updated.
- Answer recorded: both the public form and the staff **+ New Request** panel create a family + a New case (public form verified live; created case #52).

## Follow-up 3: "Package Pipeline" → "Package Workflow", and the site now scales with the window
- **Rename:** every visible "Package Pipeline" is now "Package Workflow" (sidebar, page title/heading, Cases Hub tab, command palette, dashboard link, other pages' links, help text, E2E test). The URL `/admin/kanban` is unchanged so bookmarks and links keep working. The manual specs were updated too; the generated manuals (uncommitted) still say "Pipeline" until `tools/manuals/run_all.py` is re-run.
- **Responsive audit:** drove 11 pages (public home, request form, login, dashboard, cases, workflow, queue, my queue, case detail, board portal, system admin) at 1920, 1440, 1280, 1024, 768 and 414 px and measured horizontal overflow. It found real problems, fixed in `lotv-admin.css` unless noted:
  - **Top bar** ran off-screen below ~1100 px (user menu, currency clipped) — it now wraps onto a second row; desktop-only ⌘K and collapse buttons hide below 900 px; user name hides on phones.
  - **Case page** kept a fixed 360 px side column and was cut off on tablets/phones — fixed-width inline grids now stack to one column ≤1100 px, and all inline multi-column grids go single-column ≤600 px.
  - **Public site menu** overflowed at tablet width — the hamburger now takes over at ≤1000 px (was 720).
  - **Packing-list add row** (CaseDetail.razor) spilled out of its column on desktop — now wraps.
  - **Wide tables** (e.g. 9-column Family Cases) crushed their text on tablets — they now keep a minimum width and scroll sideways inside their panel.
- Result: 60/66 checks clean; the other 6 are Leaflet map tiles/overlay on System Admin that are clipped inside their own map box (not page overflow).

