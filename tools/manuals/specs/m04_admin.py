"""Manual 04 — Admin Guide: Users, Temp Passwords & Login As. Personas: Chris Kremer (HQAdmin), Nora Newhire, Sam Staffer."""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lib import *  # noqa: E402

SLUG = "04-admin"


def build(page):
    r = Recorder(SLUG, "Admin Guide: Users, Temporary Passwords & Login As", page)
    r.h1("Admin Guide: Users, Temporary Passwords & Login As")
    r.p("**Who this is for:** administrators. *Temporary passwords* can be set by an **HQAdmin, ChapterAdmin or Director**. **Login As** is for the **HQAdmin only**.")
    r.p("**When you finish you will be able to:** find a person's account, give someone locked out a one-time temporary password, "
        "walk them through choosing their own password, and sign in as another person to see exactly what they see.")
    r.note("The people in the pictures are practice accounts. Nobody — not even an administrator — can ever see another person's real password. A temporary password replaces it; it is the only way to help someone back in.", "Privacy")

    # ---------------------------------------------------------------- 1
    r.process("Sign in as an administrator", "Reach the staff dashboard.", who="HQAdmin, ChapterAdmin, Director", time="1 minute")
    r.signin("chris.kremer", "You land on the **Chapter Dashboard** with a long green menu on the left.", who="your administrator username")
    r.check(["Your name and your role (for example *HQAdmin*) are in the top-right corner.", "The left menu contains a **System Admin** link."])

    # ---------------------------------------------------------------- 2
    r.process("Open User Management", "Find the list of staff accounts.", who="HQAdmin, ChapterAdmin, Director", need="You are signed in (Process 1).", time="1 minute")
    sa = page.locator('a.sidebar-link:has-text("System Admin")').first
    r.step("Click System Admin in the left menu",
           "In the green menu on the left, find the heading **System Admin** and click the **⚙ System Admin** link under it.",
           target=sa, action=lambda: (sa.click(), page.wait_for_url(re.compile(r".*system-admin-hub"), timeout=20000)), after=True,
           expect="The **System Admin** page opens with a row of tabs across the top.",
           trouble="No System Admin link? Your role may not include it. Ask an HQAdmin.")
    tab = page.get_by_role("button", name="User Management")
    r.step("Click the User Management tab",
           "Click the **User Management** tab.",
           target=tab, action=lambda: (tab.click(), page.wait_for_selector("text=Staff accounts and role permissions", timeout=20000)), after=True,
           expect="A table of accounts with the columns Name, Username, Recovery Email, Role, Status and Last Login. Your own row is shaded green and marked **You**.")
    r.step("Read the table",
           "Each row is one account. **Role** shows what they can do. **Status** shows *Active* or *Inactive*. At the right of every row other than yours are buttons: **Edit**, **Login as** (HQAdmin only), **🔑 Temp Password** and a bell.",
           target=page.locator("tbody tr", has_text="Nora Newhire"),
           expect="Nora Newhire's row with **Edit**, **Login as** and **🔑 Temp Password** buttons.")
    r.note("The **+ Invite User** button at the top right is not available yet. New accounts are created by the system administrator, not from this page.", "Good to know")
    r.check(["You can see the list of accounts.", "You can find a specific person's row by name."])

    # ---------------------------------------------------------------- 3
    r.process("Give someone a temporary password", "Let a person who forgot or never received a password get back in.",
              who="HQAdmin, ChapterAdmin, Director", need="You are on User Management (Process 2). You will hand the password to the person in person or by phone.", time="2 minutes")
    row = page.locator("tbody tr", has_text="Nora Newhire")
    tb = row.get_by_role("button", name="Temp Password")
    r.step("Click 🔑 Temp Password on the person's row",
           "Find the person. Click **🔑 Temp Password** at the right end of their row.",
           target=tb, action=lambda: (tb.click(), page.wait_for_selector("#temp-password-dialog", timeout=15000)), after=True,
           expect="A box opens: **Set a temporary password for Nora Newhire?** explaining it replaces the current password immediately.")
    go = page.get_by_role("button", name="Set Temporary Password")
    r.step("Read the box, then click Set Temporary Password",
           "Read the message. It replaces their old password the instant you click. If you picked the wrong person, click **Cancel**. Otherwise click **Set Temporary Password**.",
           target=go, action=lambda: (go.click(), page.wait_for_selector("#temp-password-value", timeout=20000)), after=True,
           expect="The box changes to **✓ Temporary password set** and shows a random password in large type.")
    pwbox = page.locator("#temp-password-value")
    pw = pwbox.inner_text().strip()
    r.step("Write the password down and give it to the person",
           "Copy or write down the password exactly as shown, including capital letters and symbols. **It is shown only once.** Tell the person in person or by phone — not by email or chat.",
           target=pwbox, expect="One line of letters, numbers and symbols. This is the only time you will see it.",
           trouble="Closed the box before copying it? Click **🔑 Temp Password** again to make a new one. The old one stops working.")
    done = page.get_by_role("button", name="Done")
    r.step("Click Done",
           "Click **Done**. The password disappears from the screen for good.",
           target=done, action=lambda: (done.click(), page.wait_for_timeout(800)),
           expect="Back to the table.")
    r.check(["You gave the password to the right person.", "You closed the box with **Done**."])

    # ---------------------------------------------------------------- 4
    r.process("Walk the person through their first sign-in", "Help the person choose their own password.",
              who="The person receiving the temporary password (you can guide them)", need="The temporary password from Process 3.", time="3 minutes")
    ctx2 = page.context.browser.new_context(viewport=VIEWPORT, device_scale_factor=1)
    p2 = ctx2.new_page()
    p2.set_default_timeout(15000)
    admin_page, r.page = page, p2
    r.signin("nora.newhire@example.org",
             "Straight away you are taken to a page titled **Set your own password**. You cannot reach anything else until you finish it.",
             who="their username", password=pw,
             pw_text="Click the **Password** box and type the **temporary** password you were given, exactly as written.")
    cur = p2.locator("#cp-current")
    r.step("Type the temporary password in the first box",
           "In **Temporary Password**, type the temporary password again.",
           target=cur, action=lambda: cur.fill(pw), when="after", expect="Dots in the first box.")
    NEW = "Shiny-New-Pass#2026"
    nb = p2.locator("#cp-new")
    r.step("Choose a new password",
           "In **New Password**, type a password only you know. It needs **at least 12 characters, with a capital letter, a number and a symbol**. Example shape: *Blue-Garden#2026*.",
           target=nb, action=lambda: nb.fill(NEW), when="after", expect="Dots in the second box.")
    nb2 = p2.locator("#cp-new2")
    r.step("Type it again to confirm",
           "In **Confirm New Password**, type the same new password again.",
           target=nb2, action=lambda: nb2.fill(NEW), when="after", expect="Dots in the third box.")
    sp = p2.get_by_role("button", name="Set Password")
    r.step("Click Set Password",
           "Click **Set Password**.",
           target=sp, action=lambda: (sp.click(), p2.wait_for_url(re.compile(r".*/(admin|board).*"), timeout=30000)), after=True,
           expect="You are taken into the portal. From now on, sign in with your new password.",
           trouble="A red message under the boxes says what is wrong — usually the password is too short or the two new passwords do not match. Fix and click Set Password again.")
    r.check(["The person reached the portal.", "The temporary password no longer works; only their new one does."])
    r.page = admin_page
    ctx2.close()

    # ---------------------------------------------------------------- 5
    r.process("Sign in as another person (Login As)", "See the portal exactly as another person sees it, for example to help them or reproduce a problem.",
              who="HQAdmin only", need="You are on User Management. You cannot use it on yourself, nor while already signed in as someone else.", time="3 minutes")
    srow = page.locator("tbody tr", has_text="Sam Staffer")
    la = srow.get_by_role("button", name="Login as")
    r.step("Click Login as on the person's row",
           "Find the person. Click **Login as** at the right end of their row. It works on anyone else, even another HQAdmin or a deactivated account.",
           target=la, action=lambda: (la.click(), page.wait_for_selector("#login-as-confirm", timeout=15000)), after=True,
           expect="A box titled **Sign in as Sam Staffer?** explains what will happen.")
    go2 = page.locator("#login-as-go")
    r.step("Read the box, then click Sign in as them",
           "Read it: everything you do is recorded under both names, account and profile changes are turned off, and it ends by itself after 30 minutes. Click **Sign in as them**.",
           target=go2, action=lambda: (go2.click(), page.wait_for_url(re.compile(r".*/admin/.*"), timeout=30000), page.wait_for_timeout(2500)), after=True,
           expect="You are now looking at the portal as Sam. A coloured banner stays on screen naming whose account you are in.")
    banner = page.locator("text=Return to my account").first
    r.step("Notice the banner",
           "The banner at the top reminds you whose account you are in and how long is left. Anything you click now is done as that person, so be careful — and remember it is logged under both names.",
           target=banner, expect="A banner with the person's name and a **Return to my account** link.")
    r.step("Return to your own account",
           "When you finish, click **Return to my account** in the banner. If you forget, it ends by itself after 30 minutes and cannot be extended.",
           target=banner, action=lambda: (banner.click(), page.wait_for_timeout(3500)), after=True,
           expect="You are back as yourself (your name in the top-right corner, no banner).")
    r.check(["The banner is gone.", "Your own name is in the top-right corner."])

    r.h2("If something looks wrong")
    r.bullets([
        "**I closed the temporary-password box before copying it.** Click **🔑 Temp Password** again. A new one replaces it.",
        "**The person says the temporary password does not work.** Check capital letters, symbols and the digits 0 / O and 1 / l. Make a fresh one if unsure.",
        "**There is no Login as button.** Only an HQAdmin has it, never on your own row, and not while already signed in as someone else.",
        "**I cannot find the person.** The list shows only your chapter unless you are an HQAdmin.",
    ])
    r.write("04-Admin-Guide-Users-Temp-Passwords-Login-As")
    return r


if __name__ == "__main__":
    # needs two browser contexts (admin + the person being helped), so it manages its own browser
    with sync_playwright() as p:
        b = p.chromium.launch()
        ctx = b.new_context(viewport=VIEWPORT, device_scale_factor=1)
        pg = ctx.new_page()
        pg.set_default_timeout(15000)
        try:
            build(pg)
        except Exception:
            pg.screenshot(path=str(ROOT / "tools" / "manuals" / "_fail.png"))
            raise
        finally:
            b.close()
