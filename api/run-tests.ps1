$cfg = Get-Content "$PSScriptRoot\appsettings.json" -Raw | ConvertFrom-Json
$env:INFRA_ADMIN_USER = $cfg.Seed.AdminUsername
$env:INFRA_ADMIN_PASSWORD = $cfg.Seed.AdminPassword
Write-Host "--- test-logbook ---"
& "$PSScriptRoot\test-logbook.ps1"
$log = $LASTEXITCODE
Write-Host "--- test-e2e ---"
& "$PSScriptRoot\test-e2e.ps1"
$e2e = $LASTEXITCODE
Write-Host "RESULT logbook=$log e2e=$e2e"
