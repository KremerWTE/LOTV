"""Manual 01 — Prayer Care Package Dashboard Manual (the hub). Personas: a family (public), Claire Hoffman (staff), Pat Packer (volunteer)."""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lib import *  # noqa: E402
from playwright.sync_api import sync_playwright  # noqa: E402

SLUG = "01-hub"


def field(page, label):
    return page.get_by_label(re.compile(rf"^{re.escape(label)}"), exact=False).first


def build(page, browser):
    r = Recorder(SLUG, "Prayer Care Package Dashboard — Staff & Volunteer Manual", page)
    r.h1("Prayer Care Package Dashboard — Staff & Volunteer Manual")
    r.p("**Start here.** This is the main guide. It explains who uses the dashboard, which of the seven short guides you need, how a request travels from the public form to a delivered package, "
        "and walks through the two things everybody does: signing in and submitting a request.")
    r.note("The families and numbers in the pictures are practice (sample) data. Nothing here is a real person.", "Practice data")

    r.h2("What the dashboard is")
    r.p("Every request for a comfort package, for prayer, or both — made through the public request form — becomes a **case** on this dashboard automatically. "
        "The case is tracked from the moment a family asks for help until it is finished. Staff manage every case; volunteers see only their own work.")

    r.h2("Which guide do I need?")
    r.md += [
        "| If you are… | And you want to… | Read |",
        "|---|---|---|",
        "| A family, or helping one | Ask for a package or prayer | **This guide**, Process 2 |",
        "| Anyone | Sign in | **This guide**, Process 1 |",
        "| A volunteer who prays | Choose families to pray for | Guide 02 — Praying for a Family |",
        "| A volunteer who packs | Pack, record tracking, mark shipped | Guide 03 — Packing & Shipping a Package |",
        "| An administrator | Reset a password, sign in as someone | Guide 04 — Users, Temporary Passwords & Login As |",
        "| Staff | Work one case in detail | Guide 05 — Managing Cases |",
        "| A Board member | Read the ministry's numbers | Guide 06 — Board Member Guide |",
        "| Staff | See all cases as a board; drag between stages | Guide 07 — The Package Pipeline Board |",
        "| Staff | Use the Cases Hub, duplicates and the Unassigned Queue | Guide 08 — Cases Hub, Duplicates & Queues |",
        "",
    ]

    r.h2("Who can do what")
    r.p("What you see depends on your **staff role** or the **volunteer role(s)** on your record. One volunteer can hold more than one volunteer role.")
    r.md += [
        "| Role | Access |",
        "|---|---|",
        "| **HQAdmin** | Everything, in every chapter. The only role that can use **Login As** or open **System Admin**. |",
        "| **ChapterAdmin** | Full case, volunteer and donation access; can set temporary passwords; not System Admin or Login As. |",
        "| **ChapterStaff** | The same day-to-day case work as ChapterAdmin, without account management. |",
        "| **Director** | The same operational access as ChapterAdmin; never HQAdmin-only screens. |",
        "| **Board** | Read-only: can look at the staff screens and the Board Portal, never change anything. |",
        "| **Volunteer** | Only **My Work Queue** and the **Prayer Dashboard**, and only their own cases and families. |",
        "",
        "| Volunteer role | What it is for |",
        "|---|---|",
        "| **Package Assembler** | Packs and ships packages. Their cases appear in **My Work Queue**. |",
        "| **Prayer Ambassador** | Joins families' prayer teams. They use the **Prayer Dashboard**. |",
        "| Parish Liaison, Event Helper, Driver, Admin | Other ministry roles, not used by this dashboard. |",
        "",
    ]

    r.h2("The statuses a case goes through")
    r.md += [
        "| Status | Meaning |",
        "|---|---|",
        "| **New** | Just submitted; nobody assigned yet. |",
        "| **InProgress** | Assigned to a volunteer and being worked. |",
        "| **AwaitingShipment** | Packed and ready for the carrier. |",
        "| **Shipped** | With the carrier. A tracking number is required. |",
        "| **Fulfilled** | Delivered; the case is complete. |",
        "| **OnHold** | Paused for any reason; can resume. |",
        "| **Cancelled** | No longer happening. |",
        "",
    ]
    r.p("A case can only move to a status that makes sense from where it is, so you can never jump from New straight to Shipped. A **prayer-only** request skips the package statuses: there is nothing to pack, so it only ever waits on the prayer team.")

    r.h2("How a request travels")
    r.bullets([
        "**1. Submitted** — a family (or someone helping them) fills in the public form. A case is created; no staff step is needed.",
        "**2. Duplicate check** — the system compares the family with existing families in the same chapter (same email, same phone, or same last name and ZIP). A possible match is held for a person to decide. → *Guide 08*",
        "**3. Assigned** — the case waits in the **Unassigned Queue**, or is assigned automatically to a Package Assembler in the same chapter (within each volunteer's case limit, 6 by default). A volunteer who does not accept in 24 hours (4 if Urgent) loses it to the next person. → *Guides 05, 07, 08*",
        "**4. Packed and shipped** — the volunteer packs, enters a tracking number and marks it Shipped. → *Guide 03*",
        "**5. Prayed for** — any number of Prayer Ambassadors join the family's prayer team, separately from the packing. → *Guide 02*",
        "**6. Fulfilled** — the case is marked Fulfilled when the package arrives.",
    ])

    # ---------------------------------------------------------------- 1
    r.process("Sign in", "Get into the staff and volunteer portal.", who="Anyone with an account", need="Your username and password. An administrator creates your account and can give you a temporary password if you are locked out.", time="1 minute")
    r.signin("claire.hoffman", "You land on your home page. **Staff** land on the **Chapter Dashboard**. **Volunteers** land on **My Work Queue**.", who="your username")
    r.note("If you were given a **temporary password**, you are taken straight to a page called *Set your own password* and cannot use anything else until you choose one (see Guide 04). If you forget your password, click **Forgot password** under the password box, or ask an administrator.", "Temporary password")
    r.check(["Your name and role are in the top-right corner."])

    # ---------------------------------------------------------------- 2
    r.process("Submit a request on the public form", "Ask for a comfort package, prayer, or both. Anyone can do this — no account needed.", who="A family, or anyone helping a family", need="Your name, an email address, a mailing address and the reason for the request. Phone numbers are optional.", time="5 minutes")
    ctx = browser.new_context(viewport=VIEWPORT, device_scale_factor=1)
    fp = ctx.new_page()
    fp.set_default_timeout(15000)
    staff_page, r.page = page, fp
    fp.goto(f"{WEB}/request-prayer-care-package")
    settle(fp, 3000)
    r.step("Open the request form",
           "In your web browser, go to the ministry's request page, **/request-prayer-care-package** on the ministry's website (for example `https://<ministry-website>/request-prayer-care-package`).",
           expect="A form with the line *To request a Prayer Care Package be mailed to you or a family who needs support, please complete the form below.*")
    forme = fp.get_by_text("This is for me").first
    r.step("Say who it is for",
           "Under **Who is this for?** click **This is for me** if you are asking for your own family, or **This is for someone else** if you are asking for another family.",
           target=forme, action=lambda: (forme.click(), fp.wait_for_timeout(1200)), after=True,
           expect="Your choice turns green and the rest of the form opens.")
    helpsel = fp.get_by_label(re.compile("What would help most right now", re.I))
    r.step("Choose what would help most",
           "Open **What would help most right now?** There are three choices:\n\n"
           "- **A comfort package mailed to you, plus ongoing prayer** (the usual choice) — you get a package and are on the prayer team's list.\n"
           "- **Prayer only — no package needed** — nothing is mailed; the prayer team prays.\n"
           "- **A comfort package only — no prayer team needed** — you get a package, but the prayer team is not told.",
           target=helpsel, action=lambda: show_options(helpsel), expect="A list of the three choices.")
    hide_options(helpsel)
    helpsel.select_option(index=1)
    fp.wait_for_timeout(800)
    r.step("Pick your choice",
           "Pick the one you want. If you picked a package, a **Children for Bracelet** section may appear further down — it is only shown when a package was requested.",
           target=helpsel, expect="Your choice is shown in the box.")
    hn = field(fp, "Husband's Name")
    r.step("Fill in the names",
           "Under **About you**, type the first and last name of each parent. (If the form shows different labels, fill in the names it asks for.) Boxes with a red **\\*** are required.",
           target=[hn, field(fp, "Wife's Name")],
           action=lambda: (hn.fill("Luis Rivera"), field(fp, "Wife's Name").fill("Marta Rivera")), when="after", expect="Both names typed.")
    r.step("Fill in email addresses and phone",
           "Type an email address for each parent. Phone numbers are optional. We use these only to contact you about your request.",
           target=[field(fp, "Husband's Email"), field(fp, "Wife's Email")],
           action=lambda: (field(fp, "Husband's Email").fill("luis.rivera.demo@example.org"), field(fp, "Wife's Email").fill("marta.rivera.demo@example.org"), field(fp, "Husband's Phone").fill("312-555-0188")),
           when="after", expect="Email addresses typed; phone optional.")
    street = field(fp, "Street Address")
    r.step("Fill in the mailing address",
           "Type the street address, apartment or suite (if any), city, state and ZIP code where the package should go.",
           target=[street, field(fp, "Zip Code")],
           action=lambda: (street.fill("742 Maple Avenue"), field(fp, "City").fill("Evanston"), field(fp, "State").fill("IL"), field(fp, "Zip Code").fill("60201")),
           when="after", expect="A complete address.")
    reason = field(fp, "Reason for Prayer Request")
    r.step("Choose the reason",
           "Open **Reason for Prayer Request** and pick the closest: Infertility, Prenatal Diagnosis, Miscarriage (before 20 weeks), Stillbirth (20 weeks or later), Infant Loss (after birth, up to age 1) or Postnatal Medical Concern.",
           target=reason, action=lambda: show_options(reason), expect="A list of reasons.")
    hide_options(reason)
    reason.select_option(index=3)
    faith = fp.locator("#lotv-faith")
    r.step("Optional: faith, date of loss and parish",
           "Boxes without a red **\*** are optional: **Faith Tradition**, **Date of loss**, **Diocese**, **Parish** and whether you would like **quarterly grief support**. Fill in what helps us care for you; skip the rest.",
           target=faith, action=lambda: faith.select_option(label="Catholic"), when="after", expect="Optional boxes filled or left blank.")
    heard = fp.locator("#lotv-how-heard")
    r.step("Say how you heard about us",
           "Open **How did you hear about us?** (required) and pick the closest answer.",
           target=heard, action=lambda: heard.select_option(label="Friend"), when="after", expect="Your answer in the box.")
    story = fp.get_by_label(re.compile("share with us", re.I)).first
    r.step("Tell your story (optional)",
           "In the large box you may share, **as much as you are comfortable with**, what has happened. Staff and volunteers who care for your request read it. You can leave it blank.",
           target=story, action=lambda: story.fill("We are grateful for any prayers and support."), when="after", expect="Your words in the box.")
    child = fp.get_by_placeholder("e.g. E.M.")
    r.step("Bracelet: tell us about your children",
           "Because you asked for a package, the form asks about a **personalised bracelet**. Share the initials of your children in birth order, including children in heaven. In **Child Initial** type their initials; **Bead Type** is *Initials* (or *Heart* if a child was not named, or if you are experiencing infertility); **Living or Deceased** can be *Living*, *Deceased* or *In Heaven*. Click **+ Add Another Child** for more. **This section is required when you ask for a package.**",
           target=child, action=lambda: (child.fill("E.R."), fp.get_by_role("combobox").last.select_option(label="In Heaven")), when="after",
           expect="A row with order 1, the initials, bead type and status.",
           trouble="A yellow message at the top saying *Please fill in: at least one child for the bracelet* means this row is empty. Fill it in, or choose *Prayer only* above if you do not want a package.")
    sub = fp.get_by_role("button", name="Submit Request")
    r.step("Choose newsletter and prayer-night options (optional), then submit",
           "Two tick-boxes under **Opt-in communications** let you join the monthly newsletter and the monthly Prayer Night invitation. Both are optional. When you are ready, click **Submit Request**.",
           target=sub, action=lambda: (sub.click(), fp.wait_for_timeout(3500)), after=True,
           expect="A confirmation that your request was received.",
           trouble="If a red message appears beside a box, that box is required or has a problem (for example an email address missing its @). Fix it and click **Submit Request** again.")
    r.check(["You saw a confirmation message.", "Nobody needs to do anything else: the request is now a case on the dashboard."])
    r.page = staff_page
    ctx.close()

    # ---------------------------------------------------------------- 3
    r.process("See the new request on the dashboard", "Confirm the request became a case.", who="Staff", need="You are signed in as staff (Process 1).", time="2 minutes")
    cases = page.locator('a.sidebar-link:has-text("Cases")').first
    r.step("Click Cases in the left menu", "Click **Cases** in the green menu.",
           target=cases, action=lambda: (cases.click(), page.wait_for_selector("button.tab-btn", timeout=20000), page.wait_for_timeout(1500)), after=True,
           expect="The **Cases** page.")
    box = page.get_by_placeholder(re.compile("Search families", re.I)).last
    r.step("Search for the family", "Type the family's name in the **Search families…** box — here, *Rivera*.",
           target=box, action=lambda: (box.fill("Rivera"), page.wait_for_timeout(1200)), when="after",
           expect="A row for the new family with the status **New**.",
           trouble="Not there? It may be held as a **possible duplicate** — check **Possible Duplicates** in the menu (Guide 08).")
    r.check(["The new family appears with status **New**."])

    # ---------------------------------------------------------------- 4
    r.process("Find your way around", "Know what is in your menu.", who="Everyone", time="2 minutes")
    r.page = page
    page.goto(f"{WEB}/admin/dashboard")
    settle(page, 1500)
    sb = page.locator("aside, .sidebar").first
    r.step("Staff menu",
           "Staff see the green menu on the left. Under **Prayer Request Package** are: **Package Pipeline**, **Possible Duplicates**, **Cases**, **Unassigned Queue**, **My Work Queue**, **Request Form**. Further down are the other areas your role may use (families, donations, volunteers, reports…).",
           target=sb, expect="A long green menu.")
    vctx = browser.new_context(viewport=VIEWPORT, device_scale_factor=1)
    vp = vctx.new_page()
    vp.set_default_timeout(15000)
    login(vp, "pat.packer@example.org")
    r.page = vp
    sbv = vp.locator("aside, .sidebar").first
    r.step("Volunteer menu",
           "Volunteers see a short menu: **My Work Queue** (boxes to pack), **Prayer Dashboard** (families to pray for) and the **Request Form** link. Nothing else — volunteers only see their own work.",
           target=sbv, expect="A short green menu with two or three links.")
    vctx.close()
    r.page = page
    r.check(["You can name the links in your own menu."])

    r.h2("If something looks wrong")
    r.bullets([
        "**The bracelet question is not showing on the form.** It only appears when a package was chosen; it is hidden for *Prayer only*.",
        "**A case is not in the Prayer Dashboard's list.** It was submitted as *comfort package only* — the family chose not to involve the prayer team.",
        "**I cannot move a case to the status I expect.** Only the next allowed step is offered. See the status table above and Guide 05.",
        "**A volunteer cannot take another case.** They may be at their chapter's limit (6 by default).",
        "**I forgot my password.** Use **Forgot password** on the sign-in page, or ask an HQAdmin, ChapterAdmin or Director for a temporary password (Guide 04).",
        "**I need to see exactly what someone else sees.** An HQAdmin can use **Login As** (Guide 04).",
    ])
    r.write("01-Prayer-Care-Package-Dashboard-Manual")
    return r


if __name__ == "__main__":
    with sync_playwright() as p:
        b = p.chromium.launch()
        ctx = b.new_context(viewport=VIEWPORT, device_scale_factor=1)
        pg = ctx.new_page()
        pg.set_default_timeout(15000)
        try:
            build(pg, b)
        except Exception:
            pg.screenshot(path=str(ROOT / "tools" / "manuals" / "_fail.png"))
            raise
        finally:
            b.close()
