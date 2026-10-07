"""Manual 08 — Staff Guide: Cases Hub, Duplicates, Queues. Persona: Claire Hoffman (ChapterStaff, Chicago Metro)."""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lib import *  # noqa: E402

SLUG = "08-hub"
USER = "claire.hoffman"

TABS = [
    ("All Cases", "Every request, with four count boxes (Open Cases, Overdue, Fulfilled, Avg Age), status buttons, filters and a search box. This is the tab you will use most."),
    ("Package Workflow", "The drag-and-drop board of every active case. It is the same page described in **Staff Guide: The Package Workflow Board**."),
    ("Historical", "A read-only archive of closed cases from prior years. It is not part of the active pipeline; use its year, reason and search boxes to look something up."),
    ("Mother's Day Mailing", "One Mother's Day card per mother, for the year shown. Each row has **Mark Sent** and **Unflag** buttons; *Flagged* rows need a check before mailing."),
    ("Father's Day Mailing", "The same as the Mother's Day tab, for fathers."),
    ("Bereavement Follow-Up", "A tracker of follow-up touchpoints for each family at 3 weeks, 3 months, 6 months and 11 months after their loss. Click **Mark Sent** once a book or card has gone out."),
    ("Unassigned Queue", "Cases waiting for a volunteer, oldest first. Process 5 shows how to assign one."),
    ("My Work Queue", "Cases assigned to **you**, with **Details** and **Quick Update** buttons."),
    ("Overdue", "Open cases more than 7 days old with no resolution. Each row has **View** and, if nobody has it, **Assign**."),
    ("Bulk Update", "Tick several cases and apply one status or priority change to all of them in a single action."),
    ("Analytics", "Charts and tables: a heat map of priority against status, reasons, fulfilment time, intake trend and a map of cases."),
    ("At Risk", "Cases needing attention: overdue, unassigned, urgent, or stalled (no update for 14+ days)."),
]


def build(page):
    r = Recorder(SLUG, "Staff Guide: The Cases Hub, Duplicates & the Unassigned Queue", page)
    r.h1("Staff Guide: The Cases Hub, Duplicates & the Unassigned Queue")
    r.p("**Who this is for:** staff (HQAdmin, ChapterAdmin, ChapterStaff, Director). Board members can look but not change.")
    r.p("**When you finish you will be able to:** open the Cases Hub and know what all 12 tabs are for, find a case, decide what to do with a possible duplicate family, and assign an unassigned case to a volunteer (including yourself).")
    r.note("Families and numbers in the pictures are practice (sample) data.", "Practice data")

    # ---------------------------------------------------------------- 1
    r.process("Sign in and open the Cases Hub", "Reach the one page that gathers every way of looking at cases.", who="Staff", time="1 minute")
    r.signin(USER, "You land on the **Chapter Dashboard**.", who="your staff username")
    cases = page.locator('a.sidebar-link:has-text("Cases")').first
    r.step("Click Cases in the left menu",
           "In the green menu on the left, under **Prayer Request Package**, click **Cases**. (The number beside it is how many open cases there are.)",
           target=cases, action=lambda: (cases.click(), page.wait_for_selector("button.tab-btn", timeout=20000), page.wait_for_timeout(1500)), after=True,
           expect="A page titled **Cases** with two rows of tab buttons across the top.")
    r.check(["You can see the heading **Cases** and the tab buttons."])

    # ---------------------------------------------------------------- 2
    r.process("Learn the 12 tabs", "Know which tab to use for which job.", who="Staff", need="You are on the Cases Hub.", time="5 minutes")
    r.p("The tabs are two rows of buttons at the top. Clicking a tab swaps the content below but keeps you on the same page. Each tab is also available as its own page elsewhere in the app; the hub just puts them in one place.")
    for name, desc in TABS:
        tab = page.locator("button.tab-btn", has_text=re.compile(rf"^{re.escape(name)}$")).first
        r.step(f"Tab: {name}", f"Click **{name}**. {desc}",
               target=tab, action=lambda t=tab: (t.click(), page.wait_for_timeout(1800)), after=True,
               expect=f"The tab turns green and the heading below changes to match.")
    page.locator("button.tab-btn", has_text=re.compile(r"^All Cases$")).first.click()
    settle(page, 1500)
    r.check(["You can say which tab shows cases assigned to you.", "You can say which tab shows cases nobody has been given."])

    # ---------------------------------------------------------------- 3
    r.process("Find a case", "Locate one family's case quickly.", who="Staff", need="You are on the **All Cases** tab.", time="2 minutes")
    r.step("Pick a status",
           "Under the four count boxes is a row of buttons: **Open**, **All**, **New**, **In Progress**, **Awaiting Shipment**, **Shipped**, **Fulfilled**, **On Hold**, **Cancelled**, **Overdue**. Click one to show only those cases. **Open** (the default) hides finished and cancelled ones.",
           target=page.locator("button.filter-chip", has_text="In Progress").first,
           action=lambda: (page.locator("button.filter-chip", has_text="In Progress").first.click(), page.wait_for_timeout(1000)), after=True,
           expect="The table lists only cases in that status.")
    page.locator("button.filter-chip", has_text=re.compile(r"^All$")).first.click()
    settle(page, 800)
    box = page.get_by_placeholder(re.compile("Search families", re.I)).last
    r.step("Search by name",
           "Click the **Search families…** box above the table and type part of a family's name, for example *Whitaker*.",
           target=box, action=lambda: box.fill("Whitaker"), when="after",
           expect="The table shrinks to the matching families.")
    r.step("Open the case",
           "At the right end of the row, click **Details** to open the full case page, or **Quick Edit** to change just the basics.",
           target=page.get_by_role("link", name="Details").first, expect="Buttons **Details** and **Quick Edit** on the row.")
    r.note("The **Priority** and **Category** boxes next to the search box narrow the list further.", "Tip")
    r.check(["You found the family's row."])

    # ---------------------------------------------------------------- 4
    r.process("Decide what to do with a possible duplicate family", "Stop the same family getting two packages — or confirm they are genuinely different.", who="Staff", time="3 minutes")
    r.p("When someone submits the public request form, the system compares the new family with every existing family **in the same chapter**, in this order, and stops at the first match:")
    r.bullets(["Same **email address** as an existing family.", "Same **phone number** (digits only — dashes and spaces do not matter).", "Same **last name and ZIP code** together."])
    r.p("If any matches, the new request is **held out** of the Unassigned Queue, auto-assignment and the Pipeline until a person decides. It shows only on the Possible Duplicates page.")
    pd = page.locator('a.sidebar-link:has-text("Possible Duplicates")').first
    r.step("Click Possible Duplicates in the left menu",
           "In the green menu, click **Possible Duplicates**. The small number beside it is how many are waiting.",
           target=pd, action=lambda: (pd.click(), page.wait_for_url(re.compile(r".*duplicate-review"), timeout=20000), page.wait_for_timeout(1500)), after=True,
           expect="A page titled **Possible Duplicate Families**. Each waiting request shows the new submission beside the existing family it matched.")
    r.step("Read the match reason and compare",
           "Find the line saying why they matched — for example *Same email address as existing family (Daniel & Elena Whitaker)*. Compare the new submission with the existing family: names, address, phone, children. To see more history, click **View family →** to open the existing record, then come back.",
           target=page.get_by_text(re.compile(r"Same .* as existing family", re.I)).first,
           expect="A match reason and two columns of details to compare.")
    keep = page.get_by_role("button", name=re.compile("Not a Duplicate"))
    merge = page.get_by_role("button", name=re.compile("Merge into Existing Family"))
    r.step("Decide",
           "Two buttons: **Not a Duplicate — Keep as New Family** (they are different people; the case continues into the normal pipeline) and **Merge into Existing Family #…** (they are the same family; the new request is folded into the existing record).",
           target=[keep.first, merge.first], expect="Both buttons side by side under the comparison.")
    r.note("There is **no undo**. If you are not sure, open the existing family first and compare more history. In this walk-through we click **Not a Duplicate**.", "Take care")
    r.step("Click your choice",
           "Click **Not a Duplicate — Keep as New Family**.",
           target=keep.first, action=lambda: (keep.first.click(), page.wait_for_timeout(2500)), after=True,
           expect="That entry disappears from the list. The case now appears in the Unassigned Queue like any other. When none are left the page says there are none to review.")
    r.check(["The entry you decided is gone from Possible Duplicates."])

    # ---------------------------------------------------------------- 5
    r.process("Assign an unassigned case — to yourself or someone else", "Give a waiting case an owner.", who="Staff",
              need="To assign it to **yourself** you need your own volunteer record with the Package Assembler role. If you do not have one, create it first: open **My Work Queue** and click **Create my volunteer record**.", time="3 minutes")
    uq = page.locator('a.sidebar-link:has-text("Unassigned Queue")').first
    r.step("Click Unassigned Queue in the left menu",
           "Click **Unassigned Queue**. The number beside it is how many cases are waiting.",
           target=uq, action=lambda: (uq.click(), page.wait_for_url(re.compile(r".*/admin/queue"), timeout=20000), page.wait_for_timeout(1500)), after=True,
           expect="Four count boxes — **Unassigned**, **Overdue & Unassigned**, **Available Volunteers**, **Oldest Waiting** — and a table of cases, oldest first.")
    ab = page.get_by_role("button", name="Assign", exact=True).first
    r.step("Click Assign next to a case",
           "Find the case and click **Assign** at the right of its row.",
           target=ab, action=lambda: (ab.click(), page.wait_for_selector("h3:has-text('Assign Case')", timeout=15000), page.wait_for_timeout(800)), after=True,
           expect="A panel opens on the right with the family's summary: contact details, address, parish and diocese, faith tradition, date of loss.")
    drawer = page.locator("h3:has-text('Assign Case')").locator("xpath=ancestor::div[2]")
    r.step("Review the family summary",
           "Read the summary at the top of the panel. Check the address is complete before you give the case to anyone.",
           target=drawer.get_by_text("Family:").first.locator("xpath=.."), expect="A tinted box with contact details and address at the top of the panel.")
    sel = drawer.locator("select").first
    r.step("Choose a volunteer",
           "Choose who gets the case, one of two ways: open **Assign To** and pick a name (each name shows how many active cases they already have), or click one of up to five **suggested volunteers**, ranked by workload — clicking one selects it for you. **To take the case yourself, pick your own name.** There is no separate “assign to me” button.",
           target=sel, action=lambda: sel.select_option(index=1), when="after",
           expect="The chosen name appears in the **Assign To** box.",
           trouble="Your own name is missing? You have no volunteer record yet — see *Before you start* above.")
    conf = drawer.get_by_role("button", name="Confirm Assignment")
    r.step("Click Confirm Assignment",
           "Click **Confirm Assignment**.",
           target=conf, action=lambda: (conf.click(), page.wait_for_timeout(2500)), after=True,
           expect="The panel closes and the case leaves the queue. It now appears in that volunteer's **My Work Queue** and on the Package Workflow in **Assigned**.")
    r.check(["The case is no longer in the Unassigned Queue."])
    r.note("New package requests may also be assigned **automatically** to a Package Assembler in the same chapter (when auto-assign is on), within each volunteer's case limit (6 by default). A volunteer who does not accept within 24 hours (4 hours if Urgent) loses it to the next person. Use this queue to assign or reassign by hand at any time.", "Automatic assignment")

    r.h2("If something looks wrong")
    r.bullets([
        "**A new request is missing from the Unassigned Queue.** It may be a possible duplicate — check **Possible Duplicates**.",
        "**My own name is not in the Assign To list.** Create your volunteer record first (My Work Queue → *Create my volunteer record*).",
        "**I clicked Merge by mistake.** There is no undo. Tell an administrator straight away; they can correct the records.",
        "**A request marked prayer-only is not in the queue.** Prayer-only requests have no package to assemble, so they never need an assembler.",
    ])
    r.write("08-Staff-Guide-Cases-Hub-Duplicates-Queues")
    return r


if __name__ == "__main__":
    session(None, build)
