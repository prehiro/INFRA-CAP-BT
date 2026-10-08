# Restart the Nuxt dev server cleanly.
#
# NUXT_APP_BASE_URL leaks across shell sessions on this machine and would make the router
# reject every route, so it is cleared explicitly before starting.
#
# NUXT_API_PROXY_TARGET is REQUIRED: nuxt.config.ts only wires the dev Vite proxy for /api
# when that variable is set. Without it the SPA still loads, but every /api request falls
# through to the SPA and returns index.html instead of JSON - which shows up as the login
# button silently doing nothing (a 200 carrying <!DOCTYPE html>, not a 401).
$env:NUXT_API_PROXY_TARGET = 'http://localhost:5099'
$env:NUXT_APP_BASE_URL = $null
Remove-Item Env:\NUXT_APP_BASE_URL -ErrorAction SilentlyContinue

$procs = Get-CimInstance Win32_Process -Filter "Name='node.exe'" |
         Where-Object { $_.CommandLine -like '*InternalApp*' }
foreach ($p in $procs) {
    Write-Host "killing node pid $($p.ProcessId)"
    Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 3
Write-Host "node processes remaining: $((Get-CimInstance Win32_Process -Filter ""Name='node.exe'"" | Where-Object { $_.CommandLine -like '*InternalApp*' }).Count)"

# Start detached so this script can exit while the server keeps running. Redirect to a log
# file rather than the console, because a Start-Process child inherits nothing useful here
# and we want the startup output to survive for the health check below.
$webDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Start-Process -FilePath 'cmd.exe' `
    -ArgumentList '/c', 'npm run dev > dev-server.log 2>&1' `
    -WorkingDirectory $webDir `
    -WindowStyle Hidden
Write-Host "dev server starting; log: $webDir\dev-server.log"
