# StartApp.ps1 - Kill any existing LOTV processes, build, then launch API and Web in separate windows.
# Usage: .\scripts\dev\StartApp.ps1 [-NoBuild] [-NoOpen]

param(
    [switch]$NoBuild,
    [switch]$NoOpen
)

$RepoRoot   = (Resolve-Path "$PSScriptRoot\..\..")
$ApiProject = Join-Path $RepoRoot "src\Lotv.Api"
$WebProject = Join-Path $RepoRoot "src\Lotv.Web"
$WebUrl     = "http://localhost:5101"

# Kill any existing instances
Write-Host "Stopping any existing LOTV processes..." -ForegroundColor Yellow
Get-Process -Name "Lotv.Api"  -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "Lotv.Web"  -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 1

if (-not $NoBuild) {
    Write-Host "Building solution..." -ForegroundColor Cyan
    & dotnet build "$RepoRoot\Lotv.slnx" --configuration Debug 2>&1 | Select-Object -Last 10 | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build FAILED - aborting." -ForegroundColor Red
        exit 1
    }
    Write-Host "Build succeeded." -ForegroundColor Green
}

# Launch API in a new window
Write-Host "Launching Lotv.Api on http://localhost:5100 ..." -ForegroundColor Cyan
$apiCmd = "Write-Host 'LOTV API - close this window or press Ctrl+C to stop.' -ForegroundColor Cyan; " +
          "`$env:ASPNETCORE_ENVIRONMENT = 'Development'; " +
          "& dotnet run --project '$ApiProject' --launch-profile http --no-build; " +
          "Write-Host 'API stopped. Press Enter to close.' -ForegroundColor Yellow; Read-Host"
Start-Process powershell -ArgumentList "-NoExit", "-Command", $apiCmd

# Brief pause to let the API start before Web tries to connect
Start-Sleep 3

# Launch Web in a new window
Write-Host "Launching Lotv.Web on $WebUrl ..." -ForegroundColor Cyan
$webCmd = "Write-Host 'LOTV Web - close this window or press Ctrl+C to stop.' -ForegroundColor Cyan; " +
          "`$env:ASPNETCORE_ENVIRONMENT = 'Development'; " +
          "& dotnet run --project '$WebProject' --launch-profile http --no-build; " +
          "Write-Host 'Web stopped. Press Enter to close.' -ForegroundColor Yellow; Read-Host"
Start-Process powershell -ArgumentList "-NoExit", "-Command", $webCmd

if (-not $NoOpen) {
    Start-Sleep 8
    Start-Process $WebUrl
}

Write-Host "Done. Both LOTV API and Web are running." -ForegroundColor Green
