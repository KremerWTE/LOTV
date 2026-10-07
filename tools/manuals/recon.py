"""Dump what a role sees on a page: python recon.py <user> <path> [<path>...]  (dev aid, not part of the build)"""
import sys, io
from lib import *
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
user, paths = sys.argv[1], sys.argv[2:]
def go(page):
    for path in paths:
        page.goto(WEB+path); settle(page, 2000)
        print("=====", path, "->", page.url)
        print("H:", [h.inner_text() for h in page.query_selector_all('h1,h2,h3,h4')][:15])
        print("BTN:", [b.inner_text().strip()[:40] for b in page.query_selector_all('button') if b.is_visible() and b.bounding_box()['x']>240][:40])
        print("A:", [a.inner_text().strip()[:30] for a in page.query_selector_all('a') if a.is_visible() and a.bounding_box()['x']>240][:25])
        print("SEL:", [(s.get_attribute('id') or s.get_attribute('aria-label')) for s in page.query_selector_all('select, input, textarea') if s.is_visible()][:25])
        page.screenshot(path=f"recon-{path.strip('/').replace('/','_')}.png")
session(user, go)
