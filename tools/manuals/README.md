# Manuals builder

Generates the eight step-by-step manuals in `docs/manuals/` by driving the **real running app** with Playwright, so every
screenshot and every instruction come from the same run. When the UI changes, re-run and both regenerate.

```
docs/manuals/src/*.md        <- editable Markdown source (generated; commit it)
docs/manuals/images/<slug>/  <- numbered screenshots, one folder per manual
docs/manuals/*.docx, *.pdf   <- built from the Markdown
```

## How a step is made

Each spec in `specs/` calls `Recorder.step(...)`, which (1) scrolls to the target and draws a numbered red box around it,
(2) takes the screenshot, (3) performs the action, (4) writes the instruction, the picture and a "You should see" line.
The red number matches the step number in the text.

## Run it

Requires Python 3.11 with `playwright`, `python-docx`, `markdown`, `Pillow`, `requests`, plus `playwright install chromium`.

```powershell
# 1. Fresh throwaway database (SQLite, outside the repo) + QA sample data + demo accounts, API and Web started
pwsh tools/manuals/reset_env.ps1 -Scratch D:\Temp\lotv-manuals
# 2. Take the clean snapshot that every spec restores before it runs
pwsh tools/manuals/fast_reset.ps1 -Scratch D:\Temp\lotv-manuals -Snapshot
# 3. Regenerate everything (restores the snapshot before each manual) and build docx + pdf
$env:LOTV_MANUALS_SCRATCH = "D:\Temp\lotv-manuals"
python tools/manuals/run_all.py            # or:  python tools/manuals/run_all.py m03 m05
```

Single manual while iterating: `pwsh tools/manuals/fast_reset.ps1 -Scratch ...; python tools/manuals/specs/m03_pack.py; python tools/manuals/build.py 03`

Always restore the snapshot between runs: the specs create and change real records (a volunteer record, a case's status,
a submitted request), so a second run on the same data will not match the text.

## Demo accounts (password `DevPassword1!`)

| Account | Role | Used by |
|---|---|---|
| `claire.hoffman` | ChapterStaff, Chicago Metro | 01, 05, 07, 08 |
| `chris.kremer` | HQAdmin | 04 (and setup) |
| `pat.packer@example.org` | Volunteer (no record yet) | 01, 03 |
| `penny.prayer@example.org` | Volunteer (no record yet) | 02 |
| `bea.board@example.org` | Board | 06 |
| `nora.newhire@example.org` | ChapterStaff | 04 (temporary-password demo) |
| `sam.staff@example.org` | ChapterStaff | 04 (Login As demo) |

## Things to know

- Screenshots hide dev-only furniture (the login page's demo-credentials hint, the "N migrations" badge, live toast pop-ups) — see `scrub()` in `lib.py`.
- Native `<select>` dropdowns do not paint in headless Chrome, so `show_options()` expands one inline for the picture.
- Blazor text boxes update on blur; specs press Tab after typing where a button depends on the text.
- `recon.py <user> <path>...` dumps the headings, buttons and fields a role sees on a page — handy when writing a new spec.
- Practice data is the app's own **QA Sample Data** (Admin → QA Sample Data), so nothing here is a real family.
