"""Manual 03 — Volunteer Guide: Packing & Shipping a Package. Persona: Pat Packer (Volunteer with the Package Assembler role, set up by a coordinator)."""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lib import *  # noqa: E402

SLUG = "03-pack"


def line(page, label):
    """The whole 'Label: value' line, not just the bold label."""
    return page.locator(f'div:has(> strong:text-is("{label}"))').first
EMAIL = "pat.packer@example.org"


def build(page):
    r = Recorder(SLUG, "Volunteer Guide: Packing & Shipping a Package", page)
    r.h1("Volunteer Guide: Packing & Shipping a Package")
    r.p("**Who this is for:** Package Assemblers — volunteers who pack a comfort package and send it to a family. "
        "No computer skill is needed beyond using a web browser. Every click is shown with a picture; the thing to click "
        "is circled in red and numbered to match the step.")
    r.p("**When you finish you will be able to:** sign in, open your work queue, read your queue of cases, find the "
        "shipping address, tick off the packing list, record a tracking number, and mark a case Shipped and then Fulfilled.")
    r.note("The families in the pictures are practice (sample) data, not real people. Your screen will show your own cases.", "Practice data")

    # off-screen: a coordinator sets Pat up as a Package Assembler (staff add the role to the volunteer record). Until then a
    # volunteer sees only the Prayer Dashboard.
    stok0 = api_token("chris.kremer")
    api("POST", "/api/v1/volunteers", stok0, json={
        "firstName": "Pat", "lastName": "Packer", "email": EMAIL, "role": "PackageAssembler", "status": "Active",
        "chapterId": 1, "joinedDate": "2026-01-05T00:00:00Z"}).raise_for_status()

    # ------------------------------------------------------------------ 1
    r.process("Sign in", "Get into the LOTV portal.", who="Any volunteer with an account",
              need="The username and password your coordinator gave you. Your coordinator must also have made you a **Package Assembler** — until they do, you only see the Prayer Dashboard.", time="1 minute")
    r.signin(EMAIL, "You land on the **Prayer Dashboard** — a list of families who need prayer. Your packing work is one click away, in the left menu.",
             who="the username your coordinator gave you")
    r.check(["Your name is in the top-right corner.", "The left menu shows **Prayer Dashboard** and **My Work Queue**."])

    # ------------------------------------------------------------------ 2
    r.process("Open your work queue",
              "Go to the page that lists the boxes you are packing.",
              who="Package Assemblers", need="You are signed in (Process 1).", time="1 minute")
    wq = page.locator('a:has-text("My Work Queue")').first
    r.step("Click “My Work Queue”",
           "In the menu on the left, click **My Work Queue**.",
           target=wq, action=lambda: (wq.click(), page.wait_for_selector("text=No cases assigned to you", timeout=20000)), after=True,
           expect="A page headed **My Work Queue** with four count boxes. Under them it says **No cases assigned to you.** That is correct if a coordinator has not given you a case yet.",
           trouble="If **My Work Queue** is not in your menu, your coordinator has not made you a Package Assembler yet. Tell them — they add the role to your volunteer record.")
    r.note("Your coordinator assigns cases to you. When they do, the case appears here by itself, or after you press **↻ Refresh** (top right). Keep this page open and carry on with Process 3 once a case arrives.", "Waiting for a case")
    ids = give_cases(EMAIL, 3)   # off-screen: staff assigns cases to Pat
    assert 37 in ids, ids
    stok = api_token("chris.kremer")   # off-screen: the coordinator lists what goes in the box
    for rid in (1, 2, 3):
        api("POST", "/api/v1/requests/37/items", stok, json={"resourceItemId": rid, "quantity": 1}).raise_for_status()
    refresh = page.locator('button:has-text("Refresh")').first
    r.step("Press Refresh to see your new cases",
           "Click **↻ Refresh** in the top-right corner of the page.",
           target=refresh, action=lambda: refresh.click(), after=True,
           expect="A table of cases appears. Each row is one family you are packing a box for.")
    r.check(["The **No cases assigned to you** message is gone.", "You see a table listing at least one case."])

    # ------------------------------------------------------------------ 3
    r.process("Read your queue", "Understand the four count boxes, the filter buttons and the table.",
              who="Package Assemblers", need="You have at least one case (Process 2).", time="2 minutes")
    tiles = page.get_by_text(re.compile(r"^My open cases$", re.I)).first.locator("xpath=ancestor::div[2]")
    r.step("Look at the four count boxes",
           "Across the top are four boxes: **My Open Cases** (cases you still need to finish), **My Overdue** (cases open more than 7 days), **Fulfilled (All Time)** (boxes you have completed, ever) and **Avg Age (Open)** (how long your open cases have been waiting).",
           target=tiles, expect="Four white boxes with numbers. If **My Overdue** is above 0, do those cases first.")
    chips = page.locator('button:has-text("In Progress")').first
    r.step("Use the filter buttons to narrow the list",
           "Under the boxes are filter buttons: **All**, **New**, **In Progress**, **Awaiting Shipment**, **Overdue**. Click one to show only those cases. Click **All** to see everything again.",
           target=chips, action=lambda: (chips.click(), page.wait_for_timeout(800)), after=True,
           expect="The table now lists only cases in the status you chose; the chosen button turns green.")
    page.locator('button:has-text("All")').first.click()
    settle(page, 600)
    first_row = page.locator("tbody tr").first
    r.step("Read a row of the table",
           "Each row shows: the case number (**#**), the **Family** name, the **Reason** they asked for help, **Priority**, **Status**, the date it was **Created** and its **Age**. At the far right are two buttons: **Details** and **Quick Update**.",
           target=first_row, expect="One row per case, with **Details** and **Quick Update** at the right-hand end.")
    r.check(["You can say what each of the four boxes counts.", "You can filter the list and get back to **All**."])

    # ------------------------------------------------------------------ 4
    r.process("Open a case and find the shipping address", "See everything you need to pack and mail the box.",
              who="Package Assemblers", need="A case in your queue.", time="2 minutes")
    row37 = page.locator("tbody tr", has_text="#37")
    det = row37.get_by_role("link", name="Details")
    r.step("Click Details on the case",
           "Find the case you are working on. Click **Details** at the right-hand end of its row. (**Quick Update** is a shortcut for changing status only — it does not show the address.)",
           target=det, action=lambda: (det.click(), page.wait_for_url(re.compile(r".*/admin/cases/\d+"), timeout=20000)),
           after=True, expect="A page headed **Case #37 — family name**. Left side: case information. Right side: the **Family** box with the shipping address.")
    fam = line(page, "Shipping Address:")
    r.step("Find the shipping address",
           "Look at the **Family** box on the right. **Shipping Address** is where the box goes. Copy it exactly onto the label — check the apartment number and ZIP.",
           target=fam, expect="A street address, city, state and ZIP after **Shipping Address**. If it shows only a dash (—), the address is missing: do not ship — tell your coordinator.")
    story = page.locator("[data-field=story]")
    r.step("Read the family's story and any special notes",
           "On the left, scroll down to **Notes Thread**. The first box, **Story from the request form**, is what the family chose to share. Read it before you choose items or write a card. Treat everything here as private — never post it or share it outside the ministry.",
           target=story, expect="A green box with the family's own words, or *No story was shared with this request.*")
    r.step("Check the children's initials for the bracelet",
           "Scroll back up to **Request Information**. **Bracelet / children's initials** tells you whose initials go on the personalized bracelet. A dash (—) means no bracelet was requested.",
           target=line(page, "Bracelet / children's initials:"),
           expect="Letters such as *B.M.* — or a dash if there is no bracelet.")
    r.check(["You have the full shipping address.", "You know whether a bracelet is needed, and the initials."])

    # ------------------------------------------------------------------ 5
    r.process("Pack the box and tick the packing list", "Record what went into the box.",
              who="Package Assemblers", need="You are on the case's Details page (Process 4). Your coordinator has listed the items for this box.", time="5 minutes")
    r.step("Find the Packing List",
           "Scroll down the right-hand column to **Packing List**. Your coordinator puts the items for each box here. The small counter at the top right of the panel shows how many are packed, for example *0 / 3 packed*.",
           target=page.locator("div.panel").filter(has=page.get_by_text("Packing List", exact=True)).first,
           expect="A list of items such as a memory box, a blanket and a book, each with an empty tick-box.",
           trouble="The list is empty? Your coordinator has not added items yet. Message them — you cannot add items yourself.")
    r.note("The **— Select item —** box and **Add** button under the list are for coordinators. They may look empty or do nothing for you. That is normal — you only tick and, if needed, report problems.", "Good to know")
    boxes = page.locator("div.panel").filter(has=page.get_by_text("Packing List", exact=True)).locator("input[type=checkbox]")
    for i, word in enumerate(["first", "second", "third"]):
        cb = boxes.nth(i)
        r.step(f"Tick the {word} item once it is in the box",
               f"Put the {word} item in the box, then click the small tick-box to its left." + (" Do the same for every other item." if i == 0 else ""),
               target=cb, action=lambda c=cb: (c.check(), page.wait_for_timeout(1500)),
               expect=f"The item name is crossed out and the counter reads *{i + 1} / 3 packed*." if i < 2 else "Every item is crossed out and the counter reads *3 / 3 packed*.",
               trouble="Ticked the wrong one? Click the box again to untick it." if i == 0 else None)
    r.note("The **Quick Actions** box on this page also has **Unassign**, **Put On Hold** and **Cancel Case**. Those are coordinator decisions — if you cannot finish a case, add a note and tell your coordinator instead of using them.", "Do not use")
    r.check(["Every item in the list is crossed out.", "The counter shows all items packed, for example *3 / 3 packed*."])
    r.note("If an item is out of stock, damaged, or you want to add something, do not guess. Write what happened in the **Notes Thread** (type in the box, click **Add Note**) and tell your coordinator before you ship.", "Something missing?")

    # ------------------------------------------------------------------ 6
    r.process("Mark the box ready to ship, with its tracking number",
              "Tell the ministry the box is packed and record the tracking number.",
              who="Package Assemblers", need="The box is packed and you have a carrier tracking number.", time="2 minutes")
    r.step("Go back to My Work Queue",
           "Click **My Work Queue** in the left-hand menu.",
           target=page.locator('a:has-text("My Work Queue")').first,
           action=lambda: (page.locator('a:has-text("My Work Queue")').first.click(), page.wait_for_selector("tbody tr", timeout=20000)),
           expect="Your table of cases.")
    qu = page.locator("tbody tr", has_text="#37").get_by_role("button", name="Quick Update")
    r.step("Click Quick Update on the case",
           "Find the case in the table and click **Quick Update**.",
           target=qu, action=lambda: (qu.click(), page.wait_for_selector("text=Add Note", timeout=15000)), after=True,
           expect="A panel slides in from the right headed **Case #37 — family name**. It shows the family's contact details and a **Status** box.")
    drawer = page.locator('div[style*="z-index:501"]')
    status = drawer.locator("select").first
    r.step("Open the Status box",
           "Click the **Status** box. It lists only the statuses this case is allowed to move to next. A case can never skip ahead: you will see New, AwaitingShipment, Fulfilled, OnHold and Cancelled here, and Shipped only after AwaitingShipment.",
           target=status, action=lambda: show_options(status),
           expect="A short list of choices, all under *InProgress (current)*.")
    hide_options(status)
    r.step("Choose AwaitingShipment",
           "Click **AwaitingShipment**. This means: packed and ready for the carrier.",
           target=status, action=lambda: status.select_option(label="AwaitingShipment"), when="after",
           expect="Two new boxes appear under Status: **Tracking Number** and **Shipped Date**.")
    track = drawer.locator('input[placeholder="1Z999AA10123456784"]')
    r.step("Type the tracking number",
           "Click **Tracking Number** and type the number from the carrier's receipt, with no spaces. Example: 1Z999AA10123456784.",
           target=track, action=lambda: track.fill("1Z999AA10123456784"), when="after",
           expect="The number is in the box exactly as printed on the receipt.")
    sdate = drawer.locator("input[type=date]")
    r.step("Choose the Shipped Date",
           "Click **Shipped Date** and pick the day you handed the box to the carrier (or type it as month/day/year).",
           target=sdate, action=lambda: sdate.fill("2026-10-08"), when="after",
           expect="A date in the box, for example 10/08/2026.")
    save = drawer.get_by_role("button", name="Save", exact=True)
    r.step("Click Save",
           "Click the green **Save** button.",
           target=save, action=lambda: (save.click(), page.wait_for_selector("text=Case updated successfully", timeout=20000)),
           after=True, expect="A green message: **✓ Case updated successfully.**",
           trouble="A yellow warning instead? Read it — it says what is missing, such as the tracking number or an item that is not packed. Fix that and click Save again.")
    close = drawer.get_by_role("button", name="Close")
    r.step("Close the panel and check the status",
           "Click **Close**. The case's **Status** badge in the table now says **AwaitingShipment**.",
           target=close, action=lambda: (close.click(), page.wait_for_timeout(1000)), after=True,
           expect="The row for the case shows an **AwaitingShipment** badge.")
    r.check(["The case's status says **AwaitingShipment**.", "The tracking number is saved on the case (open **Details** and look under *Request Information*, *Tracking*)."])

    # ------------------------------------------------------------------ 7 & 8
    for n, (proc_name, goal, to_status, why, nxt) in enumerate([
        ("Mark the box as Shipped", "Record that the carrier has picked the box up.", "Shipped",
         "Do this once the carrier has the box in hand.", "A **Shipped** badge on the row."),
        ("Mark the case Fulfilled", "Close the case once the family has received the package.", "Fulfilled",
         "Do this when delivery is confirmed (carrier tracking shows delivered, or the family tells your coordinator).",
         "The case disappears from your list — finished cases leave your queue — and **My Open Cases** goes down by one."),
    ]):
        r.process(proc_name, goal, who="Package Assemblers", need=why, time="1 minute")
        qu = page.locator("tbody tr", has_text="#37").get_by_role("button", name="Quick Update")
        r.step("Click Quick Update", "In **My Work Queue**, find the case and click **Quick Update**.",
               target=qu, action=lambda q=qu: (q.click(), page.wait_for_selector("text=Add Note", timeout=15000)),
               expect="The panel opens on the right.")
        drawer = page.locator('div[style*="z-index:501"]')
        status = drawer.locator("select").first
        r.step(f"Choose {to_status}", f"Click the **Status** box and choose **{to_status}**.",
               target=status, action=lambda s=status, t=to_status: s.select_option(label=t), when="after",
               expect=f"The Status box shows {to_status}.")
        save = drawer.get_by_role("button", name="Save", exact=True)
        r.step("Click Save", "Click **Save**. The tracking number and date from before stay on the case — you do not need to type them again unless the screen shows empty boxes.",
               target=save, action=lambda s=save: (s.click(), page.wait_for_selector("text=Case updated successfully", timeout=20000)),
               after=True, expect="**✓ Case updated successfully.**")
        close = drawer.get_by_role("button", name="Close")
        r.step("Close the panel", "Click **Close**.",
               target=close, action=lambda c=close: (c.click(), page.wait_for_timeout(1000)),
               expect=nxt)
        r.check([f"The case status is **{to_status}**."])

    r.h2("If something looks wrong")
    r.bullets([
        "**Nothing is in my queue.** A coordinator has not assigned you a case yet, or has not made you a Package Assembler. Press **↻ Refresh**; if it is still empty, tell your coordinator.",
        "**I cannot take a new case.** Each chapter limits how many active cases one volunteer can carry (6 unless your chapter changed it). Finish and mark an existing case first.",
        "**The Status box does not offer the status I want.** Only the next allowed step is offered. For example, a case must be **AwaitingShipment** before it can be **Shipped**.",
        "**The shipping address is missing or looks wrong.** Do not ship. Write a note on the case (Notes Thread → type → **Add Note**) and tell your coordinator.",
        "**I saved the wrong status.** Tell your coordinator straight away; staff can correct it.",
    ])
    r.write("03-Volunteer-Guide-Packing-and-Shipping")
    return r


if __name__ == "__main__":
    session(None, build)
