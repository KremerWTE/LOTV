# Session Notes — 2026-09-01/02: JotForm Design Review & HIPAA-BAA Root Cause

## Summary

A full design/content review of the live JotForm intake form, prompted by the client flagging the background color, the "Quaterly" typo, and confusing "For Me"/"For Someone Else" behavior. This session finally root-caused *why* every JotForm API write attempt across this entire project has ever silently failed — and, along the way, found and fixed a real live bug the client's own manual edit introduced, plus a genuine data-visibility gap surfaced by comparing the app against the ministry's spreadsheet.

---

## The Review

Pulled the live form's full properties and question list via the read-only API and found:

- **Background**: two layered non-white colors — page background `#F3F3FE` (pale lavender, almost certainly what read as "light purple") and a tan `#d0c9c0` form-card background.
- **"Quaterly Grief Support"**: confirmed typo, one field, low-risk fix.
- **The "For Me"/"For Someone Else" overlap**: the conditional logic itself was correct (`For Someone Else → show recipient fields`, `For Me → hide them`) — the actual bug was that the radio had no default value, so before any selection the recipient fields sat in their default-visible state, looking like both branches were showing at once.

Gave the client manual, click-by-click instructions for all of this rather than attempting API edits — given this exact form's prior corruption incident (2026-08-05) and six previously-failed properties edits, color/theme changes specifically were flagged as too risky to attempt via API at all (theme colors are compiled across `background`, `styleJSON`, and a large generated CSS blob, keyed to a theme revision ID — a raw API edit bypasses JotForm's own recompilation step).

## Root-Causing the JotForm API Restriction, For Good

At the client's request, attempted several of the lower-risk items (HIPAA toggle, radio default, Reason dropdown "Other") via the API anyway, this time testing a previously-untried endpoint (`form/{id}/question/{id}` single-question edit, distinct from bulk `properties` and bulk `questions` creation). Every attempt reported `200 success`; none actually wrote anything — confirmed via immediate GET-diffs after each one, and a final full `form/{id}/questions` pull showing every one of the 27 questions byte-for-byte unchanged.

That's 9 total failed write attempts across this project's history now, spanning 3 different endpoints and 5 different field types, all with the identical silent-no-op signature. Pulled the account's own `user` endpoint to check for a structural reason rather than continuing to guess at payload shapes, and found it:

```
"region": "HIPAA"
"isHIPAA": "1"          (account level, not just this form)
"baaSubmissionID": "5951596922638984868"
```

This account has an actual signed **HIPAA Business Associate Agreement** on file with JotForm. JotForm requires every schema/settings change on a HIPAA-BAA account to go through their own audited builder UI — so every edit is logged for compliance — and deliberately discards programmatic writes for exactly that reason. This isn't a bug, a wrong API key, or a payload-shape problem. **It's platform policy, and it's not fixable from our side.** Every future JotForm change goes through the builder UI by hand; the one alternative path is the ministry contacting JotForm support directly to ask about an audited API tier, if that's worth pursuing.

## A Live Bug From the Client's Own Fix

While confirming the client's manual edits landed (checked the live form directly), found the background color and the "Quaterly" typo were both already fixed. Good news on its face — but this immediately surfaced a real, live-breaking consequence: the webhook's `knownLabels` array in `Program.cs` still expected the old "Quaterly Grief Support" misspelling to anchor its label-boundary regex. Left alone, this would have silently mis-parsed every new submission from that point forward, and risked corrupting the *adjacent* fields' captured values too — the boundary match is what keeps "Date of Recent Loss" and "Faith Tradition" from bleeding into each other (the exact mechanism behind the original 2026-08-12 "Children for Bracelet" data-loss bug).

Fixed in the same sitting the typo fix was confirmed: `knownLabels` and the two `JotFormWebhookTests` payloads that reference it now say "Quarterly". 433/433 tests still pass. This is exactly the coordination risk flagged earlier for the "Husband's/Wife's Name" rename — confirmed live that it's a real, not theoretical, risk class.

## A False Alarm, Investigated Properly

Mid-session, the client reported "you deleted all of the conditions" — a serious claim given the production stakes. Checked immediately and repeatedly (3 separate times: 2 live click-throughs on the actual published form, 1 raw API data pull) and found the conditional logic fully intact and functioning correctly every time. Declined to "rebuild" conditions that provably already existed and worked — attempting an API write to "fix" something not actually broken would have been pure downside (guaranteed no-op at best, a duplicate/conflicting rule at worst) given the confirmed API restriction. Most likely explanation: a stale-cache rendering issue in the builder's own Condition Wizard panel, distinct from the live published form.

## Donation Nudge Wording

Drafted, at the client's request, copy for a "support the ministry" nudge scoped specifically to the "For Someone Else" branch — dropped entirely for "For Me" submitters, since asking someone who just requested support for their own loss to donate is tone-deaf. Both a Thank You page version and an autoresponder-email addition were drafted. Implementation needs JotForm's **Conditional Thank You Page** feature specifically (separate from the Condition Wizard, so it can't put the existing field show/hide logic at risk) — not yet applied, pending the ministry's manual setup.

## Spreadsheet vs. App: Field-Coverage Comparison

Asked to check "the difference between the spreadsheet and the Kanban cards from JotForm." No production database access exists (no credentials, no network path to the private `10.100.1.87` host, and connecting an AI session directly to a database holding real families' PII isn't something to do unprompted anyway) — so this was done at the field-coverage level: does the code capture each spreadsheet column, and does the UI actually *display* it, rather than a literal record-by-record diff.

Found and fixed a real gap: **`Family.DateOfLoss`** — the field that drives the entire bereavement follow-up tracker's milestone math (3-week/3-month/6-month/11-month dates) — was captured correctly but displayed **nowhere in the app's UI at all**, confirmed via a repo-wide grep across every `.razor` file. Separately, the full shipping address (street + apt, not just city/state), Diocese, "How did you hear", and the family's Story were all captured and displayed correctly — but only on the standalone Family Detail page, not on Case Detail, which is where staff actually work a case day to day. Staff processing a case had no way to see the mailing address needed to actually ship a package without navigating away.

**Fixed**: added Shipping Address (full), Diocese, Date of Loss, How They Heard, and Story to Case Detail's Family panel. Verified live against several real seeded cases, both with each field populated and null (confirmed correct `—` fallbacks), zero console errors. 433/433 tests still pass.

## Current Project State

- **Branch**: `kremer-dev` (pushed to both `origin` and `wtesolutions`)
- Live JotForm form: background color, "Quaterly" typo, and the radio default all confirmed fixed by the client; matching webhook code fix (`knownLabels`) already applied for the typo.
- Still pending, manual builder UI only (confirmed non-negotiable given the HIPAA-BAA finding): autoresponder/notification "From" fields, HIPAA toggle, "How did you hear" wording, pre-checked opt-in checkboxes, Reason dropdown "Other" option, 3-page split, donation-nudge implementation, and — highest-stakes, needs to be paired with a code change — the "Husband's/Wife's Name" rename.
- No production database access; the spreadsheet-vs-app comparison this session was code/field-coverage level, not a live-record diff.
