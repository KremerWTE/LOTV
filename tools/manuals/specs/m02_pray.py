"""Manual 02 — Volunteer Guide: Praying for a Family. Persona: Penny Prayer (Volunteer, no volunteer record yet)."""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lib import *  # noqa: E402

SLUG = "02-pray"


def build(page):
    r = Recorder(SLUG, "Volunteer Guide: Praying for a Family", page)
    r.h1("Volunteer Guide: Praying for a Family")
    r.p("**Who this is for:** volunteers who want to pray for the families who have asked for help. "
        "You do not need any computer skill beyond using a web browser. Every click is shown below with a picture; "
        "the item to click is circled in red and numbered to match the step.")
    r.p("**What you will be able to do when you finish:** sign in, set yourself up as a Prayer Ambassador, choose "
        "families to pray for, see who you are praying for, and stop praying for a family whenever you like.")
    r.note("Everything in this guide uses the pictures you will really see. Names of families in the pictures are practice (sample) data, not real people.", "Practice data")

    # ---------------------------------------------------------------- Process 1
    r.process("Sign in", "Get into the LOTV portal.", who="Any volunteer with an account",
              need="Your username and password from your coordinator (an administrator creates your account).", time="1 minute")
    r.signin("penny.prayer@example.org",
             "You land on **My Work Queue** (a page with the heading *My Work Queue*). Do not worry if it says you have no cases — that page is for people who pack boxes. The next process takes you to the prayer page.",
             who="the username your coordinator gave you")
    r.check(["You can see your own name in the top-right corner.", "The left menu shows **My Work Queue** and **Prayer Dashboard**."])

    # ---------------------------------------------------------------- Process 2
    r.process("Set yourself up as a Prayer Ambassador (first time only)",
              "Create your volunteer record so you can join families' prayer teams.",
              who="Any signed-in volunteer", need="You are signed in (Process 1).", time="1 minute")
    r.step("Open the Prayer Dashboard",
           "Look at the menu on the left side of the screen. Click **Prayer Dashboard**.",
           target=page.locator('a:has-text("Prayer Dashboard")'),
           action=lambda: page.click('a:has-text("Prayer Dashboard")'), after=True,
           expect="A page titled **Prayer Dashboard** saying *You aren't set up as a volunteer yet.*")
    r.step("Click the green button to create your record",
           "Click **Create my volunteer record as a Prayer Ambassador**. You only ever do this once.",
           target=page.locator('button:has-text("Create my volunteer record as a Prayer Ambassador")'),
           action=lambda: (page.click('button:has-text("Create my volunteer record as a Prayer Ambassador")'),
                           page.wait_for_selector('text=Families You Could Pray For', timeout=20000)),
           expect="The page reloads and now shows two tiles — **Praying For** and **Families You Could Join** — and a list headed **Families You Could Pray For**.",
           trouble="If nothing changes after a few seconds, press the **↻ Refresh** button at the top right. If you still see *You aren't set up as a volunteer yet*, tell your coordinator — your account may be missing its volunteer permission.")
    tiles = [page.get_by_text(re.compile(r"^Praying For$", re.I)).first.locator("xpath=ancestor::div[1]"),
             page.get_by_text(re.compile(r"^Families You Could Join$", re.I)).first.locator("xpath=ancestor::div[1]")]
    r.step("Check the page",
           "Look at the two tiles at the top. **Praying For** is how many families you are on. **Families You Could Join** is how many you can still choose. You are brand new, so Praying For is 0.",
           target=tiles, shot="viewport",
           expect="Praying For: 0. Below it, a message: *You haven't added yourself to any family's prayer team yet — pick one below.*")
    r.check(["The page shows a **Praying For** tile and a **Families You Could Join** tile.", "You see a list headed **Families You Could Pray For**."])

    # ---------------------------------------------------------------- Process 3
    r.process("Choose a family to pray for", "Add yourself to a family's prayer team.",
              who="Prayer Ambassadors", need="You have finished Process 2.", time="1 minute")
    first = page.locator('button:has-text("I\'ll pray for this family")').first
    r.step("Find a family",
           "Scroll down to **Families You Could Pray For**. Each white card is one family. It shows the family's name, the reason they asked for prayer, and the date they asked. Pick any family — there is no wrong choice and nobody has to approve it.",
           target=first, shot="viewport",
           expect="A list of white cards, each with a green **I'll pray for this family** button on the right.",
           trouble="A family is missing from the list? A request marked *comfort package only* never appears here — that family asked not to have a prayer team. That is on purpose.")
    r.step("Click “I'll pray for this family”",
           "Click the green **I'll pray for this family** button on the card you chose.",
           target=first,
           action=lambda: (first.click(), page.wait_for_timeout(1500)),
           expect="The page refreshes. That family is no longer in the list below — it has moved up into **Praying For**.")
    page.evaluate("window.scrollTo(0,0)")
    r.step("Confirm you are on the team",
           "Scroll back to the top. The **Praying For** tile now says **1**, and the family you picked is listed under it.",
           target=tiles + [page.locator("button:has-text(\"I'll stop praying for this family\")").first],
           expect="Praying For: 1, and Families You Could Join is one lower. Under the tiles, a card with the family's name, *Praying since* and today's date, and a button **I'll stop praying for this family**.")
    r.check(["Praying For shows 1 (or more).", "The family you chose is listed with a **I'll stop praying for this family** button."])
    r.note("Many volunteers can pray for the same family, and you can pray for as many families as you like. There is no limit either way.", "Good to know")

    # ---------------------------------------------------------------- Process 4
    r.process("Stop praying for a family", "Take yourself off a family's prayer team.",
              who="Prayer Ambassadors", need="You are on at least one family's team (Process 3).", time="30 seconds")
    leave = page.locator("button:has-text(\"I'll stop praying for this family\")").first
    r.step("Find the family under “Praying For”",
           "Near the top of the page, find the family you no longer want to pray for. Their button says **I'll stop praying for this family**.",
           target=leave, expect="The family card with the stop button on its right.")
    r.step("Click “I'll stop praying for this family”",
           "Click the button. It takes effect straight away — there is no “are you sure?” question. You can add yourself back at any time from the list below.",
           target=leave, action=lambda: (leave.click(), page.wait_for_timeout(1500)),
           expect="**Praying For** goes back down by one, and the family returns to **Families You Could Pray For**.")
    r.check(["Praying For is one lower than before.", "The family is back in the *Families You Could Pray For* list."])

    r.h2("If something looks wrong")
    r.bullets([
        "**I clicked Create my volunteer record and nothing happened.** Press **↻ Refresh**. If it still says *You aren't set up as a volunteer yet*, tell your coordinator.",
        "**A family I expected is not in the list.** That family asked for a comfort package only, with no prayer team. It is hidden from this page on purpose.",
        "**I forgot my password.** Click **Forgot password** on the sign-in page, or ask an administrator for a temporary password.",
        "**The page looks blank or stuck.** Press **↻ Refresh**, or reload the browser page (F5).",
    ])
    r.write("02-Volunteer-Guide-Praying-for-a-Family")
    return r


if __name__ == "__main__":
    session(None, build)
