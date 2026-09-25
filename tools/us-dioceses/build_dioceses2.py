import csv
import json
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")
exec(open("build_dioceses.py", encoding="utf-8").read().split("wiki = open")[0])  # reuse the STATES table

wiki = open("dioceses.wiki", encoding="utf-8").read()
section = wiki[wiki.index("== Territorial provinces and dioceses =="):wiki.index("== Eastern Catholic eparchies ==")]

found = []
for m in re.finditer(r"\[\[((?:Roman Catholic )?(?:Archdiocese|Diocese)[^\]|]*)(?:\|([^\]]*))?\]\]", section):
    target = m.group(1).replace("_", " ").strip()
    display = (m.group(2) or target).strip()
    display = re.sub(r"^Roman Catholic ", "", display)
    if target.startswith("Diocese of the ") or "Ecclesiastical" in target:
        continue
    if all(f[0] != target for f in found):
        found.append((target, display))

# Wikidata: title -> seat city and state, following the seat up to its state
wd = {}
for r in json.load(open("wd3.json", encoding="utf-8"))["results"]["bindings"]:
    e = wd.setdefault(r["title"]["value"].replace("_", " "), {"seat": set(), "state": set()})
    e["seat"].add(r["seatLabel"]["value"]); e["state"].add(r["stateLabel"]["value"])

# Places Wikidata's state chain can't give (territories, the federal district), and articles whose title differs from the link.
MANUAL = {
    "Archdiocese of Washington": ("Washington", "DC"),
    "Diocese of Saint Thomas": ("Charlotte Amalie", "VI"),
    "Archdiocese of San Juan": ("San Juan", "PR"), "Diocese of Arecibo": ("Arecibo", "PR"), "Diocese of Caguas": ("Caguas", "PR"),
    "Diocese of Fajardo–Humacao": ("Humacao", "PR"), "Diocese of Mayagüez": ("Mayagüez", "PR"), "Diocese of Ponce": ("Ponce", "PR"),
    "Archdiocese of Agaña": ("Hagåtña", "GU"), "Diocese of Chalan Kanoa": ("Chalan Kanoa", "MP"),
    "Diocese of Samoa–Pago Pago": ("Pago Pago", "AS"),
    "Diocese of Joliet": ("Joliet", "IL"), "Diocese of Lincoln": ("Lincoln", "NE"), "Diocese of San Jose": ("San Jose", "CA"),
    # the cathedral city, where Wikidata gives the chancery city, a county or the state
    "Diocese of San Bernardino": ("San Bernardino", "CA"), "Archdiocese of Boston": ("Boston", "MA"), "Diocese of Arlington": ("Arlington", "VA"),
    "Archdiocese of New York": ("New York", "NY"), "Diocese of Baker": ("Baker City", "OR"), "Archdiocese of Saint Paul and Minneapolis": ("Saint Paul", "MN"),
    "Diocese of Palm Beach": ("Palm Beach Gardens", "FL"), "Diocese of Norwich": ("Norwich", "CT"),
}
SKIP = {"Archdiocese for the Military Services, USA"}   # no territory of its own; not a place parishes are matched by state

rows, review = [], []
for target, display in found:
    if target in SKIP: continue
    if display in MANUAL or target in MANUAL:
        city, code = MANUAL.get(display) or MANUAL[target]; how = "manual"
    elif target in wd and len(wd[target]["seat"]) == 1 and len(wd[target]["state"]) == 1:
        city = next(iter(wd[target]["seat"])); st = next(iter(wd[target]["state"])); code = STATES.get(st); how = "wikidata"
        if code is None: review.append((display, f"unknown state {st}")); continue
    elif target in wd:
        review.append((display, f"ambiguous: seats={sorted(wd[target]['seat'])} states={sorted(wd[target]['state'])}")); continue
    else:
        review.append((display, "no Wikidata match")); continue
    if "County" in city or city in STATES: review.append((display, f"suspicious seat: {city}"))
    rows.append({"name": display, "city": city, "state": code, "source": how})

json.dump(rows, open("dioceses_final.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(f"links: {len(found)} | rows: {len(rows)} | needing review: {len(review)}")
for r in review: print("  REVIEW:", r)
print("\nby state:", ", ".join(f"{s}:{n}" for s, n in sorted(__import__('collections').Counter(r['state'] for r in rows).items())))
print("\nALL ROWS:")
for r in rows: print(f"  {r['name']} | {r['city']}, {r['state']} ({r['source']})")

import csv
rows.sort(key=lambda r: (r["state"], r["name"]))
with open("us-dioceses.csv", "w", encoding="utf-8", newline="") as f:
    w = csv.writer(f, lineterminator=chr(10))
    w.writerow(["Name", "City", "State"])
    for r in rows: w.writerow([r["name"], r["city"], r["state"]])
print("wrote us-dioceses.csv with", len(rows), "rows")
