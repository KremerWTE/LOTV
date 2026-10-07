"""Manual 07 — Staff Guide: The Package Pipeline Board. Persona: Claire Hoffman (ChapterStaff, Chicago Metro)."""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lib import *  # noqa: E402

SLUG = "07-pipeline"
USER = "claire.hoffman"


def col(page, label):
    return page.locator(".kanban-col").filter(has=page.locator(".kanban-col-hd span", has_text=re.compile(rf"^{label}$", re.I)))


def wide(page, on=True):
    """Whole-board view: widen the window and zoom out so all nine columns fit in one picture."""
    if on:
        page.set_viewport_size({"width": 1700, "height": 560})
        page.evaluate("document.body.style.zoom='0.72'")
    else:
        page.evaluate("document.body.style.zoom='1'")
        page.set_viewport_size(VIEWPORT)
    page.wait_for_timeout(700)


def drag(page, card, target_col, hold_shot=None):
    """Real mouse drag (HTML5 drag-and-drop). hold_shot: callable run while the card is held over the column."""
    b = card.bounding_box()
    t = target_col.locator(".kanban-col-hd").bounding_box()
    page.mouse.move(b["x"] + b["width"] / 2, b["y"] + 25)
    page.mouse.down()
    page.mouse.move(b["x"] + b["width"] / 2 + 8, b["y"] + 30, steps=4)
    page.mouse.move(t["x"] + t["width"] / 2, t["y"] + 60, steps=12)
    page.wait_for_timeout(500)
    if hold_shot:
        hold_shot()
    page.mouse.up()
    page.wait_for_timeout(1500)


def build(page):
    r = Recorder(SLUG, "Staff Guide: The Package Pipeline Board", page)
    r.h1("Staff Guide: The Package Pipeline Board")
    r.p("**Who this is for:** staff (HQAdmin, ChapterAdmin, ChapterStaff, Director). Board members can look at the board but cannot change it.")
    r.p("**When you finish you will be able to:** open the board, read every column and card, assign a case to a volunteer, move a case to its next stage by dragging, "
        "understand why a move is refused, find cases with missing family information, and open a case's full page.")
    r.p("The board is a picture of every active package case. It updates by itself when anyone else changes a case — you never need to refresh it.")
    r.note("Cards marked **SAMPLE** are practice data used for these pictures. Real cases do not have that badge.", "Practice data")

    # ---------------------------------------------------------------- 1
    r.process("Sign in and open the board", "Get to the Package Pipeline.", who="Staff", time="1 minute")
    r.signin(USER, "You land on the **Chapter Dashboard**.", who="your staff username")
    pp = page.locator('a.sidebar-link:has-text("Package Pipeline")').first
    r.step("Click Package Pipeline in the left menu",
           "In the green menu on the left, under **Prayer Request Package**, click **Package Pipeline**.",
           target=pp, action=lambda: (pp.click(), page.wait_for_selector(".kanban-col", timeout=20000), page.wait_for_timeout(1500)), after=True,
           expect="A page titled **Package Pipeline** with columns of cards side by side.")
    r.check(["You can see the heading **Package Pipeline** and columns of cards."])

    # ---------------------------------------------------------------- 2
    r.process("Read the columns", "Know what each column means and how a case moves across them.", who="Staff", need="You are on the Package Pipeline.", time="3 minutes")
    wide(page)
    r.step("See the whole board",
           "This picture is zoomed out so you can see every column at once. Cases move **left to right** as work happens. The small number in each column's header is how many cards it holds. On your own screen, scroll sideways to see columns that do not fit.",
           expect="Nine columns: New, Assigned, Volunteer Accepted, Packing, Notes, Shipping, On Hold, Fulfilled, Cancelled.")
    wide(page, False)
    r.p("**What each column means**")
    r.md += [
        "| Column | What it means | Who usually moves a card here |",
        "|---|---|---|",
        "| **New** | A request nobody is handling yet. | Arrives automatically from the public form |",
        "| **Assigned** | A volunteer has been given the case. | Staff, by clicking **Assign** |",
        "| **Volunteer Accepted** | The volunteer has confirmed they will do it. | Staff or the volunteer |",
        "| **Packing** | The box is being assembled. A placeholder shipping record is created automatically when a case reaches Packing. | Staff or the volunteer |",
        "| **Notes** | A note or card is being written for the family. | Staff or the volunteer |",
        "| **Shipping** | Packed and waiting for, or on its way with, the carrier. | Staff or the volunteer |",
        "| **On Hold** | Paused for a reason. Can resume later. | Staff |",
        "| **Fulfilled** | Delivered; the case is complete. Only the last 3 months are shown here. | Staff or the volunteer |",
        "| **Cancelled** | Closed without a package. | Staff |",
        "",
    ]
    r.check(["You can name the nine columns in order.", "You know that cases move left to right."])

    # ---------------------------------------------------------------- 3
    r.process("Read a card", "Understand every line on a case card.", who="Staff", need="You are on the board.", time="3 minutes")
    card = col(page, "Assigned").locator(".kanban-card").first
    card.scroll_into_view_if_needed()
    r.step("Look at one card",
           "A card is one family's request. From top to bottom it shows:\n\n"
           "1. **Case number** and a coloured **priority** badge (Urgent, High, Normal, Low). The coloured edge on the left also shows priority.\n"
           "2. **Family name.**\n3. **Reason** for the request.\n"
           "4. A small line: whether it is **For Self** or **For Someone Else**, their faith tradition if known, and **Bracelet** initials if children's initials were given. Anything that does not apply is left out.\n"
           "5. A **tracking number** with a 📦 icon, once one has been entered.\n"
           "6. A **⚠ Check family info** badge, only if something about the family record looks incomplete or inconsistent. Hover over it to read what is wrong.\n"
           "7. At the bottom: who it is **Assigned** to (or *Unassigned*) and how long the case has been open. The age turns red when the case is overdue.\n"
           "8. A button: **👤 Assign** if nobody has the case, **Unassign** if someone does.",
           target=card, shot="element",
           expect="A white card with the lines above.")
    r.note("Click the card's body (anywhere except its button) to open that case's full page. Process 7 shows this.", "Tip")
    r.check(["You can find the case number, family, reason, assignee and age on a card."])

    # ---------------------------------------------------------------- 4
    r.process("Assign a case to a volunteer", "Hand an unassigned case to the right person without leaving the board.", who="Staff", need="A card in the **New** column with a **👤 Assign** button.", time="2 minutes")
    newcol = col(page, "New")
    ncard = newcol.locator(".kanban-card", has=page.get_by_role("button", name=re.compile("Assign$"))).first
    abtn = ncard.get_by_role("button", name=re.compile("Assign"))
    r.step("Click Assign on a card",
           "In the **New** column, find a card that says *Unassigned* at the bottom. Click its green **👤 Assign** button.",
           target=abtn, action=lambda: (abtn.click(), page.wait_for_selector(".drawer", timeout=15000)), after=True,
           expect="A panel slides in from the right titled **Assign Case #…**.")
    drawer = page.locator(".drawer")
    sugg = drawer.locator("button").filter(has_text=re.compile(r"\(|active|case", re.I)).first
    r.step("Choose a volunteer",
           "Pick a volunteer one of two ways: (a) click one of the **🌟 Suggested (ranked by workload)** names — the person with the lightest load is first; or (b) open the **Assign To** box and choose anyone. People already at their chapter's limit are flagged.",
           target=drawer.locator("select").first, action=lambda: drawer.locator("select").first.select_option(index=1), when="after",
           expect="The volunteer's name shows in the **Assign To** box.")
    conf = drawer.get_by_role("button", name="Confirm Assignment")
    r.step("Click Confirm Assignment",
           "Click **Confirm Assignment**. To cancel instead, click **Cancel**.",
           target=conf, action=lambda: (conf.click(), page.wait_for_timeout(2500)), after=True,
           expect="The panel closes and the card jumps to the **Assigned** column. It now shows the volunteer's name and an **Unassign** button.")
    r.check(["The card is now in **Assigned**.", "The volunteer's name is on the card."])

    # ---------------------------------------------------------------- 5
    r.process("Move a card to its next stage by dragging", "Record that work has progressed.", who="Staff (volunteers use their own queue)", need="A card in the **Assigned** column.", time="2 minutes")
    acard = col(page, "Assigned").locator(".kanban-card").filter(has=page.get_by_role("button", name="Unassign")).first
    acard.scroll_into_view_if_needed()
    r.step("Pick up the card",
           "Point at any part of the card **except the button**. Press and **hold** the mouse button (or hold your finger down on a touchscreen).",
           target=acard, expect="Nothing visible changes yet.")
    target = col(page, "Volunteer Accepted")
    held = {}

    def shot_while_held():
        wide(page)
        held["png"] = True

    def do_drag():
        b = acard.bounding_box()
        t = target.locator(".kanban-col-hd").bounding_box()
        page.mouse.move(b["x"] + b["width"] / 2, b["y"] + 25)
        page.mouse.down()
        page.mouse.move(b["x"] + b["width"] / 2 + 8, b["y"] + 30, steps=4)
        page.mouse.move(t["x"] + t["width"] / 2, t["y"] + 60, steps=12)
        for k in range(6):   # keep dragover events flowing so the column highlight has time to paint
            page.mouse.move(t["x"] + t["width"] / 2 + (k % 2) * 4, t["y"] + 70, steps=2)
            page.wait_for_timeout(250)
    do_drag()
    r.step("Drag it onto the next column",
           "Keeping the button held, move the card sideways onto the column you want — here **Volunteer Accepted**. While you drag, the column you are over is **highlighted**, and any column that is **not** a valid move for this case is greyed out. Do not let go over a greyed column.",
           target=target.locator(".kanban-col-hd"), expect="The column under the card is highlighted; some other columns look dimmed.")
    page.mouse.up()
    page.wait_for_timeout(1800)
    r.step("Let go of the mouse button",
           "Release the mouse button over the highlighted column. The card drops into place and the case's **Status** and **Process Stage** are both updated together.",
           target=col(page, "Volunteer Accepted").locator(".kanban-card").first, after=False,
           expect="The card is now in **Volunteer Accepted**, and the column counts have changed by one each.")
    r.check(["The card sits in the new column.", "No yellow warning appears above the board."])

    # ---------------------------------------------------------------- 6
    r.process("What happens when a move is not allowed", "Understand the refusal message and what to do.", who="Staff", need="You are on the board.", time="2 minutes")
    ncard2 = col(page, "New").locator(".kanban-card").first
    ncard2.scroll_into_view_if_needed()
    ful = col(page, "Fulfilled")
    b = ncard2.bounding_box()
    t = ful.locator(".kanban-col-hd").bounding_box()
    r.step("Try to jump a card too far",
           "Cases cannot skip steps. For example, a brand-new case cannot go straight to **Fulfilled**. If you try — pick up a **New** card and drop it on **Fulfilled** —",
           target=ncard2, expect="You are holding a card from the New column.")
    page.mouse.move(b["x"] + b["width"] / 2, b["y"] + 25)
    page.mouse.down()
    page.mouse.move(b["x"] + b["width"] / 2 + 8, b["y"] + 30, steps=4)
    page.mouse.move(t["x"] + t["width"] / 2, t["y"] + 60, steps=12)
    page.wait_for_timeout(500)
    page.mouse.up()
    page.wait_for_timeout(1500)
    page.evaluate("window.scrollTo(0,0)")
    msg = page.locator("text=Can't move a case")
    r.step("Read the yellow message",
           "— the card does **not** move and a yellow message appears above the board, for example *Can't move a case from New directly to Fulfilled.* Nothing was changed. Assign the case and move it forward one column at a time instead.",
           target=msg if msg.count() else None,
           expect="A yellow warning bar: ⚠ Can't move a case from … directly to …. The card is still in its original column.")
    r.note("A case also needs a **tracking number** before it can be marked **Shipped**. If you see *needs a tracking number*, open the card (Process 7) and add one.", "Tracking numbers")
    r.check(["You can explain why the card did not move.", "You know the fix: move one step at a time."])

    # ---------------------------------------------------------------- 7
    r.process("Find cases with missing family information", "Spot records that need fixing before a package ships.", who="Staff", need="You are on the board.", time="2 minutes")
    ni = page.locator("#needs-info-filter")
    r.step("Click “⚠ Needs info”",
           "At the top right, click **⚠ Needs info**. The number in brackets is how many cases have a problem.",
           target=ni, action=lambda: (ni.click(), page.wait_for_timeout(1200)), after=True,
           expect="The board shows only the cards with a **⚠ Check family info** badge. The button turns green.")
    bd = page.locator("[data-needs-info]").first
    r.step("Read what is wrong",
           "Move your mouse over the **⚠ Check family info** badge on a card. A small note says what is missing or inconsistent, such as a missing ZIP. Open the case and correct it.",
           target=bd, expect="A badge on the card. Hovering shows the reason.")
    r.step("Click Needs info again to see everything",
           "Click **⚠ Needs info** a second time to go back to all cases.",
           target=ni, action=lambda: (ni.click(), page.wait_for_timeout(1200)),
           expect="All columns are full again.")
    r.check(["The button is no longer green.", "All cards are visible again."])

    # ---------------------------------------------------------------- 8
    r.process("Open a case's full page", "See and edit every detail of a case.", who="Staff", need="Any card.", time="1 minute")
    c2 = col(page, "Volunteer Accepted").locator(".kanban-card").first
    c2.scroll_into_view_if_needed()
    r.step("Click the card's body",
           "Click the **name or reason** on the card — anywhere except the **Assign / Unassign** button.",
           target=c2, action=lambda: (c2.locator(".kanban-card-name").click(), page.wait_for_url(re.compile(r".*/admin/cases/\d+"), timeout=20000), page.wait_for_timeout(1500)),
           after=True, expect="The case's own page opens: status, priority, assigned volunteer, family details, notes and activity.")
    r.check(["You can see the heading **Case #… — family name**."])
    r.p("Go to **Staff Guide: Managing Cases** for everything on that page.")

    r.h2("If something looks wrong")
    r.bullets([
        "**A card will not drag.** Make sure you press on the card itself, not its button, and keep the mouse button down until you reach the column.",
        "**The card snapped back.** The move was not allowed. Read the yellow message above the board.",
        "**I cannot see Fulfilled cases from last year.** The Fulfilled column shows the last 3 months only. Use **List View** or **Historical** for older ones.",
        "**A column is empty.** It shows the word *Empty*. That is normal.",
        "**Two people moved the same card.** The board updates live; the last change wins. Check the case's **Activity Log**.",
    ])
    r.write("07-Staff-Guide-Package-Pipeline-Board")
    return r


if __name__ == "__main__":
    session(None, build)
