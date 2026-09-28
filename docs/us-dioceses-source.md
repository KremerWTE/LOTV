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

## The diocesan map: county and town to diocese
So a parish can be placed from where it is, two more reference files ship with the app (`src/Lotv.Api/Data/Reference/`):

- **`us-county-dioceses.csv`**: all 3,146 U.S. counties (and Louisiana parishes, Alaska boroughs, Virginia independent cities) with
  the diocese covering each. Source: `counties_by_diocese.csv` of [kburchfiel/us_diocese_mapper](https://github.com/kburchfiel/us_diocese_mapper)
  (stated to be public domain), which assigns each county to a diocese from the published diocesan boundaries. Diocese names are
  matched to the list above; 26 counties are split between two dioceses and carry both (`AlsoIn`), so they are never guessed.
  Not covered: Puerto Rico, Guam, the Northern Marianas and American Samoa.
- **`us-place-dioceses.csv`**: about 22,800 towns (populated places of 300 people or more) and the diocese their county is in, kept only
  when every place with that name in the state is in the same diocese and none is in a split county. Built from
  [GeoNames](https://www.geonames.org/) populated places (CC BY 4.0), which give each place's county.
- **How it is used** (`DioceseMatcher.Find`): the diocese named in the file; else the diocese seated in the parish's city; else the
  county, if the file has a County column; else the town; else the only diocese in the state. A split county lists both dioceses for a
  person to choose. Sanity checks are in `DioceseGeographyTests`.
- **Known limits.** The county table is only as accurate as the boundaries it came from; a few dioceses cut through a county
  (the 26 flagged), and a parish very near a boundary should be checked. Towns under 300 people, and names shared by towns in
  different dioceses, fall back to the county column or a person's decision.
- **Refreshing:** re-download `counties_by_diocese.csv`, run `tools/us-dioceses/build_counties.py` then `build_places.py`.
