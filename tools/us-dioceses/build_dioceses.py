import csv
import json
import re

STATES = {
    "Alabama": "AL", "Alaska": "AK", "Arizona": "AZ", "Arkansas": "AR", "California": "CA", "Colorado": "CO",
    "Connecticut": "CT", "Delaware": "DE", "District of Columbia": "DC", "Florida": "FL", "Georgia": "GA",
    "Hawaii": "HI", "Idaho": "ID", "Illinois": "IL", "Indiana": "IN", "Iowa": "IA", "Kansas": "KS", "Kentucky": "KY",
    "Louisiana": "LA", "Maine": "ME", "Maryland": "MD", "Massachusetts": "MA", "Michigan": "MI", "Minnesota": "MN",
    "Mississippi": "MS", "Missouri": "MO", "Montana": "MT", "Nebraska": "NE", "Nevada": "NV", "New Hampshire": "NH",
    "New Jersey": "NJ", "New Mexico": "NM", "New York": "NY", "North Carolina": "NC", "North Dakota": "ND", "Ohio": "OH",
    "Oklahoma": "OK", "Oregon": "OR", "Pennsylvania": "PA", "Rhode Island": "RI", "South Carolina": "SC",
    "South Dakota": "SD", "Tennessee": "TN", "Texas": "TX", "Utah": "UT", "Vermont": "VT", "Virginia": "VA",
    "Washington": "WA", "West Virginia": "WV", "Wisconsin": "WI", "Wyoming": "WY", "Puerto Rico": "PR",
    "Guam": "GU", "U.S. Virgin Islands": "VI", "United States Virgin Islands": "VI",
    "Northern Mariana Islands": "MP", "American Samoa": "AS",
}

wiki = open("dioceses.wiki", encoding="utf-8").read()
start = wiki.index("== Territorial provinces and dioceses ==")
end = wiki.index("== Eastern Catholic eparchies ==")
section = wiki[start:end]

# every diocese link in the current territorial sections (target, display text), in page order
found = []
for m in re.finditer(r"\[\[((?:Archdiocese|Diocese)[^\]|]*)(?:\|([^\]]*))?\]\]", section):
    target = m.group(1).replace("_", " ").strip()
    display = (m.group(2) or target).strip()
    if target.startswith("Diocese of the ") or "Ecclesiastical" in target:
        continue
    if all(f[0] != target for f in found):
        found.append((target, display))

wd = json.load(open("wd2.json", encoding="utf-8"))["results"]["bindings"]
by_title = {}
for r in wd:
    title = r["title"]["value"].replace("_", " ")
    e = by_title.setdefault(title, {"seat": set(), "state": set(), "type": set()})
    if "seatLabel" in r: e["seat"].add(r["seatLabel"]["value"])
    if "stateLabel" in r: e["state"].add(r["stateLabel"]["value"])
    if "typeLabel" in r: e["type"].add(r["typeLabel"]["value"])

rows, problems = [], []
for target, display in found:
    e = by_title.get(target)
    if e is None:
        problems.append((display, "no Wikidata match for the article title"))
        continue
    seat = sorted(e["seat"])
    state = sorted(e["state"])
    code = STATES.get(state[0]) if len(state) == 1 else None
    note = []
    if len(seat) != 1: note.append(f"seat={seat or 'missing'}")
    if len(state) != 1: note.append(f"state={state or 'missing'}")
    elif code is None: note.append(f"unknown state {state[0]}")
    if note: problems.append((display, "; ".join(note)))
    rows.append({"name": display, "city": seat[0] if len(seat) == 1 else "", "state": code or "", "target": target})

json.dump({"rows": rows, "problems": problems}, open("dioceses_built.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(f"links found: {len(found)} | built rows: {len(rows)} | with a problem: {len(problems)}")
print("\nPROBLEMS:")
for p in problems:
    print("  ", p[0], "->", p[1])
