# Session Notes — 2026-03-29
## A11y Audit, Leaflet Maps, Kanban Write-back, Email Wiring, Settings API

---

### What We Did

#### 1. WCAG 2.1 AA Accessibility Audit (commit `293f7b6`)
- Added `aria-pressed` to all 24 filter-chip buttons across 21 pages
- Added `aria-label` to close buttons (`Kanban.razor`, `Cases.razor`) and sign-out button (`AdminLayout.razor`)
- Added `aria-label` to search inputs (`Cases.razor`, `Help.razor`)
- Added `aria-expanded` + `aria-controls` to FAQ accordion in `Help.razor`
- Fixed `Allocations.razor` sed-damaged escaped quotes that caused `RZ1006`/`CS0103` errors

#### 2. Leaflet Geographic Maps + Kanban Write-back + Email Delivery (commit `db73c2f`)
- **Leaflet maps**: Added `lotv-map.js` (US state centroid lookup, circle markers), `MapMarker.cs` record, `LeafletMap.razor` component; wired to `ImpactDashboard.razor` and `DioceseData.razor`
- **Kanban write-back**: `SaveFromDrawer` now async, calls `Api.UpdateRequestStatusAsync` + `Api.AssignRequestAsync`; `_editAssignedTo` changed to `int` (volunteerId); optimistic local update
- **Email wiring**: `ReceiptService` and `ScheduledReportService` now call `INotificationService.SendEmailAsync` for receipts, year-end statements, daily digest, weekly summary
- **Dev config**: Populated `appsettings.Development.json` with JWT key, connection string, empty third-party keys

#### 3. MASTER_TODO Map Items Checked Off (commit `ab2b8af`)
- Unchecked → checked: Geographic Map on ImpactDashboard, Diocese Map on DioceseData

#### 4. Settings API + ScheduledReports Cleanup + MASTER_TODO Kanban Fix (commit `6014a5b`)
- **AppSetting model**: new key-value table (`ChapterId`, `Key`, `Value`), unique index, EF migration `AddAppSettings`
- **`GET/PUT /api/v1/settings`**: Staff-authenticated, chapter-scoped upsert
- **`ApiService`**: `GetSettingsAsync()` / `SaveSettingsAsync()`
- **`Settings.razor`**: loads settings on init, 4 Save buttons each write their own key subset (`org.*`, `case.*`, `notify.*`, `privacy.*`)
- **`ScheduledReports.razor`**: removed hardcoded `LastDailyAt`/`LastWeeklyAt` and fake `_logs` entries — honest empty state until real reports fire
- **MASTER_TODO**: Kanban entry updated — removed "API write TODO Phase 5" note

#### 5. Infrastructure Fixes (this commit)
- `src/Lotv.Web/Program.cs`: Fixed `HttpClient BaseAddress` from `https://localhost:7100` → `http://localhost:5275`
- `src/Lotv.Api/appsettings.Development.json`: Added `AllowedOrigins` array (`http://localhost:5205`, `https://localhost:7146`) so CORS allows the dev Web origin

---

### Remaining Code Work (pre-infrastructure)
| Item | Notes |
|---|---|
| `AutoAssignmentService` geo scoring | Needs lat/lng fields on `PackageRequest` + migration |
| Stripe webhook `Program.cs:2307` | Needs real Stripe account + Stripe.net SDK |
| `ScheduledReports` run-history log | Needs `ReportRunLog` table written by background service |

### Remaining Infrastructure Work (blocked on hosting decisions)
- Choose hosting (Azure App Service / Container Apps / Railway / AWS)
- Database hosting (Azure SQL / RDS / Supabase)
- Blob storage for receipts/docs
- Redis (optional)
- Secrets manager (Key Vault / Secrets Manager)
- CDN for Blazor WASM static assets
- Stripe account + webhook registration + real keys
- Alerts, uptime monitoring, DB backup strategy
- Domain + SSL + DNS
- Launch checklist sign-off

---

### Dev URLs (local)
- **Web (Blazor WASM):** `http://localhost:5205`
- **API:** `http://localhost:5275`

To start:
```bash
dotnet run --project src/Lotv.Api --launch-profile http &
# wait ~30s for API to fully start, then:
dotnet run --project src/Lotv.Web --launch-profile http &
```

---

### Branch
`kremer-dev` — push to `main` via PR when ready for staging deploy
