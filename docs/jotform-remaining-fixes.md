> **SUPERSEDED 2026-09-10 — no action needed on anything below.** JotForm was removed from the app entirely per direct client instruction ("we don't need to have Stripe or JotForm"). Intake now runs through a standalone Duda-embeddable form (`docs/duda-embed/prayer-care-intake.html`) posting directly to `Lotv.Api`, with no third-party HIPAA-BAA account involved. See `MASTER_TODO.md`'s 2026-09-10 session section and `docs/LOTV-PM-Plan.md` risk register R-26. Kept below for historical reference only.

# JotForm Prayer Care Package Intake — Remaining Fixes

**Form:** `261395566857171` (`form.jotform.com/261395566857171`, hosted under the account's HIPAA subdomain)
**Last verified against live form:** 2026-09-03
**Constraint that shapes every item below:** the JotForm account has a signed HIPAA Business Associate Agreement (`isHIPAA: 1`, account-level). JotForm requires every schema/settings change on a HIPAA-BAA account to go through their own audited builder UI, and silently discards programmatic writes — confirmed across 9+ API write attempts, all reporting `200 success` while writing nothing. **Nothing in this document can be applied via the API. Every "Builder" step below is a manual click-path in the JotForm form editor.**

---

## Already fixed (for reference — no action needed)

- Background color, "Quaterly" → "Quarterly" typo, radio default
- "How did you hear about us?" wording
- Opt-in Communications checkboxes no longer pre-checked
- Reason dropdown has an "Other" option and is now required
- The `$10` / "No thank you" orphaned condition (referenced deleted field `84`) was deleted
- Husband's/Wife's Email and Phone Number fields renamed to be distinct (paired webhook code change applied same session — see `Program.cs` `knownLabels` and the `Field("Husband's Email") ?? Field("Wife's Email")` fallback chain)
- Email notification/autoresponder no longer use the `{husbandsName}` merge tag or "Eric Garrison" as the From name; the misleading "Re:" subject prefix is gone

---

## 1. Delete the second orphaned condition (field `67`)

**Status:** Not done. **Priority: do this immediately, same pass as anything else in the builder.**

**What it is:** While confirming field `84` was cleaned up, a full diff of every field ID referenced in `form/261395566857171/properties` → `conditions` against the actual live question list turned up a second dead reference: **field `67`** doesn't exist (confirmed via a direct `GET form/261395566857171/question/67` → `404 Question not found`), but it's still listed in the action targets of two active conditions:

- Condition: `field 38 equals "For Someone Else"` → **Show** fields `[..., 67, 69]`
- Condition: `field 38 equals "For Me"` → **Hide** fields `[65, 58, 67]`

Unlike the `84` orphan (a standalone donation-amount rule), this one sits **inside the core "For Me" / "For Someone Else" radio branching** — the exact logic area the client already flagged once as "you deleted all the conditions" (investigated at the time, found to be a false alarm/stale builder cache, not real corruption). This is real corruption, just harmless: JotForm silently skips a reference to a field that no longer exists, so nothing currently breaks. But it's a second piece of debris in the one spot everyone's already nervous about, and it should be cleaned up so the Condition Wizard panel stops carrying dead weight.

**How to fix:**
1. Builder → Settings (or the Conditions panel) → Conditions
2. Open the "For Someone Else" show-rule and the "For Me" hide-rule tied to the `prayerCare` (Prayer Care Package Options) field
3. In each rule's field list, remove whatever entry corresponds to the missing field — it may show as a blank/unlabeled entry or an error indicator in the builder UI, since the field itself no longer exists to render a label for
4. Save, then re-pull `form/261395566857171/properties` (or ask for a re-verification pass) to confirm `67` no longer appears in any condition's `fields` array

**Risk:** None to submission parsing — this field was never in the webhook's `knownLabels` and never contributed data. Pure cleanup.

---

## 2. The form has never been connected to anything — this is the real priority

**Status:** Not done. **This is bigger than every other item in this document combined.**

**What it is:** Two separate confirmations point to the same conclusion:
- `GET form/261395566857171/webhooks` returns an **empty array** — zero webhooks registered on JotForm's side, ever
- The form's own `count` property (total submissions) is **`0`**

Put together: **no live submission from this form has ever reached the LOTV app.** Every parsing fix discussed across every JotForm review session — the Quarterly typo, the husband/wife label collision, the bracelet-initials bug, all of it — has been correct work, but it's been correct work against a pipe that was never turned on. Whatever is populating `Prayer Care Package Request Database.xlsx` today is coming from staff manually keying in submissions from wherever they actually receive them (email, phone, the raw JotForm submissions dashboard), not from this webhook.

**Why it's stuck:** JotForm needs a public HTTPS URL to POST submissions to. The LOTV API (`/api/v1/webhooks/jotform` in `Program.cs`) is not deployed publicly anywhere — Phase 6 (Deployment) is blocked on the client's cloud hosting decisions (hosting provider, DNS, SSL). This is a pre-existing, already-tracked blocker, not a new discovery — this review just confirmed its real-world consequence directly against the live form rather than inferring it.

**How to fix, once a public API URL exists:**
1. Deploy `Lotv.Api` somewhere with a public HTTPS endpoint (Phase 6 — needs client's hosting decision first; not a JotForm-side task)
2. Builder → Settings → Integrations → Webhooks → add webhook → point it at `https://<public-api-host>/api/v1/webhooks/jotform`
3. Alternatively, this can be done via the JotForm API even on a HIPAA-BAA account — webhook *registration* is a subscription action, not a schema/settings edit, so it's worth testing whether `POST form/261395566857171/webhooks` actually lands (the HIPAA-BAA restriction has only been confirmed against question/properties edits so far, not webhook subscriptions specifically)
4. Submit one real test entry through the live public form and confirm a `WebhookEvent` row appears in the database (`Source = "jotform"`) and a `Family`/`PackageRequest` gets created correctly
5. Cross-check that test submission's parsed fields against what actually landed in the app, end to end, before considering this closed

**Action item independent of the technical fix:** flag this directly to the ministry now, regardless of when Phase 6 hosting lands. If they believe the form has been collecting/forwarding submissions automatically this whole time, they should know that's not the case — every submission to date has needed a human to notice it and transcribe it into the spreadsheet by hand.

---

## 3. Email "From" name polish (cosmetic, low priority)

**Status:** Partially done — the actual bugs are fixed; what's left is a display-name preference.

**What's already fixed:** the notification email no longer uses the broken `{husbandsName}` merge tag as its "From," the autoresponder no longer shows "Eric Garrison" as a personal sender name, and the misleading "Re:" subject prefix is gone.

**What's still generic:** both emails now use JotForm's account default sender rather than a ministry-branded name:
- Notification 1 → `from: default|default`
- Autoresponder 1 → `from: none|Jotform` — a submitter's autoresponder email currently shows as coming from "Jotform," not "Lily of the Valley Ministry"

**How to fix:**
1. Builder → Settings → Emails → Notification 1 → From → set an explicit display name (e.g., "Lily of the Valley Ministry") rather than leaving it on the account default
2. Same screen → Autoresponder 1 → From → change from "Jotform" to "Lily of the Valley Ministry"
3. No code impact — these are outbound-only settings never read by the webhook parser

**Risk:** None. Purely cosmetic; not urgent, but a submitter getting an autoresponder "from Jotform" after requesting bereavement support is a small miss worth fixing whenever the ministry is next in the builder.

---

## 4. Scope the donation nudge to "For Someone Else" only

**Status:** Not done.

**What it is:** The thank-you page (`activeRedirect: thanktext`) currently shows one static message to **every** submitter, including a donation link:

> "If you'd like to help us send care packages to other grieving families, you can support the ministry [here]."

This was supposed to be scoped to the "For Someone Else" branch only — asking someone who just requested a comfort package for their *own* pregnancy/infant loss to donate is the wrong tone. Right now that distinction doesn't exist; every submitter sees the same page.

**How to fix:**
1. Builder → Settings → Thank You Page
2. This needs JotForm's **Conditional Thank You Page** feature specifically — a separate tool from the field-level Condition Wizard (so setting it up can't put the existing "For Me"/"For Someone Else" field show/hide logic at risk)
3. Add a condition: when `Prayer Care Package Options` (`prayerCare`) equals "For Someone Else" → show the donation-nudge variant (copy already drafted, see below)
4. Leave the default/fallback thank-you page (shown when the condition doesn't match, i.e. "For Me") as the plain thank-you text with **no** donation link
5. Optional: apply the same scoping logic to the autoresponder email body, using the drafted email-version copy, so the "For Me" branch's autoresponder also skips the ask

**Drafted copy** (from the 2026-09-01/02 review session — reuse rather than redrafting):
- **Thank You page (For Someone Else):** "Thank you for your submission. If you'd like to help us send care packages to other grieving families, you can support the ministry [here]." *(this is the copy currently live everywhere — just needs to be gated to this branch only)*
- **Thank You page (For Me):** plain "Thank you for your submission." with no donation link
- **Autoresponder addition (For Someone Else only):** a short paragraph appended to the confirmation email inviting the requester to donate, using the same "support other grieving families" framing — not yet drafted verbatim in prior sessions beyond the concept; write final wording when this is implemented

**Risk:** Low, if done through the Conditional Thank You Page tool specifically rather than trying to route this through the Condition Wizard (which only controls field visibility, not page-level redirects).

---

## 5. HIPAA toggle — client decision, not a mechanical fix

**Status:** Not done. `isHIPAA: 1` at the account level.

**What it is:** This is the setting that causes every API write to this form (and any other form on this account) to silently no-op — confirmed as platform policy, not a bug, not fixable from our side. Turning it off would restore programmatic editability for all future JotForm work on this account.

**This is not a checklist item — it needs a direct conversation with the ministry first.** Turning it off is entirely their call, and hinges on questions only they can answer:
- Does the BAA still need to be active given how this form is actually being used today (bearing in mind finding #2 above — it's not even connected to anything yet)?
- Is there PHI/HIPAA-covered data genuinely flowing through this specific form, or was the BAA set up for a different original purpose?
- If they want to keep the BAA but also want easier programmatic edits, the only other path is asking JotForm support directly about an audited API tier — worth raising with them as an alternative to an outright toggle-off, if that's on the table

**How to fix (once the ministry decides):**
- If disabling: Builder → Account Settings → HIPAA compliance section → turn off, likely with a confirmation flow given the signed BAA
- If keeping it on: no action, but document the decision so future sessions don't waste time re-attempting API writes

---

## 6. Three-page split

**Status:** Not done. Form is currently one main page + one "Donation Page" (a single `control_pagebreak` field, `qid 82`).

**What it is:** A previously-requested UX improvement to break the long single-page intake form into three logical sections, reducing how overwhelming it feels to fill out in one scroll. **This isn't purely mechanical — there's no drafted layout yet for where the breaks should fall.**

**How to fix:**
1. **Needs the client's input first**: decide the three groupings. A reasonable default split, based on the field order already on the form, would be:
   - **Page 1 — Who this is for:** Prayer Care Package Options, Husband's/Wife's Name, Husband's/Wife's Email/Phone, Recipient's Address
   - **Page 2 — About the loss:** Reason for Prayer Package Request, Date of Recent Loss, Faith Tradition, Diocese, Parish, How did you hear, Story, Children for Bracelet, anonymity preference, custom message, Opt-in Communications, Requester Name/Email/Phone/Address, captcha
   - **Page 3 — Donation (existing):** unchanged
   - This is a suggestion, not a decision — confirm with the ministry before building it
2. Builder → drag two more Page Break fields into place at the agreed split points
3. **After publishing, submit one live test entry** and confirm the webhook's `pretty` submission field still concatenates every page's answers into one string exactly as before — JotForm's submission payload shouldn't change shape based on pagination, but this specific form has never been tested with more than its current one page break, so don't assume it without checking
4. No code change expected, but re-run the JotForm webhook test suite mentally against the real payload shape once you have a live example, in case pagination adds any wrapper structure the parser doesn't already handle

**Risk:** Low technically, but blocked on a content/UX decision, not a builder mechanic.

---

## Suggested order of operations

1. **Now:** Delete the field-`67` orphaned condition (#1) — takes two minutes, same builder session as anything else
2. **Now, separately from the form itself:** Tell the ministry directly that the form has 0 real submissions ever received (#2) — this is a conversation, not a builder task, and it's more urgent than any cosmetic fix on this list
3. **Whenever next in the builder:** Fix the email From names (#3) — quick, no dependencies
4. **After a short client conversation:** Scope the donation nudge (#4) and decide the HIPAA toggle (#5) — both need a client answer before they're mechanical
5. **After Phase 6 hosting lands:** Register the webhook and do a live end-to-end test (#2, continued)
6. **Whenever the client has time to weigh in on page groupings:** the 3-page split (#6) — lowest urgency, purely UX polish
