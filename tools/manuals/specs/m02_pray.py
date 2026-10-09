"""Manual 02 — Volunteer Guide: Praying for a Family. Persona: Penny Prayer (Volunteer, no volunteer record yet).

The Prayer Dashboard is one plain list of open families. Each family shows who is praying and how many; one button joins
or leaves. There is no setup screen: the volunteer record is made the first time someone taps "I'll pray for this family".
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lib import *  # noqa: E402

SLUG = "02-pray"


def build(page):
    r = Recorder(SLUG, "Volunteer Guide: Praying for a Family", page)
    r.h1("Volunteer Guide: Praying for a Family")
    r.p("**Who this is for:** volunteers (called *Ambassadors* in the portal) who want to pray for the families who have asked for help. "
        "You do not need any computer skill beyond using a web browser. Every click is shown below with a picture; "
        "the item to click is circled in red and numbered to match the step.")
    r.p("**What you will be able to do when you finish:** sign in, see the families who need prayer and who is already praying "
        "for them, choose families to pray for, and stop praying for a family whenever you like.")
    r.note("Everything in this guide uses the pictures you will really see. Names of families in the pictures are practice (sample) data, not real people.", "Practice data")

    # ---------------------------------------------------------------- Process 1
    r.process("Sign in", "Get into the LOTV portal.", who="Any volunteer with an account",
              need="Your username and password from your coordinator (an administrator creates your account).", time="1 minute")
    r.signin("penny.prayer@example.org",
             "You land on the **Prayer Dashboard** — a list of families who need prayer. There is nothing to set up first.",
             who="the username your coordinator gave you")
    r.check(["You can see your own name in the top-right corner.", "The left menu shows **Prayer Dashboard**.", "The page shows a list of families."])

    # ---------------------------------------------------------------- Process 2
    r.process("See who needs prayer", "Read the list of families and see who is already praying for each one.",
              who="Any signed-in volunteer", need="You are signed in (Process 1).", time="1 minute")
    first = page.locator('button:has-text("I\'ll pray for this family")').first
    r.step("Look at the list",
           "Each white card is one family. It shows the family's name, the reason they asked for prayer, the date, and their story if they shared one. "
           "Under that, a line says how many people are praying for them and who — or *0 praying — be the first*. Look through the list; there is no wrong choice.",
           target=first, shot="viewport",
           expect="A list of white cards, each with a green **I'll pray for this family** button on the right.",
           trouble="A family is missing from the list? A request marked *comfort package only* never appears here — that family asked not to have a prayer team. That is on purpose.")

    # ---------------------------------------------------------------- Process 3
    r.process("Choose a family to pray for", "Add yourself to a family's prayer team.",
              who="Any volunteer", need="You have finished Process 2.", time="1 minute")
    r.step("Click “I'll pray for this family”",
           "Click the green **I'll pray for this family** button on the card you chose. The first time you do this the portal sets up your volunteer record for you — there is nothing else to fill in.",
           target=first,
           action=lambda: (first.click(), page.wait_for_selector('button:has-text("Praying — stop")', timeout=20000), page.wait_for_timeout(800)),
           expect="The family moves to the top of the list, its button now says **Praying — stop**, and its line says *1 praying* with your name.",
           trouble="If nothing changes after a few seconds, reload the page (F5) and look again — you may already be on that family's team. If you see a message that you can't add yourself, tell your coordinator.")
    page.evaluate("window.scrollTo(0,0)")
    r.step("Confirm you are on the team",
           "Look at the top of the list. The family you picked is first, marked **Praying — stop**, and **1 praying** shows your name.",
           target=[page.locator('button:has-text("Praying — stop")').first, page.get_by_text("1 praying").first], shot="viewport",
           expect="Your family at the top with **Praying — stop** and *1 praying* followed by your name.")
    r.check(["The family you chose is at the top of the list.", "Its button says **Praying — stop**.", "Its line says **1 praying** and shows your name."])
    r.note("Many volunteers can pray for the same family, and you can pray for as many families as you like. There is no limit either way. "
           "You are never required to choose a family — you can just read the list.", "Good to know")

    # ---------------------------------------------------------------- Process 4
    r.process("Stop praying for a family", "Take yourself off a family's prayer team.",
              who="Any volunteer", need="You are on at least one family's team (Process 3).", time="30 seconds")
    leave = page.locator('button:has-text("Praying — stop")').first
    r.step("Find the family you are praying for",
           "The families you pray for are at the top of the list. Find the one you no longer want to pray for. Its button says **Praying — stop**.",
           target=leave, expect="The family card with the **Praying — stop** button on its right.")
    r.step("Click “Praying — stop”",
           "Click the button. It takes effect straight away — there is no “are you sure?” question. You can add yourself back at any time.",
           target=leave, action=lambda: (leave.click(), page.wait_for_timeout(1500)),
           expect="The family's button goes back to **I'll pray for this family** and it shows one fewer person praying.")
    r.check(["The family's button says **I'll pray for this family** again.", "Your name is no longer in its *praying* line."])

    r.h2("If something looks wrong")
    r.bullets([
        "**I clicked a family and nothing happened.** Reload the page (F5). If it still does not change, tell your coordinator.",
        "**A family I expected is not in the list.** That family asked for a comfort package only, with no prayer team. It is hidden from this page on purpose.",
        "**I forgot my password.** Click **Forgot password** on the sign-in page, or ask an administrator for a temporary password.",
        "**The page looks blank or stuck.** Reload the browser page (F5).",
    ])
    r.write("02-Volunteer-Guide-Praying-for-a-Family")
    return r


if __name__ == "__main__":
    session(None, build)
