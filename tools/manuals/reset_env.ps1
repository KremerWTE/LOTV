# Rebuild a clean local environment for capturing manuals: fresh throwaway SQLite DB + QA sample data + demo personas.
# Usage: pwsh tools/manuals/reset_env.ps1 [-Scratch <dir>] [-NoBuild]
param([string]$Scratch = "$env:TEMP\lotv-manuals", [switch]$NoBuild)
$root = Resolve-Path "$PSScriptRoot\..\.."
New-Item -ItemType Directory -Force $Scratch | Out-Null
Get-Process -Name "Lotv.Api","Lotv.Web" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 2
Remove-Item "$Scratch\manuals.db*" -Force -ErrorAction SilentlyContinue
if (-not $NoBuild) { dotnet build "$root\Lotv.slnx" -c Debug -v q | Select-Object -Last 3 }
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:Database__Provider='Sqlite'
$env:ConnectionStrings__DefaultConnection="Data Source=$Scratch\manuals.db"
$env:QaSample__AutoLoad='true'
Start-Process dotnet -ArgumentList 'run','--project',"$root\src\Lotv.Api",'--launch-profile','http','--no-build' -RedirectStandardOutput "$Scratch\api.out" -RedirectStandardError "$Scratch\api.err" -WindowStyle Hidden
for ($i=0; $i -lt 60; $i++) { Start-Sleep 2; try { if ((Invoke-WebRequest http://localhost:5100/health -UseBasicParsing).StatusCode -eq 200) { break } } catch {} }
Start-Process dotnet -ArgumentList 'run','--project',"$root\src\Lotv.Web",'--launch-profile','http','--no-build' -RedirectStandardOutput "$Scratch\web.out" -RedirectStandardError "$Scratch\web.err" -WindowStyle Hidden
for ($i=0; $i -lt 60; $i++) { Start-Sleep 2; try { if ((Invoke-WebRequest http://localhost:5101/ -UseBasicParsing).StatusCode -eq 200) { break } } catch {} }
python "$PSScriptRoot\seed.py"
