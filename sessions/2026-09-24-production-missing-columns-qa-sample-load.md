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
- **Not done:** the actual list of US dioceses and parishes. Data source chosen: a public directory (research pending; ~195 dioceses, ~16,000 parishes; terms of use and the parish-to-diocese mapping to be checked before anything is fetched or loaded). Existing production parishes are demo/none, so the repair will only report.

## Open Items

- [ ] PR kremer-dev → stage → main; then check the API log for "Added missing column" lines and click Load on production.
- [ ] If another `Invalid column name` appears, the bootstrap did not cover it — send the message.
- [ ] Startup errors previously swallowed on production (workload recompute, workflow repair) should now succeed; worth a look at the log after deploy.

---

**Session End** | **Branch:** kremer-dev ✅ Safe
