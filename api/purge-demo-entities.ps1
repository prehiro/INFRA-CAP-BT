# Deletes the master/transaction demo entities, keeping cctv_log_book.
# Order matters: fields are removed with their entity, records go via the soft-delete
# endpoint. Run list-entities.ps1 first to see what will be removed.
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5099/api'
$KEEP = 'cctv_log_book'


# Credentials come from the environment so nothing secret is committed to git:
#   $env:INFRA_ADMIN_PASSWORD = '<password>'    (PowerShell)
$AdminUser = if ($env:INFRA_ADMIN_USER) { $env:INFRA_ADMIN_USER } else { 'admin' }
$AdminPass = $env:INFRA_ADMIN_PASSWORD
if (-not $AdminPass) { throw 'Set $env:INFRA_ADMIN_PASSWORD before running this script.' }
$body = @{ username = $AdminUser; password = $AdminPass } | ConvertTo-Json -Compress
$login = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' -Body $body
$hdr = @{ Authorization = "Bearer $($login.token)" }
Write-Output "Login OK sebagai $($login.user.username)"

$ents = Invoke-RestMethod -Uri "$base/entities?all=true" -Headers $hdr
$doomed = @($ents | Where-Object { $_.slug -ne $KEEP })

Write-Output ""
Write-Output "AKAN DIHAPUS:"
foreach ($e in $doomed) {
  $page = Invoke-RestMethod -Uri "$base/records/$($e.id)?pageSize=1" -Headers $hdr
  Write-Output ("  {0,-16} {1} record" -f $e.name, $page.total)
}
Write-Output ""
Write-Output "DIPERTAHANKAN:"
foreach ($e in @($ents | Where-Object { $_.slug -eq $KEEP })) {
  Write-Output "  $($e.name)"
}
Write-Output ""

foreach ($e in $doomed) {
  # Records first: RecordValue rows reference the field, and fields reference the entity.
  $page = Invoke-RestMethod -Uri "$base/records/$($e.id)?pageSize=500" -Headers $hdr
  foreach ($r in $page.items) {
    Invoke-RestMethod -Uri "$base/records/$($e.id)/$($r.id)" -Method Delete -Headers $hdr | Out-Null
  }
  # Then the entity itself (cascade removes its DynamicField rows).
  Invoke-RestMethod -Uri "$base/entities/$($e.id)" -Method Delete -Headers $hdr | Out-Null
  Write-Output "  deleted $($e.name) (+ $($page.total) record)"
}

Write-Output ""
$after = Invoke-RestMethod -Uri "$base/entities?all=true" -Headers $hdr
Write-Output "SISA ENTITY: $($after.Count)"
foreach ($e in $after) { Write-Output "  $($e.name) ($($e.slug))" }