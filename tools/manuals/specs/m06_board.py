"""Manual 06 — Board Member Guide. Persona: Bea Board (Board role, read-only)."""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lib import *  # noqa: E402

SLUG = "06-board"
EMAIL = "bea.board@example.org"


def build(page):
    r = Recorder(SLUG, "Board Member Guide", page)
    r.h1("Board Member Guide")
    r.p("**Who this is for:** Board members who want to check how the ministry is doing. You can look at everything in this guide. "
        "You can **not** change anything: every save, edit or create action is refused by the system itself, not merely hidden, so nothing you click can alter ministry records.")
    r.p("**When you finish you will be able to:** sign in, open the Board Portal summary, read each number on it, browse individual cases for more detail, and sign out.")
    r.note("The families and amounts in the pictures are practice (sample) data.", "Practice data")

    # ---------------------------------------------------------------- 1
    r.process("Sign in", "Get into the portal.", who="Board members", need="The username and password an administrator gave you.", time="1 minute")
    r.signin(EMAIL, "You land on the staff area. The left menu is short: **Package Workflow**, **Possible Duplicates**, **Cases**, **Unassigned Queue**, **My Work Queue** and **Request Form**.",
             who="the username you were given")
    r.note("You land in the staff area, but the summary page made for the Board is separate and is not in this menu. Process 2 shows how to reach it. Bookmark it once you have.", "Where is the Board Portal?")
    r.check(["Your name and the word **Board** are in the top-right corner."])

    # ---------------------------------------------------------------- 2
    r.process("Open the Board Portal", "Reach the one-page summary of the ministry's numbers.", who="Board members", need="You are signed in.", time="1 minute")
    r.step("Go to the Board Portal address",
           "Click in your browser's address bar (the long box at the very top of the browser window), delete what is there, and type the portal's web address followed by **/board/portal** — for example `https://<your-portal-address>/board/portal` — then press **Enter**. Save it as a bookmark so next time is one click.",
           action=lambda: page.goto(f"{WEB}/board/portal"), after=True, no_shot=False,
           expect="A page titled **Board Portal** with the line *High-level ministry performance — read-only governance summary*.",
           trouble="If you are taken to the sign-in page, sign in again and retype the address.")
    r.check(["The page heading says **Board Portal**."])

    # ---------------------------------------------------------------- 3
    r.process("Read the Board Portal", "Know what each number means.", who="Board members", need="You are on the Board Portal (Process 2).", time="5 minutes")
    r.step("Read the top row of numbers",
           "The seven boxes at the top are the ministry at a glance:\n\n"
           "- **Total Donations** — all money received.\n- **Donations This Month** — money received this calendar month.\n"
           "- **Families Served** — families who have received help.\n- **Open Cases** — requests still being worked on.\n"
           "- **Fulfilled Cases** — requests completed.\n- **Active Volunteers** — volunteers currently active.\n- **Dioceses Reached** — dioceses with at least one family served.",
           target=page.locator("text=Total Donations").first.locator("xpath=ancestor::div[3]"),
           expect="Seven boxes, each with one number.")
    mf = page.get_by_text("Money Flow by Category").first
    r.step("Read Money Flow by Category",
           "Scroll down a little. **Money Flow by Category** shows where donations were directed. Each line has a bar and, at its right, the dollar amount and its share of the total.",
           target=mf, expect="Lines such as *General Operating Fund* with a gold bar and an amount and percentage.")
    rd = page.get_by_text("Resource Distribution").first
    r.step("Read Resource Distribution",
           "Next to it, **Resource Distribution** shows what has gone out in packages — books, blankets, memory boxes and so on — as unit counts and percentages.",
           target=rd, expect="Green bars for each type of item with a unit count.")
    page.evaluate("window.scrollTo(0, document.body.scrollHeight)")
    tr = page.get_by_text("12-Month Trend").first
    r.step("Read the 12-Month Trend",
           "Scroll to the bottom. **12-Month Trend** is a table: one row per month, with donations received and cases fulfilled. Use it to spot busy and quiet months.",
           target=tr, expect="A table with the columns Month, Donations and Cases fulfilled, one row for each of the last twelve months.")
    r.check(["You can say what each of the seven top numbers counts."])

    # ---------------------------------------------------------------- 4
    r.process("Look at individual cases", "See more detail than the summary gives — safely, read-only.", who="Board members", need="You are signed in.", time="5 minutes")
    page.goto(f"{WEB}/admin/dashboard")
    settle(page, 1500)
    cases = page.locator('a.sidebar-link:has-text("Cases")').first
    r.step("Click Cases in the left menu",
           "Click **Cases** in the green menu on the left. If you are on the Board Portal, use the **Staff Login** button at the top right or retype the address ending in **/admin/dashboard** first.",
           target=cases, action=lambda: (cases.click(), page.wait_for_selector("text=Family Cases", timeout=20000)), after=True,
           expect="A page headed **Cases** with a row of tabs and a table of every case.")
    det = page.get_by_role("link", name="Details").first
    r.step("Open a case",
           "Click **Details** at the right-hand end of any row.",
           target=det, action=lambda: (det.click(), page.wait_for_url(re.compile(r".*/admin/cases/\d+"), timeout=20000), page.wait_for_timeout(1500)), after=True,
           expect="The full page for one case: status, family information, notes and an activity log.")
    page.evaluate("window.scrollTo(0, 0)")
    r.step("Read what is on the page",
           "On the left: **Case Details**, **Request Information**, **Notes Thread** and **Activity Log**. On the right: the **Family** box with contact details and the case's **Packing List**. Read as much as you need. Treat everything here as private — never share family details outside the board.",
           target=page.locator("text=Request Information").first, expect="Panels of information about this family and case.")
    qa = page.locator("div.panel", has_text="Quick Actions").get_by_role("button").first
    r.step("Know that changes are refused",
           "You may see buttons such as **Mark InProgress**. If you click one, the system **refuses** it and nothing changes: Board access cannot alter anything. You do not need to click them — this is shown only so you are not surprised if you do.",
           target=qa, action=lambda: (qa.click(), page.wait_for_timeout(2000), page.evaluate("window.scrollTo(0,0)")), after=True,
           expect="The case's status badge at the top is unchanged. (Check it is the same as before you clicked.)")
    r.check(["You opened a case and read it.", "The case's status did not change."])

    # ---------------------------------------------------------------- 5
    r.process("Sign out", "Leave the portal safely, especially on a shared computer.", who="Board members", time="30 seconds")
    page.goto(f"{WEB}/board/portal")
    settle(page, 1500)
    so = page.get_by_role("button", name="Sign Out")
    r.step("Click Sign Out",
           "On the Board Portal, click **Sign Out** at the top right.",
           target=so, action=lambda: (so.click(), page.wait_for_timeout(2500)), after=True,
           expect="You are returned to a public page and are no longer signed in.")
    r.check(["Going back to the portal asks you to sign in again."])

    r.h2("What you cannot do")
    r.bullets([
        "Save, edit, create, assign or delete anything. These are refused by the system.",
        "Use HQ-only tools such as **System Admin**. They are not in your menu; if you type their addresses you may be able to view some lists, but you cannot change anything.",
        "See other people's passwords. Nobody can, including administrators.",
    ])
    r.h2("If something looks wrong")
    r.bullets([
        "**I cannot find the Board Portal.** It is not in the menu. Type its address (Process 2) or use your bookmark.",
        "**The numbers look out of date.** Press your browser's refresh button (F5).",
        "**I need a change made.** Ask a staff member — you cannot make it yourself.",
    ])
    r.write("06-Board-Member-Guide")
    return r


if __name__ == "__main__":
    session(None, build)
