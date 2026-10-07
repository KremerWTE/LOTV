"""Manual 05 — Staff Guide: Managing Cases. Persona: Claire Hoffman (ChapterStaff, Chicago Metro)."""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lib import *  # noqa: E402

SLUG = "05-cases"
USER = "claire.hoffman"
CASE = 37          # Oliver & Vivian Delgado — a New, unassigned case in Chicago Metro


def panel(page, title):
    return page.locator("div.panel").filter(has=page.locator(".panel-header", has_text=title)).first


def top(page):
    page.evaluate("window.scrollTo(0,0)")
    page.wait_for_timeout(400)


def build(page):
    r = Recorder(SLUG, "Staff Guide: Managing Cases", page)
    # off-screen set-up: a prayer ambassador exists so the Prayer Team panel has someone to add
    pt = api_token("penny.prayer@example.org")
    api("POST", "/api/v1/volunteers/me", pt, json={"Role": "PrayerAmbassador"})

    r.h1("Staff Guide: Managing Cases")
    r.p("**Who this is for:** HQAdmin, ChapterAdmin, ChapterStaff and Director. Board members can see everything below but every change is refused.")
    r.p("**When you finish you will be able to:** open a case, understand every box on its page, assign a volunteer, change status, priority and due date, "
        "record tracking, manage the packing list and prayer team, write notes, and read the activity log.")
    r.note("Families in the pictures are practice (sample) data. Case #37 is used throughout so you can follow the same case from start to finish.", "Practice data")

    # ---------------------------------------------------------------- statuses
    r.h2("The statuses a case moves through")
    r.p("Every case has one **Status**. A case can only move to a status that makes sense from where it is now, so the dropdowns only ever offer the allowed next steps. You can never jump straight from New to Shipped.")
    r.md += [
        "| From this status | You can move it to |",
        "|---|---|",
        "| **New** (waiting for a volunteer) | InProgress, OnHold, Cancelled |",
        "| **InProgress** (a volunteer has it) | New (sent back to the queue), AwaitingShipment, Fulfilled, OnHold, Cancelled |",
        "| **AwaitingShipment** (packed, ready for the carrier) | Shipped, Fulfilled, OnHold, Cancelled |",
        "| **Shipped** (with the carrier) | Fulfilled, OnHold |",
        "| **OnHold** (paused) | New, InProgress, AwaitingShipment, Cancelled |",
        "| **Fulfilled** (delivered — finished) | OnHold |",
        "| **Cancelled** | OnHold |",
        "",
    ]
    r.bullets([
        "**Tracking number first:** a case cannot be marked **Shipped** until it has a tracking number.",
        "**Prayer-only requests** have no package, so they never need an assembler and skip the package statuses. They only ever wait on the Prayer Team.",
    ])

    # ---------------------------------------------------------------- 1
    r.process("Sign in and open a case", "Reach a case's own page.", who="Staff", time="2 minutes")
    r.signin(USER, "You land on the **Chapter Dashboard**.", who="your staff username")
    cases = page.locator('a.sidebar-link:has-text("Cases")').first
    r.step("Click Cases in the left menu", "Click **Cases** in the green menu on the left.",
           target=cases, action=lambda: (cases.click(), page.wait_for_selector("button.tab-btn", timeout=20000), page.wait_for_timeout(1500)), after=True,
           expect="The **Cases** page with a table of every case.")
    box = page.get_by_placeholder(re.compile("Search families", re.I)).last
    r.step("Search for the family", "Click the **Search families…** box above the table and type part of the name — here, *Delgado*.",
           target=box, action=lambda: (box.fill("Delgado"), page.wait_for_timeout(1000)), when="after",
           expect="The table shows only matching families.")
    det = page.get_by_role("link", name="Details").first
    r.step("Click Details", "Click **Details** at the right end of the row.",
           target=det, action=lambda: (det.click(), page.wait_for_url(re.compile(r".*/admin/cases/\d+"), timeout=20000), page.wait_for_timeout(2000)), after=True,
           expect="The case's page, headed **Case #37 — Oliver & Vivian Delgado**, with coloured badges for status and priority.")
    r.note("You can also open a case from the Package Workflow (click the card's name) or from any list's **Details** button.", "Tip")
    r.check(["The heading shows the case number and family name."])

    # ---------------------------------------------------------------- 2
    r.process("Tour of the case page", "Know what every box on the case page shows.", who="Staff", need="You are on a case's page (Process 1).", time="5 minutes")
    top(page)
    r.step("The header",
           "At the top: the case number and family name, then badges for **Status** (for example *New*), **Priority** (Urgent, High, Normal, Low) and, if it applies, **Prayer Only** or **Overdue**, then the date it was created. A *SAMPLE* badge only appears on practice data.",
           target=page.locator("h2", has_text="Case #").first, expect="Badges under the case name.")
    r.p("The page has two columns. **Left:** what you edit and read about the case. **Right:** the family, helpers and shortcuts.")
    cd = panel(page, "Case Details")
    r.step("Case Details (left) — the editing panel",
           "Everything you can change about the case: **Status**, **Priority**, **Assigned Volunteer**, **Due Date** and **Internal Notes (staff only)**. **Category** and **Chapter** are shown but fixed. If you set Status to AwaitingShipment, Shipped or Fulfilled, **Tracking Number** and **Shipped Date** boxes appear. Nothing is saved until you click **Save Changes**; **Discard** throws away your edits and reloads what is saved.",
           target=cd, expect="Boxes for Status, Priority, Assigned Volunteer, Due Date, Category, Chapter and Internal Notes, with **Save Changes** and **Discard** buttons.")
    ri = panel(page, "Request Information")
    r.step("Request Information",
           "Read-only facts: who the request is for, the reason, when it was submitted and last updated, the **Process stage**, whether staff outreach was requested, the bracelet/children's initials, who it is assigned to, tracking and shipped date (once entered), and — if the request was made for someone else — who referred them.",
           target=ri, expect="Two columns of labelled facts.")
    fam = page.locator("div.panel").filter(has=page.locator(".panel-header", has_text=re.compile(r"^\s*Family"))).first
    r.step("Family (right) — contact and address",
           "The family's name, parents, email, phone, **Shipping Address**, parish, diocese, faith tradition, date of loss, how they heard of us, quarterly grief-support choice, and their **Prayer wall** privacy choice (named, anonymous, or prayers only). **View Profile →** at the top right opens the family's own record for deeper edits.",
           target=fam, expect="A long list of the family's details.")
    notes = panel(page, "Notes Thread")
    r.step("Notes Thread",
           "The family's own story from the request form always comes first (it is read live from the family record, so it stays current if corrected). Staff notes follow, newest last. Notes with an **Internal** tag are never shown to the recipient.",
           target=notes, expect="A green *Story from the request form* box, then any notes, then a box to add one.")
    act = panel(page, "Activity Log")
    r.step("Activity Log",
           "A timestamped history of everything that has happened to the case — who created it, who assigned it, who changed its status — each entry with a name. Use it to answer *who did that and when?*",
           target=act, expect="Entries such as *… created this case*, newest at the top.")
    r.check(["You can say where to change priority, where to read the address, and where to find the history."])

    # ---------------------------------------------------------------- 3
    r.process("Assign a volunteer", "Give the case an owner.", who="Staff", need="The case is **New** (unassigned).", time="2 minutes")
    r.p("There are two ways. **Way A** (fastest) uses the ranked suggestions. **Way B** picks anyone from a list.")
    cand = panel(page, "Auto-Assignment Candidates")
    r.step("Way A: look at the Auto-Assignment Candidates box",
           "On the right, **Auto-Assignment Candidates** lists volunteers ranked for this case. The best match has a green **✓ Best fit** tag. Each shows a **score** and the **distance in miles** from the family.",
           target=cand, expect="Up to six names, each with a score, a distance and an **Assign** button.")
    ab = cand.get_by_role("button", name="Assign", exact=True).first
    r.step("Click Assign beside the person you choose",
           "Click **Assign** under the volunteer you want. It happens immediately — there is no confirmation step.",
           target=ab, action=lambda: (ab.click(), page.wait_for_timeout(2500), top(page)), after=True,
           expect="The Status badge changes to **InProgress** and the volunteer's name appears under *Assigned to* in Request Information.")
    r.note("**Way B:** in **Case Details**, open **Assigned Volunteer**, pick a name (each shows how many active cases they have), then click **Save Changes**. To take a volunteer **off** a case, use **↩ Unassign** in Quick Actions (Process 5).", "Way B")
    r.check(["Status is InProgress.", "*Assigned to* shows the volunteer's name."])

    # ---------------------------------------------------------------- 4
    r.process("Change priority, due date or internal notes", "Edit the case's details and save.", who="Staff", need="You are on the case page.", time="3 minutes")
    cd = panel(page, "Case Details")
    prio = cd.locator("select").nth(1)
    r.step("Change the Priority",
           "In **Case Details**, open **Priority** and pick Urgent, High, Normal or Low. Urgent cases get a shorter acceptance window (4 hours instead of 24).",
           target=prio, action=lambda: prio.select_option(label="High"), when="after", expect="The box shows your choice.")
    due = cd.locator("input[type=date]").first
    r.step("Set a Due Date (optional)",
           "Click **Due Date** and choose a date, or type it as month/day/year.",
           target=due, action=lambda: due.fill("2026-10-20"), when="after", expect="A date in the box.")
    inn = cd.locator("textarea")
    r.step("Write an internal note (optional)",
           "In **Internal Notes (staff only)** type anything staff should know. Volunteers and the family never see this box.",
           target=inn, action=lambda: inn.fill("Family prefers a phone call before delivery."), when="after", expect="Your text in the box.")
    sv = cd.get_by_role("button", name="Save Changes")
    r.step("Click Save Changes",
           "Click **Save Changes**. All your edits in this panel are saved together.",
           target=sv, action=lambda: (sv.click(), page.wait_for_timeout(2000), top(page)), after=True,
           expect="A green message **✓ …** at the top of the page, and the Priority badge in the header now says **High**.",
           trouble="Changed your mind before saving? Click **Discard** and the panel reloads what is saved.")
    r.check(["The header shows the new priority.", "A green confirmation appeared."])

    # ---------------------------------------------------------------- 5
    r.process("Change the status", "Move the case to its next step.", who="Staff", need="You are on the case page. The case is InProgress (Process 3).", time="3 minutes")
    r.p("There are two ways: the **Status** box (pick, then save) or the **Quick Actions** buttons (one click). Both follow the same rules.")
    st = cd.locator("select").first
    r.step("Way A: open the Status box",
           "In **Case Details**, click **Status**. It lists only the statuses allowed next, under *InProgress (current)*.",
           target=st, action=lambda: show_options(st), expect="A short list of allowed next statuses.")
    hide_options(st)
    r.step("Choose AwaitingShipment",
           "Pick **AwaitingShipment** (packed, ready for the carrier). **Tracking Number** and **Shipped Date** boxes appear.",
           target=st, action=lambda: st.select_option(label="AwaitingShipment"), when="after", expect="Two new boxes under the status.")
    tr = cd.locator('input[placeholder="1Z999AA10123456784"]')
    r.step("Type the tracking number",
           "Type the carrier's tracking number with no spaces. It is required before the case can later be marked Shipped.",
           target=tr, action=lambda: tr.fill("1Z999AA10123456784"), when="after", expect="The number in the box.")
    sd = cd.locator("input[type=date]").nth(1)
    r.step("Choose the Shipped Date",
           "Pick the date the box went to the carrier.",
           target=sd, action=lambda: sd.fill("2026-10-08"), when="after", expect="A date in the box.")
    sv = cd.get_by_role("button", name="Save Changes")
    r.step("Click Save Changes",
           "Click **Save Changes**.",
           target=sv, action=lambda: (sv.click(), page.wait_for_timeout(2000), top(page)), after=True,
           expect="The Status badge in the header now says **AwaitingShipment**, and Request Information shows the tracking number.")
    qa = panel(page, "Quick Actions")
    r.step("Way B: use Quick Actions",
           "On the right, **Quick Actions** has one button for every allowed next status — for example **🚚 Mark Shipped**, **✅ Mark Fulfilled**, **⏸ Put On Hold**, **✖ Cancel Case** — plus **↩ Unassign** if someone has the case. Click one to move the case in one step. Here we click **⏸ Put On Hold** to pause it.",
           target=qa, expect="A column of buttons. The set changes with the case's status.")
    hold = qa.get_by_role("button", name=re.compile("Put On Hold"))
    r.step("Click Put On Hold",
           "Click **⏸ Put On Hold**. The case pauses.",
           target=hold, action=lambda: (hold.click(), page.wait_for_timeout(2000), top(page)), after=True,
           expect="The Status badge says **OnHold**.")
    back = panel(page, "Quick Actions").get_by_role("button", name=re.compile("Mark AwaitingShipment"))
    r.step("Resume the case",
           "To resume, click the button for the status it should return to — here **📦 Mark AwaitingShipment**.",
           target=back, action=lambda: (back.click(), page.wait_for_timeout(2000), top(page)), after=True,
           expect="The Status badge is **AwaitingShipment** again.")
    r.note("The small buttons **↩ Mark New** (send back to the queue) and **↩ Unassign** take the case away from its volunteer. Use them when a volunteer cannot do it.", "Sending a case back")
    r.check(["The Status badge shows the status you intended.", "The Activity Log has a new entry for each change."])

    # ---------------------------------------------------------------- 6
    r.process("Manage the packing list", "Record exactly what goes in the box.", who="Staff (volunteers can only tick items off)", need="A case that wants a package.", time="4 minutes")
    pl = panel(page, "Packing List")
    sel = pl.locator("select")
    r.step("Choose an item",
           "Scroll down the right column to **Packing List**. Open **— Select item —** and choose an item. Only items **with stock on hand** for your chapter are listed, each with the amount available.",
           target=sel, action=lambda: sel.select_option(index=1), when="after", expect="The chosen item with its available count.")
    add = pl.get_by_role("button", name="Add")
    r.step("Choose the quantity and click Add",
           "Leave the number at **1** or change it, then click **Add**.",
           target=add, action=lambda: (add.click(), page.wait_for_timeout(1500)), after=True,
           expect="The item appears in the list, and the counter at the top reads *0 / 1 packed*.")
    sel2 = pl.locator("select")
    sel2.select_option(index=2)
    pl.get_by_role("button", name="Add").click()
    page.wait_for_timeout(1200)
    cb = pl.locator("input[type=checkbox]").first
    r.step("Tick items as they are packed",
           "Click the tick-box beside an item once it is physically in the box. A ticked item is crossed out. Volunteers do this step too.",
           target=cb, action=lambda: (cb.check(), page.wait_for_timeout(1500)), after=True,
           expect="The item is crossed out and the counter goes up, for example *1 / 2 packed*.")
    rm = pl.get_by_role("button", name="Remove").last
    r.step("Remove a wrong item",
           "To take an item off, click **Remove** at the right of its line.",
           target=rm, action=lambda: (rm.click(), page.wait_for_timeout(1500)), after=True,
           expect="The item disappears and the counter updates.")
    r.note("If you move a case toward Shipped while something is still unticked, a **⚠ Not everything is packed yet** warning shows so nothing ships half-packed.", "Safety check")
    r.check(["Every item in the list is ticked before the box is shipped."])

    # ---------------------------------------------------------------- 7
    r.process("Manage the prayer team", "Add or remove Prayer Ambassadors for this family.", who="Staff", need="Someone has the **Prayer Ambassador** volunteer role.", time="2 minutes")
    pt_panel = panel(page, "Prayer Team")
    psel = pt_panel.locator("select")
    r.step("Choose a Prayer Ambassador",
           "Find **Prayer Team**. Open **— Add a Prayer Ambassador —** and choose a person. Only people with that role who are not already on the team are listed. The prayer team is completely separate from whoever packs the box; any number of people can pray for one family.",
           target=psel, action=lambda: psel.select_option(index=1), when="after", expect="A name in the box.")
    padd = pt_panel.get_by_role("button", name="Add")
    r.step("Click Add",
           "Click **Add**.",
           target=padd, action=lambda: (padd.click(), page.wait_for_timeout(1500)), after=True,
           expect="The person's name is listed with a **Remove** button beside it.")
    prm = pt_panel.get_by_role("button", name="Remove").first
    r.step("Remove someone",
           "To take a person off the team, click **Remove** beside their name.",
           target=prm, action=lambda: (prm.click(), page.wait_for_timeout(1500)), after=True,
           expect="The name disappears from the list.")
    r.check(["The team list shows who you intended."])

    # ---------------------------------------------------------------- 8
    r.process("Write a note", "Leave information for the next person, optionally hidden from the recipient.", who="Staff (volunteers can add notes but not mark them Internal)", time="2 minutes")
    nt = panel(page, "Notes Thread")
    ta = nt.locator("textarea")
    r.step("Type your note",
           "Scroll to **Notes Thread**. Click the **Add a note…** box at the bottom and type your note.",
           target=ta, action=lambda: (ta.fill("Called the family; delivery address confirmed."), ta.press("Tab")), when="after", expect="Your text in the box.")
    ck = nt.locator("input[type=checkbox]")
    r.step("Decide whether it is Internal only",
           "The tick-box **Internal only (hidden from recipient)** is ticked by default for staff. Leave it ticked for anything the family should not see. Untick it only for a message meant for them.",
           target=ck, expect="A ticked box beside *Internal only*.")
    ad = nt.get_by_role("button", name="Add Note")
    r.step("Click Add Note",
           "Click **Add Note**.",
           target=ad, action=lambda: (ad.click(), page.wait_for_timeout(1800)), after=True,
           expect="Your note appears in the thread under your name with the time, tagged **Internal**.")
    r.check(["The note appears in the thread with your name."])

    # ---------------------------------------------------------------- 9
    r.process("Check other requests from the same family", "Catch a repeat request before sending a second package.", who="Staff", time="2 minutes")
    r.p("Every case page has an **All Requests from This Family** box listing the family's other cases with their statuses. If another request is still open, a blue banner at the top says **This family has another open request** and links to it — check it is not a repeat before shipping a second package.")
    page.goto(f"{WEB}/admin/cases/13")
    settle(page, 2000)
    fr = panel(page, "All Requests from This Family")
    r.step("Find All Requests from This Family",
           "On the case page, look for **All Requests from This Family** and the blue banner above the panels. Click any `#` number to jump to that case. **+ New Case for This Family** in Quick Actions starts a second request on purpose.",
           target=fr, expect="A list of this family's cases, newest first, each with a status badge.")
    r.check(["You know whether this family already has another open case."])

    r.h2("If something looks wrong")
    r.bullets([
        "**The Status box does not offer the status I want.** Only allowed next steps appear. See the table at the start of this guide, and move one step at a time.",
        "**I get *A tracking number is required before marking a case Shipped*.** Type the tracking number, click **Save Changes**, then mark it Shipped.",
        "**I changed something and want it back.** Before saving, click **Discard**. After saving, change it again — and check the Activity Log for who changed what.",
        "**The volunteer I want is not in the list.** They may be at their case limit (6 by default), inactive, or in another chapter.",
        "**There is no Packing List box.** Prayer-only requests have nothing to pack.",
    ])
    r.write("05-Staff-Guide-Managing-Cases")
    return r


if __name__ == "__main__":
    session(None, build)
