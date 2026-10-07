"""Shared helpers for building the step-by-step manuals.

A manual is a Python module in specs/ that drives the *running* app with Playwright. Every `step()` call:
  1. scrolls the target into view and draws a numbered red box around it,
  2. takes a screenshot (the state the reader sees *before* acting),
  3. performs the action,
  4. writes the matching Markdown (instruction + image + "You should see").
So the screenshots and the text can never drift apart: re-run the spec after a UI change and both regenerate.
"""
from __future__ import annotations

import io
import os
import re
import sys
from pathlib import Path

import requests
from PIL import Image
from playwright.sync_api import Page, sync_playwright

WEB = os.environ.get("LOTV_WEB", "http://localhost:5101")
API = os.environ.get("LOTV_API", "http://localhost:5100")
ROOT = Path(__file__).resolve().parents[2]
MANUALS = ROOT / "docs" / "manuals"
SRC = MANUALS / "src"
IMAGES = MANUALS / "images"
PASSWORD = "DevPassword1!"

VIEWPORT = {"width": 1200, "height": 760}

_HL_JS = """
([sel, n]) => {
  document.querySelectorAll('.__hl').forEach(e => e.remove());
  const els = Array.isArray(sel) ? sel : [sel];
  return els.length;
}
"""


def api_token(username: str, password: str = PASSWORD) -> str:
    r = requests.post(f"{API}/api/v1/auth/login", json={"username": username, "password": password}, timeout=30)
    r.raise_for_status()
    return r.json()["accessToken"]


def api(method: str, path: str, token: str, **kw):
    r = requests.request(method, f"{API}{path}", headers={"Authorization": f"Bearer {token}"}, timeout=60, **kw)
    return r


def login(page: Page, username: str, password: str = PASSWORD, wait_url: str | None = None):
    page.goto(f"{WEB}/login")
    page.wait_for_selector("#login-username", timeout=30000)
    page.fill("#login-username", username)
    page.fill("#login-password", password)
    page.click('button:has-text("Sign In")')
    page.wait_for_url(wait_url or re.compile(r".*/(admin|board|change-password|volunteer).*"), timeout=30000)
    settle(page)


def settle(page: Page, ms: int = 900):
    try:
        page.wait_for_load_state("networkidle", timeout=8000)
    except Exception:
        pass
    page.wait_for_timeout(ms)


def _draw(page: Page, targets: list, badge: int | None):
    """Draw a red box (and a numbered badge) around each target locator. Returns the union bounding box."""
    boxes = []
    scrub(page)
    for t in targets:
        try:
            t.first.scroll_into_view_if_needed(timeout=5000)
        except Exception:
            pass
    page.wait_for_timeout(250)
    for t in targets:
        try:
            bb = t.first.bounding_box(timeout=5000)
        except Exception:
            bb = None
        if bb:
            boxes.append(bb)
    page.evaluate(
        """([boxes, badge]) => {
          document.querySelectorAll('.__hl').forEach(e => e.remove());
          boxes.forEach((b, i) => {
            const d = document.createElement('div'); d.className = '__hl';
            d.style.cssText = `position:fixed;z-index:2147483647;pointer-events:none;border:3px solid #e11d48;`
              + `border-radius:8px;box-shadow:0 0 0 4px rgba(225,29,72,.25);`
              + `left:${b.x-5}px;top:${b.y-5}px;width:${b.width+10}px;height:${b.height+10}px;`;
            if (i === 0 && badge) {
              const n = document.createElement('div');
              n.textContent = badge;
              n.style.cssText = 'position:absolute;left:-14px;top:-14px;width:26px;height:26px;border-radius:50%;'
                + 'background:#e11d48;color:#fff;font:700 14px/26px Arial,sans-serif;text-align:center;';
              d.appendChild(n);
            }
            document.documentElement.appendChild(d);
          });
        }""",
        [boxes, badge],
    )
    return boxes


_SCRUB_JS = """() => {
  for (const el of document.querySelectorAll('div,span,p,small,a,button')) {
    const t = (el.textContent || '').trim();
    if (el.children.length <= 3 && ((/Demo credentials/i.test(t) && t.length < 200) || (/\d+\s+migrations?$/i.test(t) && t.length < 30)))
      el.style.display = 'none';
  }
  // live "Case Assigned"-style pop-ups are timing noise in a still picture
  document.querySelectorAll('[role=status][aria-live]').forEach(e => e.style.display = 'none');
}"""


def scrub(page: Page):
    """Hide dev-only furniture (demo-credentials hint, pending-migrations badge) so it never lands in a manual."""
    page.evaluate(_SCRUB_JS)


def _clear(page: Page):
    page.evaluate("document.querySelectorAll('.__hl').forEach(e => e.remove())")


def _save(png: bytes, path: Path, clip=None):
    path.parent.mkdir(parents=True, exist_ok=True)
    im = Image.open(io.BytesIO(png)).convert("RGB")
    if clip:
        im = im.crop(clip)
    # Palette-quantise: UI screenshots are flat colour, so this cuts size ~3x with no visible loss.
    im.quantize(colors=192, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).save(path, optimize=True)


class Recorder:
    def __init__(self, slug: str, title: str, page: Page):
        self.slug = slug
        self.title = title
        self.page = page
        self.md: list[str] = []
        self.proc = 0
        self.stepn = 0
        self.shots = 0

    # ---- prose -----------------------------------------------------------------------------------
    def raw(self, text: str = ""):
        self.md.append(text)

    def h1(self, text: str):
        self.md += [f"# {text}", ""]

    def h2(self, text: str):
        self.md += [f"## {text}", ""]

    def p(self, text: str):
        self.md += [text, ""]

    def bullets(self, items: list[str]):
        self.md += [f"- {i}" for i in items] + [""]

    def note(self, text: str, kind: str = "Note"):
        self.md += [f"> **{kind}:** {text}", ""]

    def process(self, name: str, goal: str, who: str | None = None, need: str | None = None, time: str | None = None):
        self.proc += 1
        self.stepn = 0
        self.md += [f"## Process {self.proc}: {name}", ""]
        meta = [f"**Goal:** {goal}"]
        if who:
            meta.append(f"**Who can do this:** {who}")
        if need:
            meta.append(f"**Before you start:** {need}")
        if time:
            meta.append(f"**Time:** {time}")
        self.md += ["  \n".join(meta), ""]

    def check(self, items: list[str]):
        self.md += ["**Check your work** — you are done when:", ""] + [f"- [ ] {i}" for i in items] + [""]

    # ---- the core ---------------------------------------------------------------------------------
    def step(
        self,
        title: str,
        body: str,
        target=None,
        action=None,
        expect: str | None = None,
        trouble: str | None = None,
        shot: str = "viewport",
        after: bool = False,
        no_shot: bool = False,
        settle_ms: int = 900,
        when: str = "before",
    ):
        """
        target : Locator (or list of Locators) to box on the BEFORE screenshot. None = plain screenshot.
        action : callable run after the screenshot (click/type/...). May be None for look-only steps.
        after  : also capture the screen *after* the action as the "You should see" picture.
        when   : 'before' (default) boxes the target then acts; 'after' acts first, then boxes + shoots (use for typing,
                 where the reader should see the typed text).
        shot   : 'viewport' (default) or 'full' (full scrollable page) or 'element' (crop around the target).
        """
        self.stepn += 1
        n = self.stepn
        page = self.page
        pid = f"p{self.proc}-s{n:02d}"
        imgs: list[str] = []

        if when == "after" and action is not None:
            action()
            settle(page, settle_ms)
            action = None

        if not no_shot:
            targets = [] if target is None else (target if isinstance(target, list) else [target])
            boxes = _draw(page, targets, n) if targets else (scrub(page) or [])
            if shot == "full":
                png = page.screenshot(full_page=True)
                clip = None
            else:
                png = page.screenshot()
                clip = None
                if shot == "element" and boxes:
                    x0 = max(0, min(b["x"] for b in boxes) - 60)
                    y0 = max(0, min(b["y"] for b in boxes) - 60)
                    x1 = min(VIEWPORT["width"], max(b["x"] + b["width"] for b in boxes) + 60)
                    y1 = min(VIEWPORT["height"], max(b["y"] + b["height"] for b in boxes) + 60)
                    clip = (int(x0), int(y0), int(x1), int(y1))
            _clear(page)
            rel = f"images/{self.slug}/{pid}.png"
            _save(png, MANUALS / rel, clip)
            imgs.append(rel)
            self.shots += 1

        if action is not None:
            action()
            settle(page, settle_ms)

        if after and not no_shot:
            scrub(page)
            png = page.screenshot()
            rel = f"images/{self.slug}/{pid}-after.png"
            _save(png, MANUALS / rel)
            imgs.append(rel)
            self.shots += 1

        self.md += [f"### Step {n}. {title}", "", body, ""]
        for i, rel in enumerate(imgs):
            cap = "What you will see" if i == 0 and len(imgs) == 1 else ("Before" if i == 0 else "After you click")
            self.md += [f"![{title} — {cap}]({rel})", ""]
        if expect:
            self.md += [f"**You should see:** {expect}", ""]
        if trouble:
            self.md += [f"> **If it doesn't look like this:** {trouble}", ""]

    def signin(self, username: str, lands: str, who: str = "your username", password: str = PASSWORD, pw_text: str | None = None):
        """Standard 4-step sign-in. Leaves the browser signed in."""
        page = self.page
        page.goto(f"{WEB}/login")
        page.wait_for_selector("#login-username", timeout=30000)
        settle(page, 1200)
        self.step("Open the sign-in page",
                  "In your web browser, go to the LOTV staff portal sign-in page (your coordinator will give you the web address; it ends in `/login`).",
                  target=page.locator("#login-username"),
                  expect="A page titled **Sign In** with two boxes: *Username* and *Password*.")
        self.step("Type your username",
                  f"Click the **Username** box and type {who}. It is not case-sensitive.",
                  target=page.locator("#login-username"),
                  action=lambda: page.fill("#login-username", username), when="after",
                  expect="Your username appears in the Username box.")
        self.step("Type your password",
                  pw_text or "Click the **Password** box and type your password. The characters show as dots so nobody can read them over your shoulder.",
                  target=page.locator("#login-password"),
                  action=lambda: page.fill("#login-password", password), when="after",
                  expect="A row of dots in the Password box.",
                  trouble="If an administrator gave you a *temporary* password, you will be asked to choose a new one right after the next step. That is normal.")
        def _go():
            page.click('button:has-text("Sign In")')
            page.wait_for_url(re.compile(r".*/(admin|board|change-password|volunteer).*"), timeout=30000)
        self.step("Click Sign In",
                  "Click the green **Sign In** button.",
                  target=page.locator('button:has-text("Sign In")'),
                  action=_go, after=True,
                  expect=lands,
                  trouble="A red message under the password box means the username or password was typed wrong. Retype both. After several wrong tries the account may lock for a few minutes — ask an administrator for a temporary password.",
                  settle_ms=2000)

    def write(self, name: str):
        SRC.mkdir(parents=True, exist_ok=True)
        (SRC / f"{name}.md").write_text("\n".join(self.md) + "\n", encoding="utf-8")
        print(f"[{self.slug}] wrote {name}.md — {self.shots} screenshots", flush=True)


def session(role_user: str, fn, password: str = PASSWORD, start: str | None = None):
    """Open a fresh browser context signed in as `role_user` and call fn(page)."""
    with sync_playwright() as p:
        b = p.chromium.launch()
        ctx = b.new_context(viewport=VIEWPORT, device_scale_factor=1)
        page = ctx.new_page()
        page.set_default_timeout(15000)
        if role_user:
            login(page, role_user, password)
        if start:
            page.goto(f"{WEB}{start}")
            settle(page, 1500)
        try:
            return fn(page)
        except Exception:
            try:
                page.screenshot(path=str(ROOT / "tools" / "manuals" / "_fail.png"))
            except Exception:
                pass
            raise
        finally:
            b.close()


def give_cases(volunteer_email: str, count: int = 2, chapter_id: int = 1) -> list[int]:
    """As an admin, assign `count` brand-new unassigned cases to the volunteer (so a volunteer's queue has real work)."""
    me = api("GET", "/api/v1/volunteers/me", api_token(volunteer_email)).json()
    tok = api_token("chris.kremer")
    cases = api("GET", "/api/v1/requests", tok).json()
    pick = [c for c in cases if c["status"] == "New" and c.get("processStage") == "Unassigned"
            and c["family"]["chapterId"] == chapter_id and c.get("wantsPackage", True)][:count]
    for c in pick:
        r = api("PUT", f"/api/v1/requests/{c['id']}/assign", tok, json={"volunteerId": me["id"]})
        r.raise_for_status()
    return [c["id"] for c in pick]


def show_options(loc):
    """Expand a <select> into an always-open list so a screenshot can show every choice (headless Chrome never paints the native popup)."""
    loc.evaluate("e => { e.dataset.hlSize = e.size; e.size = Math.min(e.options.length, 12); e.style.height = 'auto'; }")


def hide_options(loc):
    loc.evaluate("e => { e.size = +e.dataset.hlSize || 0; e.style.height = ''; }")
