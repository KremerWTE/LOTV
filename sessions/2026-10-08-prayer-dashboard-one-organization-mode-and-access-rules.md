# Session Summary — Prayer Dashboard rebuilt and opened to everyone; chapters switched off (one-organization mode); section access table

**Date:** 2026-10-08 | **Branch:** kremer-dev | **Status:** ✅ Complete. 797/797 tests pass; one-organization mode checked against the running app (API + Playwright). Manuals (`docs/manuals/`) deliberately not committed.

## Request
Make the Prayer Dashboard visible to all staff/employees (and never hidden), very simple, showing who is praying and how many; drop chapters everywhere for now (production too); only Chris Kremer sees everything, everyone else sees cases, prayer pages and admin only if their role allows; Board gets the Prayer Dashboard; remove the Chapter onboarding step; the dashboard is no longer called "HQ Dashboard".

## What changed
- **Prayer Dashboard** (`PrayerList.razor`): one plain list of every open family — name, reason, date, story, "N praying: names", and a single join/stop button. Families you pray for sort first. No stat cards, no setup screen. A volunteer record is created (as Prayer Ambassador) only when someone first taps "I'll pray for this family". Board sees the list with no buttons.
- **Prayer API:** `prayer-candidates` now returns `PrayingNames`. Viewing the prayer lists needs no role/record. Joining needs a volunteer record with Prayer Ambassador **or Package Assembler** (staff were being refused when their record was PackageAssembler).
- **One-organization mode:** `Chapters:Enabled` (default **false**; read after `builder.Build()` so test/deploy overrides apply). `ChapterContextService.ChapterId` returns null → every chapter filter is skipped. SignalR uses one shared group (`ChapterMode.GroupFor`). Auto-assignment no longer matches volunteers by chapter. Three queries that compared `ChapterId == ctx.ChapterId` directly were made null-safe. No schema change, no migration, no chapter rows needed (`ChapterId` is a plain int, FK not enforced).
- **UI:** chapter pickers hidden on 17 create/edit forms (default ChapterId = 1 submitted); Chapters / Chapter Analytics / Chapter Health menu entries hidden (`ChapterUi.Enabled`); the Chapter step removed from both onboarding wizards (5 steps → 4); "HQ Dashboard" / "HQ Chapter Dashboard" renamed "Dashboard"; empty "System Admin" heading hidden for non-admins.
- **Access table** `SectionAccess.cs` (one place to change policy): chris.kremer = everything; Volunteer = Prayer (plus My Work Queue only if they are a Package Assembler); Staff/Admin = Prayer, Cases, Admin; Board = Prayer (view only) + Cases (read-only). **Analytics is hidden from everyone but Chris for now** — planned: Admin and Board get Analytics (one-line edits, commented in the file). Volunteers land on the Prayer Dashboard.
- **Tests:** test host runs with `Chapters:Enabled=true` (suite was written for chapter scoping); two tests rewritten and one added for the new assembler/driver prayer rules; `PrayerTeamTests` updated. 797 pass.

## Verified (local app, throwaway DB, one-organization mode)
Chris, ChapterStaff (two accounts) and Board all see the same 51 cases / 49 families / 17 volunteers / 43 prayer families regardless of chapter; a prayer-only volunteer sees only "Prayer Dashboard" and lands on it; a new public request appears for every staff account; staff can create families and donors with no chapter picker; wizards have no chapter text.

## Not verified / left open
- No automated test for one-organization mode itself (a static switch would race with parallel tests); checked manually instead.
- SignalR live updates in one-org mode were not exercised.
- Decision made, easy to flip: a volunteer who is *only* a Prayer Ambassador sees just the Prayer Dashboard; Package Assemblers keep My Work Queue so packing is not cut off.
- Child's name on loss cases: only `ChildrenInitials` exists (bracelet field), so it was removed again at the user's request; a real "child's name" intake question would be a separate form change.
- **Prayer log** (counting individual prayers by type) not built — waiting on the list of prayer types.
- Remaining chapter wording: the "HQ Operations Board" name, chapter reports that are only reachable by Chris, and the admin Chapters pages (hidden from menus, routes still work).
- Production: nothing deployed from this session. `Chapters:Enabled` is not set anywhere, so production defaults to one-organization mode on deploy.
