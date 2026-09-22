# StopApp.ps1 - Kill any running LOTV API and Web processes.

Write-Host "Stopping LOTV API and Web processes..." -ForegroundColor Yellow

Get-Process -Name "Lotv.Api" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "Lotv.Web" -ErrorAction SilentlyContinue | Stop-Process -Force

# Catch any dotnet run processes hosting either project
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
    $_.MainWindowTitle -eq ""
} | ForEach-Object {
    $cmdLine = (Get-CimInstance Win32_Process -Filter "ProcessId=$($_.Id)" -ErrorAction SilentlyContinue).CommandLine
    if ($cmdLine -match "Lotv\.(Api|Web)") {
        $_ | Stop-Process -Force -ErrorAction SilentlyContinue
    }
}

Start-Sleep 1
Write-Host "Done." -ForegroundColor Green
