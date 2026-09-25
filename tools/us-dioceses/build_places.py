import csv, io, re, zipfile, collections, sys
sys.stdout.reconfigure(encoding="utf-8")

# county (state, county FIPS) -> (diocese, dioceseState, shared?)
county = {}
cbd = list(csv.DictReader(open("cbd.csv", encoding="utf-8")))
ref = {(r["State"], r["County"]): r for r in csv.DictReader(open(r"D:\GitHub\LOTV\src\Lotv.Api\Data\Reference\us-county-dioceses.csv", encoding="utf-8"))}


def county_norm(n):
    import unicodedata
    n = unicodedata.normalize("NFKD", n).encode("ascii", "ignore").decode().lower()
    n = re.sub(r"\s+(county|parish|borough|census area|municipality)$", "", n)
    n = re.sub(r"\bsaint\b", "st", n).replace("st.", "st")
    return re.sub(r"[^a-z ]", "", n).strip()


for r in cbd:
    row = ref.get((r["State_Code"], county_norm(r["NAMELSAD"])))
    if row:
        county[(r["State_Code"], r["COUNTYFP"])] = (row["Diocese"], row["DioceseState"], bool(row["AlsoIn"]))


def norm_place(s):
    s = re.sub(r"[^a-z0-9 ]", "", s.lower())
    return " ".join("st" if w == "saint" else w for w in s.split())


MINPOP = int(sys.argv[1]) if len(sys.argv) > 1 else 300
CODES = {"PPL", "PPLA", "PPLA2", "PPLA3", "PPLA4", "PPLC", "PPLS"}
places = collections.defaultdict(set)   # (state, place) -> {(diocese, dioceseState)} ; None marks an unusable county
seen = 0
z = zipfile.ZipFile("geo_us.zip")
for line in io.TextIOWrapper(z.open("US.txt"), encoding="utf-8"):
    p = line.rstrip("\n").split("\t")
    if p[6] != "P" or p[7] not in CODES:
        continue
    if int(p[14] or 0) < MINPOP:
        continue
    st, fp = p[10], p[11]
    hit = county.get((st, fp))
    seen += 1
    key = (st, norm_place(p[2] or p[1]))
    if not key[1]:
        continue
    if hit is None or hit[2]:
        places[key].add(None)     # unknown or split county: the name can't be trusted for this state
    else:
        places[key].add((hit[0], hit[1]))

good = {k: next(iter(v)) for k, v in places.items() if len(v) == 1 and None not in v}
print("populated places read:", seen, "| distinct names:", len(places), "| unambiguous:", len(good))
rows = sorted((k[0], k[1], v[0], v[1]) for k, v in good.items())
with open(r"D:\GitHub\LOTV\src\Lotv.Api\Data\Reference\us-place-dioceses.csv", "w", encoding="utf-8", newline="") as f:
    w = csv.writer(f)
    w.writerow(["State", "Place", "Diocese", "DioceseState"])
    w.writerows(rows)
import os
print("file bytes:", os.path.getsize(r"D:\GitHub\LOTV\src\Lotv.Api\Data\Reference\us-place-dioceses.csv"))
for k in [("IL", "naperville"), ("IL", "oak park"), ("IL", "springfield"), ("TX", "katy"), ("NY", "yonkers"), ("ID", "island park")]:
    print(k, good.get(k))
