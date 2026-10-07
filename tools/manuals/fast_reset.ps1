# Restore the seeded DB snapshot and restart only the API (about 15 s). Use before re-running any spec.
#   pwsh tools/manuals/fast_reset.ps1 -Snapshot   # take the snapshot (after reset_env.ps1 has seeded everything)
#   pwsh tools/manuals/fast_reset.ps1             # restore it
param([string]$Scratch = "$env:TEMP\lotv-manuals", [switch]$Snapshot)
$root = Resolve-Path "$PSScriptRoot\..\.."
if ($Snapshot) {
    # SQLite keeps recent writes in manuals.db-wal; fold them into the main file before copying it.
    python -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); c.execute('pragma wal_checkpoint(truncate)'); c.close()" "$Scratch\manuals.db"
}
Get-Process -Name "Lotv.Api" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 2
if ($Snapshot) { Copy-Item "$Scratch\manuals.db" "$Scratch\manuals.seed.db" -Force }
else { Remove-Item "$Scratch\manuals.db-*" -Force -ErrorAction SilentlyContinue; Copy-Item "$Scratch\manuals.seed.db" "$Scratch\manuals.db" -Force }
$env:ASPNETCORE_ENVIRONMENT='Development'; $env:Database__Provider='Sqlite'
$env:ConnectionStrings__DefaultConnection="Data Source=$Scratch\manuals.db"; $env:QaSample__AutoLoad='true'
Start-Process dotnet -ArgumentList 'run','--project',"$root\src\Lotv.Api",'--launch-profile','http','--no-build' -RedirectStandardOutput "$Scratch\api.out" -RedirectStandardError "$Scratch\api.err" -WindowStyle Hidden
for ($i=0; $i -lt 60; $i++) { Start-Sleep 1; try { if ((Invoke-WebRequest http://localhost:5100/health -UseBasicParsing).StatusCode -eq 200) { break } } catch {} }
Write-Host "API ready ($(if ($Snapshot) {'snapshot taken'} else {'restored'}))"
