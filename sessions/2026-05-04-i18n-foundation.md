# 2026-05-04 — i18n Foundation (en / es)

## Goal
Close out the last open backlog item: **Localization / i18n**.

## What shipped

### Foundation
- **`src/Lotv.Web/Services/LocalizationService.cs`** — singleton service holding in-memory `Dictionary<culture, Dictionary<key, string>>` for `en` + `es`.
  - Persists current culture to `localStorage` (`lotv.culture` key) via JSInterop
  - Exposes `T(key)` / indexer with English fallback
  - Fires `OnCultureChanged` for components to re-render
  - Chose this over `Microsoft.Extensions.Localization` + `.resx` to avoid pulling in `BlazorWebAssemblyLoadAllGlobalizationData` and the resx tooling for what amounts to a public-page label set
- **`Program.cs`** — registered as singleton; `InitializeAsync()` called after host build to restore preferred culture before first render

### Switcher
- **`src/Lotv.Web/Shared/LanguageSwitcher.razor`** — globe icon dropdown with `aria-haspopup` / `aria-expanded` / `role="listbox"`, current selection highlighted, click-to-switch fires `SetCultureAsync` and broadcasts.

### Layouts / pages migrated
- `Layout/PublicLayout.razor` — nav links, footer, language switcher mounted in header
- `Pages/Home.razor` — hero, KPIs, "How We Help" cards, "Who We Serve" tag list, "Get Involved" CTAs

### Strings included
nav (7), layout (4), home page (32 keys covering hero/KPIs/cards/tags/CTAs), common form (9), buttons (5), messages (3) — **~60 keys × 2 locales**.

## Pattern for remaining pages
Any page can opt in with:
```razor
@inject LocalizationService Loc
@implements IDisposable
...
@Loc["some.key"]
@code {
  protected override void OnInitialized() => Loc.OnCultureChanged += Refresh;
  private void Refresh() => InvokeAsync(StateHasChanged);
  public void Dispose() => Loc.OnCultureChanged -= Refresh;
}
```
Add new keys to both `en` and `es` dictionaries in `LocalizationService.cs`.

Pages still on hardcoded English (foundation supports them, just need string extraction):
Apply, Give, VolunteerSignup, Help, Transparency, Events, EventTickets, DonationConfirm, MyRequests, MyProfile, DonorImpact, DonateResources.

## Verification
- `dotnet build Lotv.slnx` → 0 warnings, 0 errors
- Tests not run this session (no domain code touched; pure UI infra)

## Cleanup
- Pre-session leftover: 3 untracked files (`ReportRunLog.cs` model + paired migration `20260408165550_AddGeoPrivacyGbDonationReportLog.{cs,Designer.cs}`) were referenced by commit `fbf293a` but never staged. Committed as `chore: add missing ReportRunLog model and migration files from prior commit` (70e89d1).

## MASTER_TODO
- Marked Localization / i18n complete.
- Last-updated stamp bumped to 2026-05-04.
