import csv,re,unicodedata,collections
rows=list(csv.DictReader(open("cbd.csv",encoding="utf-8")))
ours=list(csv.DictReader(open(r"D:\GitHub\LOTV\src\Lotv.Api\Data\Reference\us-dioceses.csv",encoding="utf-8")))
def key(s):
    s=re.sub(r"[\u2013\u2014\ufffd-]"," ",s)
    s=unicodedata.normalize("NFKD",s).encode("ascii","ignore").decode().lower()
    s=s.replace("st.","saint")
    s=re.sub(r"\b(roman catholic|archdiocese|diocese|of|the)\b"," ",s)
    return re.sub(r"[^a-z]","",s)
by_key=collections.defaultdict(list)
for o in ours: by_key[key(o["Name"])].append(o)
MANUAL={"Bismark":"Bismarck","Portland in Maine":"Portland","Portland":"Portland in Oregon","Toledo":"Toledo in Ohio","Venice":"Venice in Florida","Victoria in Texas":"Victoria"}
def resolve(their):
    m=MANUAL.get(their,their)
    st=None
    if isinstance(m,tuple): m,st=m
    cands=by_key.get(key(m),[])
    if st: cands=[c for c in cands if c["State"]==st] or cands
    return cands[0] if len(cands)>=1 else None
cache={}
def R(t):
    if t not in cache: cache[t]=resolve(t)
    return cache[t]
def county_norm(n):
    n=unicodedata.normalize("NFKD",n).encode("ascii","ignore").decode().lower()
    n=re.sub(r"\s+(county|parish|borough|census area|municipality)$","",n)
    n=re.sub(r"\bsaint\b","st",n).replace("st.","st")
    return re.sub(r"[^a-z ]","",n).strip()
out=[];unres=collections.Counter()
for r in rows:
    prim=R(r["Diocese"])
    if not prim: unres[r["Diocese"]]+=1; continue
    also=""
    parts=[p.strip() for p in r["Diocese_Detail"].split(",")] if r["Diocese_Detail"]!=r["Diocese"] else []
    others=[p for p in parts if p and p!=r["Diocese"]]
    if others:
        o=R(others[0]) or None
        also=o["Name"] if o else ""
    out.append((r["State_Code"],county_norm(r["NAMELSAD"]),prim["Name"],prim["State"],also))
out.sort()
with open(r"D:\GitHub\LOTV\src\Lotv.Api\Data\Reference\us-county-dioceses.csv","w",encoding="utf-8",newline="") as f:
    w=csv.writer(f); w.writerow(["State","County","Diocese","DioceseState","AlsoIn"]); w.writerows(out)
print(len(out),"counties written; unresolved:",dict(unres)); print(sum(1 for o in out if o[4]),"shared counties")
used={o[2] for o in out}; print("our dioceses with no county:",[o["Name"] for o in ours if o["Name"] not in used])
