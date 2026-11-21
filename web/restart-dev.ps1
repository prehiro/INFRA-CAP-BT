<#
    restart-dev.ps1 - restart the INFRA-CAP dev stack on this machine.

    WHY THIS SCRIPT CHECKS THE API TOO
    Login used to fail with "Cannot reach the server. Check your network connection or contact the
    administrator." after every restart. The cause was never Nuxt: the Vite proxy got ECONNREFUSED on
    /api/auth/login because the .NET API on :5099 was not running (it dies with the terminal that
    started it, or with the machine). The web server was up and answering 200, so the page loaded fine
    and only the login button failed - which looks exactly like a broken login page.

    The fix: bring the API up if it is missing, then PROVE the whole chain before declaring success -
    page 200 on :3000 AND /api/auth/me answering 401 through the proxy (a 200 carrying index.html
    means the proxy is not wired, which fails the same way at login).

    WHAT ELSE IT SETS, AND WHY
    NUXT_API_PROXY_TARGET - required. nuxt.config.ts only wires the dev Vite proxy for /api when this
                            is set; without it /api falls through to the SPA and answers index.html
                            with HTTP 200, so login throws while parsing JSON and shows the same error.
    NUXT_APP_BASE_URL     - cleared. It leaks across shell sessions here and makes the router reject
                            every route when it holds a production base.
    NUXT_IGNORE_LOCK      - set for the dev server only. A killed Nuxt leaves a stale dev lock that
                            blocks the next start ("Another Nuxt dev server is already running").

    Usage:
        powershell -ExecutionPolicy Bypass -File .\restart-dev.ps1
        powershell -ExecutionPolicy Bypass -File .\restart-dev.ps1 -WebOnly   # leave the API alone
#>
[CmdletBinding()]
param(
    # Restart only Nuxt; skip the API check/start (useful when the API is debugged separately).
    [switch]$WebOnly
)

$ErrorActionPreference = 'Stop'

$webDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $webDir
$apiDir  = Join-Path $rootDir 'api'
$webUrl  = 'http://localhost:3000'
$apiBase = 'http://localhost:5099'
$devLog  = Join-Path $webDir 'dev-server.log'
$devErr   = Join-Path $webDir 'dev-server.err'
$apiOut  = Join-Path $apiDir 'dev-api.out'
$apiErr  = Join-Path $apiDir 'dev-api.err'

function Write-Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Write-Ok($m)   { Write-Host "    [ok] $m" -ForegroundColor Green }
function Write-Note($m) { Write-Host "    [..] $m" -ForegroundColor Yellow }

# Any HTTP status means "something answered". 0 means the connection itself failed.
function Get-HttpStatus {
    param([string]$Url)
    try {
        $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 4
        return [int]$r.StatusCode
    } catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return 0
    }
}

function Wait-For {
    param([string]$Url, [int]$Expected, [int]$TimeoutSec)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if ((Get-HttpStatus -Url $Url) -eq $Expected) { return $true }
        Start-Sleep -Milliseconds 700
    }
    return $false
}

# ---------------------------------------------------------------- 1. environment
Write-Step 'Configuring environment'
$env:NUXT_API_PROXY_TARGET = $apiBase
if (-not $env:ASPNETCORE_ENVIRONMENT) { $env:ASPNETCORE_ENVIRONMENT = 'Development' }
Remove-Item Env:\NUXT_APP_BASE_URL -ErrorAction SilentlyContinue
Write-Ok "NUXT_API_PROXY_TARGET = $apiBase"

# ---------------------------------------------------------------- 2. API
if (-not $WebOnly) {
    Write-Step "Checking the API on $apiBase"
    $apiStatus = Get-HttpStatus -Url "$apiBase/api/auth/me"
    if ($apiStatus -ne 0) {
        Write-Ok "API already running (HTTP $apiStatus)"
    } else {
        Write-Note 'API not answering - starting it'
        $apiDll = Join-Path $apiDir 'bin/Debug/net10.0/Api.dll'
        Remove-Item $apiOut, $apiErr -ErrorAction SilentlyContinue
        if (Test-Path $apiDll) {
            # Run the already-built DLL: no restore/build, so the API is up in a couple of seconds.
            Start-Process -FilePath 'dotnet.exe' `
                -ArgumentList @("`"$apiDll`"", '--urls', $apiBase) `
                -WorkingDirectory $apiDir -WindowStyle Hidden `
                -RedirectStandardOutput $apiOut -RedirectStandardError $apiErr | Out-Null
        } else {
            Start-Process -FilePath 'dotnet.exe' `
                -ArgumentList @('run', '--no-launch-profile', '--urls', $apiBase) `
                -WorkingDirectory $apiDir -WindowStyle Hidden `
                -RedirectStandardOutput $apiOut -RedirectStandardError $apiErr | Out-Null
        }
        if (Wait-For -Url "$apiBase/api/auth/me" -Expected 401 -TimeoutSec 90) {
            Write-Ok "API ready on $apiBase"
        } else {
            Write-Host "    [x] API did not come up within 90s. Log: $apiErr" -ForegroundColor Red
            if (Test-Path $apiErr) { Get-Content $apiErr -Tail 20 | ForEach-Object { Write-Host "      $_" } }
            exit 1
        }
    }
}

# ---------------------------------------------------------------- 3. stop Nuxt
Write-Step 'Stopping the running dev server'
$procs = Get-CimInstance Win32_Process -Filter "Name='node.exe'" |
         Where-Object { $_.CommandLine -like '*InternalApp*' }
foreach ($p in $procs) {
    Write-Host "    killing node pid $($p.ProcessId)"
    Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
}
if ($procs.Count -gt 0) { Start-Sleep -Seconds 2 }

# The port stays bound for a moment after the process dies. Starting Nuxt on a busy 3000 makes it
# silently pick 3001, which in the browser is indistinguishable from an unreachable server.
$portDeadline = (Get-Date).AddSeconds(20)
while ((Get-Date) -lt $portDeadline) {
    if ((Get-HttpStatus -Url "$webUrl/") -eq 0) { break }
    Start-Sleep -Milliseconds 700
}
$remaining = (Get-CimInstance Win32_Process -Filter "Name='node.exe'" |
              Where-Object { $_.CommandLine -like '*InternalApp*' }).Count
Write-Ok "node processes remaining: $remaining"

# ---------------------------------------------------------------- 4. start Nuxt
Write-Step 'Starting the dev server'
Remove-Item $devLog, $devErr -ErrorAction SilentlyContinue
$env:NUXT_IGNORE_LOCK = '1'   # a stale lock from the kill above would otherwise block this start
# Redirect at the Start-Process level, not with `> file` inside cmd: a shell redirect only covers
# npm, so cmd.exe itself keeps this console's stdout handle open - and any caller piping this script's
# output (or a CI runner) then never sees EOF and waits for the dev server to exit.
Start-Process -FilePath 'cmd.exe' `
    -ArgumentList '/c', 'npm run dev' `
    -WorkingDirectory $webDir `
    -WindowStyle Hidden `
    -RedirectStandardOutput $devLog `
    -RedirectStandardError $devErr | Out-Null

# ---------------------------------------------------------------- 5. prove it works
Write-Step 'Waiting for the dev server'
if (-not (Wait-For -Url "$webUrl/" -Expected 200 -TimeoutSec 150)) {
    Write-Host "    [x] $webUrl never answered 200. Last log lines:" -ForegroundColor Red
    if (Test-Path $devLog) { Get-Content $devLog -Tail 30 | ForEach-Object { Write-Host "      $_" } }
    exit 1
}
Write-Ok "page answering 200 on $webUrl"

# The proxy is the part that actually broke login, so check it explicitly: 401 JSON proves the
# request reached the API instead of falling through to the SPA's index.html (which answers 200).
$proxyOk = $false
$proxyDeadline = (Get-Date).AddSeconds(45)
while ((Get-Date) -lt $proxyDeadline) {
    if ((Get-HttpStatus -Url "$webUrl/api/auth/me") -eq 401) { $proxyOk = $true; break }
    Start-Sleep -Milliseconds 700
}
if ($proxyOk) {
    Write-Ok 'proxy /api -> API verified (401 on /api/auth/me)'
} else {
    Write-Host '    [x] /api/auth/me did not answer 401 through the proxy.' -ForegroundColor Red
    Write-Host '        A 200 carrying index.html here means NUXT_API_PROXY_TARGET did not reach the dev server.' -ForegroundColor Red
    if (Test-Path $devLog) { Get-Content $devLog -Tail 30 | ForEach-Object { Write-Host "      $_" } }
    exit 1
}

Write-Host ''
Write-Host "Ready. Open $webUrl and log in." -ForegroundColor Green
Write-Host "  web log: $devLog"
Write-Host "  api log: $apiOut"
Write-Host ''