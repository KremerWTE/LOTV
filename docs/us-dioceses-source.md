# The US diocese list (`src/Lotv.Api/Data/Reference/us-dioceses.csv`)

185 territorial (arch)dioceses: name, seat city, state or territory code. It exists so every parish can belong to a diocese
(see the Parish & Diocese Directory page, "Add the U.S. diocese list"). Loaded dioceses are marked *directory only* and are
never counted in "dioceses reached".

## Sources
- **Which dioceses exist:** the current territorial sections of Wikipedia's
  [List of Catholic dioceses in the United States](https://en.wikipedia.org/wiki/List_of_Catholic_dioceses_in_the_United_States)
  (text under CC BY-SA 4.0). Retrieved 2026-09-25.
- **Seat city and state:** [Wikidata](https://www.wikidata.org) (CC0), looked up per diocese by its Wikipedia article title,
  following the seat up to its state (`tools/us-dioceses/seat-and-state.sparql`).
- The USCCB's own "Bishops and Dioceses" page is the official list, but it blocks scripted access, so it was not fetched.

## Checked by hand
Territories, the federal district and four articles whose title differs from their link are set explicitly, and the seat is
the **cathedral city** where Wikidata gives the chancery city, a county or the state (Boston not Braintree, Baker City not Bend,
Saint Paul, New York, Arlington, Palm Beach Gardens, San Bernardino). Per-state counts were compared with the real map
(e.g. Texas 15, California 12, Illinois 6, New York 8, Pennsylvania 8).

## Not included
Eastern-rite eparchies and archeparchies (about 18), the Archdiocese for the Military Services and the Personal Ordinariate.
They can be added the same way.

## Refreshing it
1. Save the page's raw wikitext as `dioceses.wiki` (`.../w/index.php?title=List_of_Catholic_dioceses_in_the_United_States&action=raw`).
2. Run the SPARQL query at https://query.wikidata.org and save the JSON result as `wd3.json`.
3. Run `python build_dioceses2.py` (it reads `build_dioceses.py` for the state table), review anything it flags, and copy
   `us-dioceses.csv` over the reference file.

## What this list can and cannot do
It gives each parish a diocese when the parish list names one, when the parish is in a diocese's **seat city**, or when its state
has **only one diocese**. A parish in any other town of a state with several dioceses cannot be placed by city and state alone
(that needs a county-to-diocese map); the import lists those for a person to decide and never guesses.
