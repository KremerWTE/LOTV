# Lotv.E2E — Playwright End-to-End Tests

Browser-based E2E tests using [Microsoft.Playwright](https://playwright.dev/dotnet/) and xUnit.

## Prerequisites

1. **Install Playwright browsers** (one-time, after `dotnet restore`):
   ```bash
   dotnet build tests/Lotv.E2E/Lotv.E2E.csproj
   pwsh tests/Lotv.E2E/bin/Debug/net9.0/playwright.ps1 install --with-deps chromium
   ```
   On Linux/CI (no PowerShell):
   ```bash
   dotnet tool install --global Microsoft.Playwright.CLI
   playwright install --with-deps chromium
   ```

2. **Start both apps** in separate terminals:
   ```bash
   # Terminal 1 — API (port 5000)
   dotnet run --project src/Lotv.Api

   # Terminal 2 — Web frontend (port 5001)
   dotnet run --project src/Lotv.Web
   ```

## Running Tests

```bash
# All E2E tests (headless Chromium, default)
dotnet test tests/Lotv.E2E/Lotv.E2E.csproj

# Watch the browser (disable headless)
E2E_HEADLESS=false dotnet test tests/Lotv.E2E/Lotv.E2E.csproj

# Run against staging
E2E_BASE_URL=https://staging.lotv.app E2E_API_URL=https://api-staging.lotv.app \
  dotnet test tests/Lotv.E2E/Lotv.E2E.csproj

# Slow-motion debug (500ms per action)
E2E_HEADLESS=false E2E_SLOW_MO=500 dotnet test tests/Lotv.E2E/Lotv.E2E.csproj

# Run a single test class
dotnet test tests/Lotv.E2E/Lotv.E2E.csproj --filter "FullyQualifiedName~PublicPagesTests"
```

## Environment Variables

| Variable | Default | Description |
|---|---|---|
| `E2E_BASE_URL` | `http://localhost:5001` | Blazor WASM frontend URL |
| `E2E_API_URL` | `http://localhost:5000` | API backend URL |
| `E2E_HEADLESS` | `true` | Set to `false` to show browser window |
| `E2E_SLOW_MO` | `0` | Milliseconds delay between Playwright actions |

## Test Structure

```
tests/Lotv.E2E/
├── Infrastructure/
│   ├── BrowserFixture.cs       — xUnit collection fixture, one browser per run
│   ├── E2ESettings.cs          — environment-driven config
│   └── E2ETestBase.cs          — base class: context/page lifecycle + helpers
└── Tests/
    ├── PublicPagesTests.cs      — smoke tests for all public routes
    ├── AuthFlowTests.cs         — login, logout, protected-route redirect
    ├── ApplyFlowTests.cs        — service request application form
    ├── DonationFlowTests.cs     — Give page and recurring donation flow
    ├── VolunteerFlowTests.cs    — volunteer signup and onboarding wizard
    ├── AdminPagesTests.cs       — all admin pages (login required)
    ├── MobileResponsivenessTests.cs — 390×844 viewport, no horizontal scroll
    └── AccessibilityTests.cs   — WCAG 2.1 AA checks (alt text, labels, headings)
```

## CI Integration

The CI workflow (`.github/workflows/ci.yml`) runs unit + integration tests automatically.
E2E tests are intentionally **not** part of the default CI run because they require both
apps to be running. Add a separate `e2e.yml` workflow when deploying to a persistent
staging environment:

```yaml
- name: Start API
  run: dotnet run --project src/Lotv.Api &
- name: Start Web
  run: dotnet run --project src/Lotv.Web &
- name: Wait for apps
  run: sleep 15
- name: Run E2E tests
  run: dotnet test tests/Lotv.E2E/Lotv.E2E.csproj
  env:
    E2E_BASE_URL: http://localhost:5001
    E2E_API_URL: http://localhost:5000
```

## Admin Test Accounts

Admin tests use the dev seed credentials. These only exist when the API is running
in `Development` mode with `Testing:SkipSeed` not set:

| Role | Username | Password |
|---|---|---|
| HQ Admin | `mary.roberts` | `DevPassword1!` |
| Chapter Staff | `claire.hoffman` | `DevPassword1!` |
