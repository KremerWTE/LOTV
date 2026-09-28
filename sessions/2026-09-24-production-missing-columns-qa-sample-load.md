# Session Summary — Production QA sample load fails: missing database columns

**Date:** 2026-09-24
**Branch:** kremer-dev
**Phase:** Phase 6 — Deployment & Launch (QA readiness)
**Session Type:** Bugfix

---

## Session Objectives

Get the QA sample data into production so the team can QA the site. The Load button on System Admin → QA Sample Data did nothing.

## Key Accomplishments

### Root cause found ✅
- Production ran an older build (page text said "12 families / 3 volunteers"), which swallowed the failure. After the diagnostics build was deployed the page reported: `The load failed (SqlException): Invalid column name 'Level'.`
- `Volunteers.Level` and `Requests.ProcessStage` were added by migration `20260910133545_AddVolunteerLevel`. SQL Server startup uses `EnsureCreated`, which never alters an existing table, so a database created before that date never got the columns. Any query touching volunteers (not just the sample load) fails the same way; startup steps such as `VolunteerWorkload.RecomputeAllAsync` were failing silently (caught and logged).

### Fix ✅
- New `src/Lotv.Api/Data/MissingColumnBootstrap.cs`: on SQL Server, compares the EF model to `INFORMATION_SCHEMA.COLUMNS` and adds any missing column (nullable, or NOT NULL with a default). Additive only; skips missing tables, primary keys and computed columns; idempotent; logs a warning per column added.
- Registered in `Program.cs` right after `EnsureCreated`, wrapped in try/catch so it can never take the API down.
- Chosen over one-off bootstraps because other columns may be missing too and per-column deploys would each cost a round trip.

## Verification

- Temporary LocalDB test (deleted afterwards): created the current schema, dropped `Volunteers.Level` and `Requests.ProcessStage`, ran the bootstrap (added both, second run added nothing), then loaded and removed the 39-family sample data successfully.
- End-to-end on the real API (Development host, SQL Server LocalDB, `Volunteers.Level` and `Requests.ProcessStage` dropped first): startup logged `Added missing column Requests.ProcessStage` / `Volunteers.Level`; `POST /api/v1/qa-sample-data` returned 200 "Loaded 39 sample families, 39 requests and 5 volunteers"; `GET /volunteers` 200; `DELETE` removed everything. With no chapter in the database the endpoint returns a clear 409 "There is no chapter to attach sample data to." (production must have one).
- Merged `wtesolutions/main` (7 PR merge commits, #61–#67, no file changes) into kremer-dev.
- Bug found and fixed while testing: the store identifier must use the model's own schema (null), not `"dbo"`, or every column lookup returns null.
- `dotnet build Lotv.slnx`: 0 warnings, 0 errors. 615/615 unit + integration tests pass.
- Not covered by a permanent test: the repo has no SQL Server test infrastructure (CI has no LocalDB).

## Confirm Assignment failing (same root cause) ✅

- Reported: "Confirm Assignment" fails. `PUT /requests/{id}/assign` loads `PackageRequest` and `Volunteer`, which select `ProcessStage` and `Level`, so on a database missing them it returned a bare 500 and the UI said only "Assignment failed. Please try again." The column bootstrap above fixes the cause.
- Made the reason visible: the assign endpoint now catches a save failure and returns `{ error }` with the real message (staff-only screen); `ApiService.LastAssignError` carries it; Queue, Kanban, CaseAssign and CaseDetail show it.
- Verified on the real API + LocalDB: healthy schema 200; columns stripped while running -> 500 "The assignment could not be saved (SqlException): Invalid column name 'ProcessStage'."; after restart the bootstrap added both columns and assign returned 200 (stage Assigned, status InProgress).
- Found, not changed: `GET /dashboard/stats` open-case count (`Program.cs` ~line 1762) has an operator-precedence bug (`A && New || InProgress`), so the chapter filter applies only to New; it also counts only New + InProgress. The sidebar "Cases" badge reads it once at layout load.

## Case page fixes (same day) ✅

- **Sidebar Cases badge** now counts what the Cases page calls Open (New, In Progress, Awaiting Shipment), excludes requests held for duplicate review, and applies the chapter filter to all of them (was `chapter && New || InProgress`). It refreshes on every navigation instead of once at load. Test: `SidebarOpenCasesCount_MatchesTheCasesPage_...` (616 tests pass).
- **Unassigned Queue badge:** the sidebar link now shows a count (`unassignedQueue` on `GET /dashboard/stats`), using the same filter and chapter rule as the queue page (new, unassigned, not held for duplicate review); the badge test asserts it equals the queue list. Refreshes with the Cases badge.
- **Case details Save Changes** ignored the result of assign / priority / due date / tracking saves and could report success (or fail silently) when the volunteer change failed. Each step is now checked; the first failure stops the save, names the step (with the server's reason for assign) and keeps the edits on screen.
- **Request-form answers on the case page:** the Family panel now shows the prayer-wall (privacy) preference and "Other answers from the request form" (`Family.ContactNotes`: grief-support answer, custom questions staff add in the form editor, newsletter / prayer-night opt-ins), which were saved but never displayed.
- Intake fixes (follow-up): `PackageType` is matched ignoring case (the form's default "Comfort" used to fall through to Other) and the raw choice is kept in the request's staff notes ("Package requested: …"); the embedded form now keeps a second parent's different email / phone in the contact notes (both copies of `prayer-care-intake.html`, still identical). Tests: `IntakePackageTypeTests` (621 pass). If Duda holds a pasted copy of the form rather than an iframe, it needs re-pasting.
- Earlier-noted intake gaps (now fixed, see above): `PackageType` from the form is collapsed to a category and `"Comfort"` (capitalised default) doesn't match the lowercase `"comfort"` mapping so it becomes Other; when both parents give an email or phone only the first is kept.

## Production returns 405 for every PUT / DELETE ✅ (fix shipped, not yet verified live)

- After PRs #70/#71 deployed, Confirm Assignment showed "The server returned 405 Method Not Allowed" (thanks to the new error text). Probing production with harmless unauthenticated requests to a non-existent request id: `PUT /requests/0/{assign,priority,status,unassign}` and `DELETE /qa-sample-data` -> **405** with `Server: Microsoft-IIS/10.0`, `Allow: GET, HEAD, OPTIONS, TRACE`; `POST` and `PATCH` -> 401 (reach the app). IIS (its WebDAV module) answers PUT/DELETE before the app, so **no PUT/DELETE endpoint can work on the server** (assign, unassign, status, priority, due date, remove QA sample data, ...). This, not only the missing columns, is why assignment kept failing there.
- Fix: new `src/Lotv.Api/web.config` removing `WebDAVModule` and the `WebDAV` handler. The SDK merges its handler + `<aspNetCore>` into it at publish (checked with `dotnet publish`); the workflow's ASPNETCORE_ENVIRONMENT step still finds `<aspNetCore>`. Removing an absent module is harmless (tested on IIS Express, where WebDAV isn't installed: 200).
- **Verify after deploy:** re-run the probe (`PUT https://lotv_api.wte.net/api/v1/requests/0/assign`) - expect 401, not 405. If it is still 405, the block is elsewhere (site-level Request Filtering verbs or the WebDAV feature on the server) and IIS needs to be changed by whoever administers it.
- Also: the web app (`lotv.wte.net`) may need the same if it ever receives PUT/DELETE; it doesn't today.

## Story moved into the Notes Thread ✅

- Requested: put the story in the notes thread. The family's story from the request form is now the first entry in the case page's Notes Thread ("Story from the request form", dated with the request; "No story was shared with this request." when empty) and was removed from the Family panel so it isn't shown twice. It is read from the family record, not copied into a stored note, so it appears for every existing request without a data change and follows any later correction. Not browser-checked (E2E suite needs running servers on :5000/:5001).

## "Confirmed" lane: fix the gap, rename ✅

- Confirmed = the volunteer accepted the assignment (`POST /requests/{id}/accept` moves Assigned -> Confirmed). The **Confirm Assignment** button in the Queue / Kanban dialogs only assigns (stage Assigned), which made the lane name misleading.
- **Gap fixed:** `PUT /requests/{id}/assign` never created the pending `RequestAssignment` that Accept needs (only auto-assignment did), so Accept 404'd for hand-assigned cases and they could only reach Confirmed by dragging. New `Services/ManualAssignment.RecordAsync` creates it (acceptance window from the chapter, attempt number, who assigned), retires open assignments to someone else, does nothing when the same volunteer is chosen again, and the notification email now carries the real accept-by time. Choosing a different volunteer on a case at Confirmed sends it back to Assigned (the new volunteer hasn't accepted). No expiry / auto-reassign job exists for pending assignments, so this only enables Accept / Decline.
- **Lane renamed** on the Kanban board and the case page's "Process stage" line to **Volunteer Accepted** (`ProcessStageExtensions.ToDisplayName`); the stored value and the `Confirmed` column key are unchanged. Activity-log text still says "Confirmed".
- Tests: 3 new in `AssignmentWorkflowTests` (624 pass). Not browser-checked.
- Decision left open: the **Confirm Assignment** button label is unchanged (only the lane was renamed as requested).

## Volunteer accept / decline (wired up end to end) ✅

- Found: the volunteer page (`/volunteer/pending/{id}`) never called the accept/decline endpoints (Accept just set status InProgress; Decline set status New but left the volunteer assigned), the email linked to the staff case page, the endpoints sit under the Staff-only `/requests` group, and there was no check that the caller owns the case.
- Decided with the user: volunteers sign in with their own staff-portal username/password (Whitney creates it); a decline returns the case to the **unassigned queue** (no automatic reassignment).
- New `Services/AssignmentResponses` (accept -> Assigned -> Confirmed/"Volunteer Accepted"; decline -> Unassigned/New, assignee cleared, workload recomputed, activity logged) used by both the staff endpoints (`/requests/{id}/accept|decline`, decline no longer auto-reassigns) and the new `/api/v1/my-assignments/{requestId}` (GET), `/accept`, `/decline` (Volunteer policy; the login is matched to its volunteer record by email, else name; 404 for anyone else's case; decline only while pending; returns just what a volunteer needs - no internal notes, contact details or address).
- Page rewritten around those endpoints (already-accepted state, optional decline reason, no internal notes, empty layout instead of the template sidebar); the assignment email now links to `/volunteer/pending/{id}` ("Review and accept"); a push notification tells staff when a volunteer declines.
- Verified in a real browser (API + Web on a scratch SQL Server LocalDB, Volunteer-role login): view, Decline with a reason (case back to New/Unassigned, assignment Declined, activity logged, nobody auto-assigned), staff re-assign, Accept (stage Confirmed, assignment Accepted, activity logged). 628 tests pass (4 new).
- Not changed: the other volunteer pages (Dashboard, My Assignments, Available) call staff-only `/requests` endpoints, so a login with only the Volunteer role would get 403 there (by reading the code, not tested). A signed-out volunteer who follows the email link sees "Assignment Not Found" with a Sign in button, and lands on the dashboard after signing in (no return-to-page).
- Access model stated by the user (open): board and staff see all items; volunteers only the Prayer Request Package section.

## Nicolas Kremer provisioned as a volunteer ✅

- Requested: a profile for Nicolas Kremer, a volunteer, using kremer@wte.net. Added to `StaffAccountProvisioning.Accounts` (same mechanism as Susan Harper): username `nicolas.kremer`, **Volunteer role** (not admin), no chapter tie, random unknown starting password (he sets his own through Forgot password, or a secret `StaffAccounts:InitialPasswords:nicolas_kremer` can supply one), plus a volunteer record (role Prayer Ambassador, so automatic assignment never picks him; staff or a routing rule assign to him). `StaffAccount` gained a `Role` (default HQAdmin, so Susan is unchanged); `CoreAdminAccountRepair` uses its own fixed list and does not touch him. Created on the next deploy; nothing was written to any database from here.
- **Starting password:** the deploy workflow now feeds the GitHub secret `APP_NICOLAS_INITIAL_PASSWORD` into `StaffAccounts:InitialPasswords:nicolas_kremer` (same as Susan's). A strong random password was generated and stored only in that secret (it is not in the repo or any file); the person who asked was given it in chat once. It is applied when the account is created, or later to an account that has never signed in, and never after he has signed in or changed it. He should change it at first sign-in.
- Skipping an account because its email is already used by another account now logs a warning (it was silent).
- Tests updated to be independent of how many accounts are provisioned, plus assertions for Nicolas (628 pass).
- Login is matched to the volunteer record by email, else name, so keep the two consistent if edited in the app. If he should be in the automatic rotation, change his volunteer role in the app (Volunteer role edit).

## Request-form iframe could never shrink ✅

- Question: how to shorten the embedded request-form iframe when it doesn't need to be so long. Cause: `prayer-care-intake.html` posts its height to the parent (`lotvIntakeHeight`) using `documentElement.scrollHeight`, but the file is a pasteable fragment with no doctype, so inside an iframe it is in **quirks mode** where body / scrollHeight stretch to the frame. An iframe that starts too tall reported its own height back and never shrank (measured: reported 3000 while the form was 185px tall).
- Fix (both copies of the file, kept identical): measure the `#lotv-intake` container itself (bottom edge + body margins) and observe that element with the ResizeObserver. Verified in a browser: a 3000px iframe shrinks to 209px, grows to 1486px when the form opens. New E2E test `InAnOversizedIframe_TheFormReportsItsOwnHeight_SoTheFrameShrinksAndGrows` (fails with the old file by timing out, passes with the fix); the 31 existing intake E2E tests pass.
- **The parent page still needs the listener** (Duda: page or site-wide HTML/embed): `window.addEventListener("message", function (e) { if (e.data && e.data.lotvIntakeHeight) document.getElementById("lotv-intake-frame").style.height = e.data.lotvIntakeHeight + "px"; });` and the iframe needs `id="lotv-intake-frame"`. Pasting the whole file into a Duda Embed Code widget instead of an iframe needs no listener. A pasted copy on Duda must be re-pasted to get the fix; an iframe pointing at `/request-prayer-care-package` gets it on deploy.
- Note: `tests/Lotv.E2E` has `IsTestProject=false`, so plain `dotnet test` silently skips it; run with `-p:IsTestProject=true`.

## Requests stay unassigned; Cases Map; Needs info; funnel hidden (2026-09-25)

- **All new requests are unassigned (urgent, until the assignment filters exist in System Admin):** nothing is assigned automatically any more. One setting, `Intake:AutoAssign` (default off / unset), guards all three automatic paths: the public form (`/apply`), a case created by staff (`POST /requests`), and a request cleared from duplicate review. Every request now waits in the Unassigned Queue. The explicit staff actions still work ("Auto-assign" on a case, "Apply rules to queue", assigning by hand). `LotvApiFactory` turns the setting on so the existing assignment suites still exercise automatic assignment; `IntakeQueueTests` (4) check the default (a good volunteer exists and the request still waits) and that switching it on assigns. **Existing already-assigned requests are not touched.** The Assignment Rules page is unchanged; the user will build fuller filters into System Admin.
- **Form through the iframe** (verified in a browser with a host page + local API): all answers saved (both parents, phone, address, reason, how heard, bracelet initials, date of loss, story, notes incl. second parent's email), thank-you screen shows and the frame shrinks to it, confirmation + team emails are triggered (only logged until SMTP/SocketLabs is configured; `.invalid` addresses are never emailed by design), request lands New / Unassigned.
- **Cases Map was empty:** the map JS read `m.State` / `m.Value` (PascalCase) but Blazor JS interop sends camelCase, so every marker was skipped (same for Diocese Data and Impact Dashboard, which share `LeafletMap`). Fixed in `lotv-map.js` (reads either casing). Also: free-text states ("Illinois", "il", "Ill.") are normalised to two-letter codes with the new `UsStates.ToCode` (unrecognisable ones are listed under the map and still counted), `LeafletMap` initialises whenever markers first exist, and the app's own scripts get a `?v=<build timestamp>` so browsers pick up a fixed script after a deploy (the browser had kept running the old cached file). Verified: 5 circles for IL/WI/IN/TX/CA, Total/Open/Fulfilled all redraw. 20 new `UsStatesTests`.
- **"Needs info" 74 vs 13:** the Kanban button counted every case with a data problem but the board hid Fulfilled cases older than 90 days. While the filter is on the board now shows all of them (verified: button 10, cards 10, where 4 showed before). The Unassigned Queue tab's own "Needs info" still counts only queue cases.
- **Case Funnel hidden:** removed the Case Analytics tab, the legacy sidebar link and the description mention (page and component kept; restore the tab and the link to bring it back). `/admin/cases/funnel` still opens by URL.

## Review feedback triage (2026-09-25)

- **Fixed and verified in a browser:** (1) the default layout was the Blazor template ("Home / Counter / Weather" sidebar + "About") and wrapped every page that names no layout (14 pages) - `MainLayout` now adds nothing; (2) Events and Transparency drew a cut-down copy of the public header, now they use `PublicLayout` (the site's real header/footer); (3) the sidebar **Fund Allocations badge showed the overdue-case count** (`stats.Overdue`), now the allocations actually pending review (checked: 3 overdue cases, 0 pending -> badge 0); (4) **Push Subscriptions tab hidden** in System Admin (tab commented out, restore to bring it back); (5) page titles were boxed by a focus ring (Blazor focuses the h1 after navigation and a global `:focus-visible` rule drew it) - exception for `h1[tabindex=-1]`; the stylesheet now also carries the deploy-time `?v=` like the scripts.
- **Answers about System Admin / Platform Health (it is a static mock):** email is SocketLabs only (SendGrid survives only as an unused dev config block, an unused deploy secret and the Health page's mock rows); SignalR IS used (`RequestsHub` broadcasts case created / assigned / status changes; the Kanban refreshes from them); nothing uses Blob storage (the Health row saying "Azure Blob Storage accessible" is fake; `Expense.ReceiptBlobUrl` is only a URL field with no upload code); Hangfire is not installed (fake Health row; background work is ASP.NET hosted services).
- **Not done, need decisions:** "Login As" for admins (impersonation, needs audit logging), full case-view logging ("Chris Kremer viewed case ... at ..."), Cases page filters / hiding completed, Volunteers hub cases clickable, a volunteer with two roles (`Volunteer.Role` is a single enum today), inventory readiness for group build days, home page icons / "0+ dioceses", Families conditions as a dropdown.
- **Storage (AWS) is mid-build, uncommitted:** `Services/Storage/` (IFileStorage, key rules, local and not-configured implementations) and the AWSSDK.S3 package reference. Still to write: S3 implementation, provider selection, avatar-to-S3, upload endpoint, tests, deploy secrets, backup runbook.

## Iframe form background transparent (2026-09-25)

- The page served at `/request-prayer-care-package` wrapped the form in a body with `background:#fff`, a solid white box inside any iframe. The wrapper's `html` and `body` are now `transparent`, so the embedding site's background shows through (verified: a tan host page shows through; only the buttons and fields stay white). A pasted copy of the form file (Duda Embed Code) never set a page background, so it already matches its site.

## Every parish belongs to a diocese (2026-09-25)

- **Finding:** the Parish pages were reading the legacy **mock** route (`/api/parishes`); no endpoint read or wrote the `Parishes` table, dioceses had unvalidated CRUD, and families/donors carry parish and diocese only as free text. So nothing could enforce "every parish has a diocese".
- **Built:** real `/api/v1/parishes` (list with search / diocese / state / paging + `X-Total-Count`, get, create, edit, import). Create and edit require a diocese (by id, by name, or worked out from city and state); an edit that doesn't mention one keeps the parish's; duplicates (same name + city in a diocese) are refused; a diocese's parish counts follow its parish records. `Parish` gained `City` and `State` (added on existing databases by `MissingColumnBootstrap`, which now also covers the local SQLite file).
- **City / state matching** (`DioceseMatcher`, only when certain): the list named the diocese (wording-insensitive: "Chicago Archdiocese" = "Archdiocese of Chicago"), or the parish is in a diocese's seat city, or the state has exactly one diocese. Otherwise the parish is reported "needs a decision" with the candidate dioceses, never guessed. A named diocese that doesn't exist is rejected.
- **Import** (`POST /parishes/import`, HQ/Chapter admin/Director; UI on the Parish & Diocese Directory page): CSV with Parish, Diocese, City, State columns; dry run first; the real run adds only what it can place, skips repeats and never creates a parish without a diocese. Startup repair (`ParishDioceseRepair`) links any existing parish whose diocese id is invalid, by name / city / state when certain.
- **Pages:** ParishDirectory (server search + paging, City/State column, import panel), ParishDetail, DioceseDetail, DioceseData and ImpactReport now use paged / filtered calls, so thousands of parishes never load at once.
- **Verified** in a browser (real API, LocalDB): CSV upload -> dry run (7 rows: 4 can be added incl. "Wyoming" = WY, 1 repeat, 1 needs a decision with 2 candidates, 1 rejected) -> import -> 4 parishes each under a diocese, diocese counts 1/2/1 and KPIs updated; the 2 unplaceable rows were not added. 676 tests pass (new: `DioceseMatcherTests`, `ParishTests`).
- **US diocese list built** (public directory route): 185 territorial (arch)dioceses with seat city and state in `Data/Reference/us-dioceses.csv` (Wikipedia list + Wikidata seat/state, corrected by hand; the USCCB page blocks scripted access so it was not used; provenance in `docs/us-dioceses-source.md`). New admin action "Add the U.S. diocese list" (preview then add; idempotent; matches existing dioceses by name+state or seat city+state, so territories and reworded names aren't duplicated). Loaded dioceses are `IsDirectoryOnly`: excluded from the default diocese list and from the public "dioceses reached" (verified: 185 loaded, reached stays 0). `UsStates` now knows the territories and `DioceseMatcher` ignores a trailing "in <State>" ("Diocese of Springfield" finds "Springfield in Illinois"). Verified in a browser: preview -> add 185 -> a parish file with only Parish/City/State placed Chicago, Los Angeles, Cheyenne and Laramie and held Naperville and Oak Park (Illinois has 6 dioceses, neither is a seat) with the six candidates. 692 tests pass.
- **Limit that matters:** city + state alone places seat cities and single-diocese states only. Most parishes are in other towns of multi-diocese states and need either a diocese column in the file or a county-to-diocese map (the USCCB/Stamen diocesan boundaries are the candidate; not fetched).
- **Not done:** the ~16,000 parishes themselves. No free, complete parish-to-diocese list exists (the Catholic Parish Directory is a paid spreadsheet of about 14,000 rows; the USCCB blocks scraping; each diocese publishes its own). Data source chosen: a public directory (research pending; ~195 dioceses, ~16,000 parishes; terms of use and the parish-to-diocese mapping to be checked before anything is fetched or loaded). Existing production parishes are demo/none, so the repair will only report.

## Cases / Families / Volunteers polish, case-view log, Login As (2026-09-25)

- **Cases page:** defaults to "Open" (Fulfilled and Cancelled hidden unless you pick All / Fulfilled or search), shows 100 at a time with "Show more", and says how many completed cases are hidden. Verified in a browser (33 open rows, 38 with All).
- **Families:** the condition chips are one dropdown listing every reason. **Volunteers hub:** the active-case count links to that volunteer's cases (amber warning at 5 or more).
- **Case-view log (HIPAA-style):** opening a case, or a volunteer opening an assignment, writes a "viewed this case" entry (who and when; one per person per case per 10 minutes) shown on the case timeline. `CaseAudit`, `ActivityType.Viewed`. 4 new tests.
- **Login As (HQ admins only):** "Login as" button on Users (not for yourself, other admins or turned-off accounts) with a confirmation. Opens the portal as that person for 30 minutes with no refresh token; an orange banner shows on every layout with "Return to my account". Start and end go to the audit log, activity entries read "Name (signed in by Admin)", and account/profile changes (`/auth/*`, `/users/*` writes) are refused while active. Verified end to end in a browser. 7 new tests. 703 tests pass.
- **Still open:** two roles per volunteer, inventory build-day planner, home page icons and "0+ dioceses", S3 storage and backups (bucket and region needed), Kanban rename (name needed), access rules for volunteers vs Board, bulk-unassign of existing assigned requests (needs a yes).

## Access rule, diocesan map, Package Pipeline, icons, /apply, volunteer signup (2026-09-25)

- **Access rule** (`AccessRules.cs`, one place): staff (HQ admin, chapter admin, chapter staff, director) work with everything; **Board sees all the staff areas read-only** (the `Staff` and `ChapterAdmin` policies let Board through for GET only; any change is refused); **volunteers see only the prayer request package**: `/requests/mine` and, for a case assigned to them, its detail, notes, items, packing, stage, status, tracking (an endpoint filter on the requests group; everything else is 403, and someone else's case is 403). HQ-admin areas (system admin, users, API keys, chapters, forms) stay closed to Board and volunteers. `GET /volunteers/me` opened to volunteers. Web: volunteer sidebar is only My Work Queue and the Request Form, typing any other `/admin` address returns them to My Work Queue, no search or dashboard numbers, and the case page hides priority, assigned volunteer, due date, internal notes, other-request and mailing panels; Board gets the staff sidebar and no Save buttons on a case; Board no longer lands on `/board/portal` (page kept). Verified in a browser as a volunteer and as Board; 9 tests (`AccessRuleTests`).
- **Kanban board renamed "Package Pipeline"** (sidebar, page title, tab, command palette, buttons; route `/admin/kanban` unchanged).
- **Diocesan map** (parish to diocese by county and town): `us-county-dioceses.csv` (all 3,146 counties; source kburchfiel/us_diocese_mapper `counties_by_diocese.csv`, public domain; 26 split counties list both dioceses) and `us-place-dioceses.csv` (about 22,800 towns of 300 people or more, from GeoNames CC BY 4.0, kept only when unambiguous). `DioceseMatcher.Find` now also takes a county and the map: named diocese, then seat city, then county, then town, then the only diocese in the state; a split county lists both candidates and is never guessed. Parish import accepts a County column; single-parish create/edit accepts `county`. Naperville now places into Joliet; a small unknown town is still "needs a decision". Tests: `DioceseGeographyTests`, a county import test, adjusted Naperville expectations. Build scripts in `tools/us-dioceses/`; provenance in `docs/us-dioceses-source.md`. Not covered: Puerto Rico, Guam, Marianas, American Samoa.
- **Home page and volunteer signup icons:** emoji replaced by Font Awesome Free icons (inline SVG, `Shared/Icon.razor`; the golf icon for Parish Liaison is now a church). The "0+ dioceses reached" tile is hidden until there are partner dioceses to count.
- **`/apply`** now goes to the Prayer Care Package request form (`/request-prayer-care-package`), the same form as in the dashboard.
- **Public volunteer signup fixed:** it stored no chapter (chapter 0, which SQL Server's foreign key would refuse) and accepted any field the sender chose. It now uses the first active chapter, takes only the form's fields and always saves as Onboarding. Approval is Volunteers > Pending Onboarding; a login is a separate step on Users. 2 tests (`VolunteerSignupTests`).
- Full suite: 733 pass.

## Donations auto-create a pending allocation (2026-09-28)

- **Why:** `/admin/allocations` showed 20 in the sidebar and none on the page. Traced it: the sidebar count had already been fixed (was showing the overdue-case count, not allocations — fixed 2026-09-25, not yet deployed); the page itself was correctly empty because nothing ever created a Fund Allocation record — the Donations page's "Allocate" button only flipped a status flag on the donation and never touched the Allocations table.
- **Fixed:** every new donation (staff-entered, public gift form, public API, GiveButter sync) now creates a matching pending Fund Allocation the moment it's recorded, and keeps the donation's own status badge in step. Approving or rejecting an allocation now updates the donation's badge too. The Donations page "Allocate" button now really puts the donation in the queue instead of just changing a label.
- **Existing donations:** a startup catch-up (`AllocationBackfill`, same pattern as `ParishDioceseRepair`) gives any older unallocated donation a pending allocation too, so production's real donations show up in the queue after this deploys, not just new ones.
- 7 new tests (`AllocationAutoCreateTests`, `AllocationBackfillTests`); 737 tests pass.

## Reassignment logged as a move, not two assignments (2026-09-28)

- Moving a case from one volunteer to another now logs one "reassigned from X to Y" entry (who did it, both names) instead of a bare "assigned to Y" with no record of who had it before. A first-time assignment still logs "assigned to Y". Unassigning already logged who it was taken from. 2 new tests (`CaseAssignmentLogTests`); 739 tests pass.
- Checked against today's feedback: /admin/cases-hub "All Cases" already defaults to Open (completed hidden, Show more) — fixed 2026-09-25, still pending deploy. /admin/volunteers-hub "Directory" tab already has clickable, warning-flagged case counts — same. The case-view log ("who viewed which case, when") is also already built and pending deploy.

## Volunteers never see staff-internal notes (2026-09-28)

- **Found while reviewing second-order effects of the access rule:** a volunteer could read a case's internal (staff-only) notes — the "hidden from recipient" content, meant for staff coordinating the case — because the notes endpoint returned everything to anyone who could open the case. Fixed server-side (a volunteer's GET is filtered to public notes only) and the "Internal only" checkbox is hidden from volunteers when adding a note. 1 new test (`VolunteerNotesPrivacyTests`); 740 tests pass.
- **Checked and found fine:** the Package Pipeline (Kanban) view already hides fulfilled cases older than 3 months by default, so it doesn't have the same page-length problem Cases did.
- **Found, not yet fixed:** the Donations page's bulk "Mark Allocated" / "Mark Unallocated" buttons still only flip the donation's own status field — like the single "Allocate" button did before this session's fix — without touching the Fund Allocation table. Bulk-approved donations won't show correctly in the Allocations queue.

## Two roles per volunteer: Package Assembler and Prayer Ambassador (2026-09-28)

- **The ask:** "volunteers can have 2 roles. A Package person and a Prayer Person?" plus a real question — does Priya (a sample Prayer Ambassador) have anywhere to see who to pray for, or does she just log in to the package-assembly view?
- **Answer before this work:** no. `Volunteer.Role` was a single value, and the volunteer portal was one generic view (packing/shipping tasks) regardless of role — a Prayer Ambassador saw a box-packing checklist, nothing devotional.
- **Built:** `Volunteer.AdditionalRoles` (a volunteer keeps one primary role and can hold others — `HasRole()`/`Roles` read both); a column bootstrap adds it to existing databases. The Volunteers directory shows every role as a badge. Admin > "Change Role" now has checkboxes for the additional roles alongside the primary one. Auto-assignment now checks `HasRole(PackageAssembler)` / `HasRole(Admin)` instead of an exact match, so a dual-role volunteer is still eligible for packages; a prayer-only volunteer never is.
- **New Prayer List page** (`/admin/prayer-list`, sidebar link next to My Work Queue for volunteers): shows the families currently assigned to the signed-in volunteer — name, reason, "praying since" date, and the family's own story — with no case-management controls and nothing staff-internal. A volunteer without the Prayer Ambassador role sees a message saying so instead of an empty page. Verified in a browser: gave Priya the additional role, assigned her a case, logged in as her — Prayer List showed exactly that family and story; the sidebar showed both "My Work Queue" and "Prayer List".
- 9 new tests (`VolunteerRolesTests`, `PrayerListTests`, plus an `AutoAssignmentServiceTests` case); fixed two of this session's own new tests (`VolunteerNotesPrivacyTests`, `PrayerListTests`) that hardcoded chapter 1 and could flake other chapter-1 tests now that auto-assignment matches more broadly — each gets its own chapter, like the rest of the suite. 746 tests pass.
- **Not done:** a similar per-role landing page for other roles (Driver, Event Helper, Parish Liaison) if you want them; the legacy public `/volunteer/*` pages (separate from the real staff login) weren't touched.

## Prayer team: many people praying for one family (2026-09-28)

- **The ask:** "prayer list it could be many prayers to one who needs prayers" — the Prayer List I built earlier that day reused the single package-assignment field, so it could only ever show one Prayer Ambassador per family. Real ministry needs several people praying for the same family, and one Prayer Ambassador praying for several families — a many-to-many relationship, decoupled from who assembles and ships the actual box.
- **Built:** a new `PrayerTeamMember` join table (request, volunteer, who added them, when) — separate from `PackageRequest.AssignedToId`. Staff manage it from a new "Prayer Team" panel on the case detail page: add any volunteer who holds the Prayer Ambassador role, remove any of them, no limit on how many. `GET /api/v1/requests/my-prayer-list` gives a volunteer every family they've been added to pray for, independent of `/requests/mine` (their package assignments). The Prayer List page (`/admin/prayer-list`) now reads from this instead.
- **Verified in a browser:** added two Prayer Ambassadors (Ann and Bea) to one family's prayer team; the case detail page showed both. Logged in as Ann — her Prayer List showed that family even though her My Work Queue had zero cases (she wasn't the one packing it).
- 5 new tests (`PrayerTeamTests`); 751 tests pass.

## A Prayer Ambassador picks their own people (2026-09-28)

- **The ask:** "can a prayer pick their people they pray for" — before this, only staff could put someone on a prayer team; a Prayer Ambassador could only see who staff had already added them to.
- **Built:** the Prayer List page now has two parts. "Families You Could Pray For" lists every open case they aren't already on (name, reason, story — no address, tracking or internal notes), each with an "I'll pray for this family" button; the family drops out of that list and into their own once picked. Their own list gets an "I'll stop praying for this family" button to leave on their own. A new `GET /api/v1/requests/prayer-candidates` endpoint powers the browse list.
- **Kept safe:** a volunteer can only ever add or remove *themselves* — the API checks the acting volunteer's own record on both the join and the leave, so Ann can't add or remove Bea. Staff can still manage any family's team from the case detail page as before.
- **Verified in a browser:** Ann started with three candidate families and none of her own; picked Mia Sorrow and Sam Grief; they moved into "The families you're praying for" with a "stop praying" button, and Grace Hope stayed in the pick list.
- 3 new tests (self-service join, self-service leave-vs-can't-remove-others, can't add someone else); 754 tests pass.

## Prayer-only requests (no package) (2026-09-28)

- **The ask:** "the prayer list should include everyone who request a prayer care package along with anyone who just request prayers" — before this, the only way to reach LOTV was the Prayer Care Package form, which always created a package to assemble and ship. There was no way to ask for prayer alone.
- **Built:** the request form (both the site copy and the Duda embed, kept identical as always) now opens with "What would help most right now?" — a comfort package, or prayer only. It's a new standard question (`PackageRequest.WantsPackage`, column bootstrap for existing databases) the same way "Reason" or "How did you hear about us" are — staff can reword it in the form editor but can't remove it, since real behavior depends on it. Existing requests are unaffected (all default to wanting a package, which is all any of them ever were).
- A prayer-only request: never gets a package assembler auto-assigned (there's nothing to pack); the case detail page shows a "🙏 Prayer Only" badge and hides the Packing List panel entirely; it shows in "Families You Could Pray For" as any other request would, with the same badge, so Prayer Ambassadors see and can join every kind of request in one place.
- **Verified with a real submission** through the actual public form at `/request-prayer-care-package` (not just the API): picked "Prayer only," submitted, confirmed `WantsPackage = 0` in the database, saw the "Prayer Only" badge and no Packing List on the case, and saw it appear with a "Prayer only" badge in a Prayer Ambassador's browse list.
- 3 new tests (`PrayerOnlyRequestTests`); the new standard question required adding it to `IntakeFormDefinition.StandardKeys`, which needed no other test changes. 757 tests pass.
- **Known limit:** a chapter's form definition that staff have already customized and saved in the dashboard won't have this question until they re-save it (adding it, or using Reset to Default) — existing saved custom forms are still served as-is and don't retroactively gain new standard questions.

## Open Items

- [ ] PR kremer-dev → stage → main; then check the API log for "Added missing column" lines and click Load on production.
- [ ] If another `Invalid column name` appears, the bootstrap did not cover it — send the message.
- [ ] Startup errors previously swallowed on production (workload recompute, workflow repair) should now succeed; worth a look at the log after deploy.

---

**Session End** | **Branch:** kremer-dev ✅ Safe
