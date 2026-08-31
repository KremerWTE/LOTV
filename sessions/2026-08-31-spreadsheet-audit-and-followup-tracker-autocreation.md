# Session Notes — 2026-08-31: Spreadsheet-Audited Field Coverage & Bereavement Follow-Up Auto-Creation

## Summary

Verified the JotForm intake pipeline against the ministry's actual, currently-in-use operational spreadsheet (`Prayer Care Package Request Database.xlsx`) rather than treating the JotForm schema as sufficient ground truth on its own. That audit surfaced a real functional gap — new intake never created a bereavement follow-up tracker — fixed it, and fixed a serialization regression the fix itself surfaced.

---

## Spreadsheet Audit

Read the `2026` sheet (1,276 real rows — their live, currently-active tracking) and the `SM (2026)` sheet (Stephen's Ministry bereavement tracking) directly via `openpyxl`, and cross-checked every column against what the webhook/data model actually capture.

**Confirmed good:**
- Core intake fields (names, contact, address, reason, story, faith, bracelet initials, parish/diocese, date of loss) all map cleanly, and this session's earlier fixes (2026-08-12/13) already closed the real gaps that existed.
- `SM (2026)`'s structure (child name, date of loss, 3wk/3mo/6mo/11mo due dates, Book 1-4 Sent) has exact 1:1 parity with the already-built `FollowUpTracker`/`FollowUpMilestone` model. Derived the exact milestone offset formula from real due-date deltas in the sheet: 3 Weeks = DateOfLoss + 21 days; 3/6/11 Months = DateOfLoss.AddMonths(3/6/11).

**Found gaps:**
1. **No code path ever created a `FollowUpTracker` from live intake.** Confirmed via `grep -n "FollowUpTracker" Program.cs` — zero matches outside the read/update endpoints. The table was only ever populated by the one-time `tools/LegacyImport` historical import. The API only exposed `GET` (list) and `PUT .../sent` — no `POST` to create, and no "add tracker" button in the UI either. Once the historical backlog is worked through, the ministry's actual, currently-running bereavement book-mailing process would silently stop for every new family.
2. **"How did you hear about us?" fidelity loss** — real spreadsheet answers are rich free text (actual referrer names, personal context: *"Referral from Megan Kreft"*, *"I received a care package myself"*, *"A Dear Sister in Christ who has a baby son in Heaven"*), but the live JotForm form (after the 2026-08-05 cleanup session) only offers a fixed dropdown. Not fixed — same blocker as R-19 (no working authenticated JotForm access), and also a product decision (add an "Other, specify" field vs. accept the trade-off) rather than a pure bug.

---

## Fix: Auto-Create Follow-Up Tracker on Real Intake

Added `CreateFollowUpTrackerIfLossKnownAsync(LotvDbContext db, Family family)` in `Program.cs`, called from both the JotForm webhook and the public `/apply` endpoint right after auto-assignment. Gated on `family.DateOfLoss.HasValue` — the natural real-world signal for "an actual loss occurred with a known date" (naturally excludes Infertility "for me" requests, which have no lost child and no date to schedule against).

## Regression Found and Fixed: Reference-Cycle 500 on the List Endpoint

Verifying the fix live (POST a realistic submission → check `/admin/follow-up-trackers`) immediately 500'd. Root cause: `GET /api/v1/follow-up-trackers` does `.Include(t => t.Milestones)` and returns the list directly. Once a tracker is created with both `FollowUpTracker.Milestones` and each `FollowUpMilestone.FollowUpTracker` tracked in the *same* EF context, EF's relationship fixup populates the back-reference automatically — producing a real object cycle (`Tracker → Milestones → Tracker → ...`) that the default `System.Text.Json` serializer (no `ReferenceHandler` configured anywhere in this API) recurses into infinitely. The historical import apparently never triggered this same fixup, so the bug was fully latent until real intake exercised it.

Fixed with a scoped `[JsonIgnore]` on `FollowUpMilestone.FollowUpTracker` — confirmed via the Razor page's own code that nothing ever reads that back-reference client-side, so this is a pure serialization-surface fix, not a behavior change. Deliberately did *not* add a global `ReferenceHandler.IgnoreCycles` — that would silently paper over the same latent bug class in `Retreat`/`SilentAuction` (the only other two models with a real bidirectional collection nav property) without anyone verifying whether it's actually reachable there too.

## Tests

Added 4 tests to `tests/Lotv.Tests/Integration/JotFormWebhookTests.cs`:
- Tracker created with correct milestone dates for a realistic submission with a date of loss
- No tracker created for an Infertility submission with no date of loss
- The list endpoint regression test — actually hits `GET /api/v1/follow-up-trackers` after creating a tracker via the webhook, asserting 200 (not 500) and that milestone data is actually present in the response body

433/433 tests passing (429 → 433 this session's worth of additions total).

## Verified Live

Ran the API + Web locally, POSTed a realistic submission with a date of loss straight at the webhook, and confirmed via Playwright screenshot that `/admin/follow-up-trackers` renders the new tracker's milestones with correct due dates (loss 8/1/2026 → 3wk 8/22/2026, 3mo 11/1/2026, 6mo 2/1/2027, 11mo 7/1/2027 — all match the derived formula exactly).

## Current Project State

- **Branch**: `kremer-dev`
- JotForm → Kanban → bereavement follow-up pipeline: fully verified end-to-end for real intake, code-side
- Still open: live JotForm form settings (HIPAA, sender names, "how did you hear" fidelity) blocked on authenticated access; hosting platform still unspecified by the client
