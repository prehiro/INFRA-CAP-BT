# Dry-run: list entity + record count BEFORE deleting anything.
$base = 'http://localhost:5099/api'

# Credentials come from the environment so nothing secret is committed to git:
#   $env:INFRA_ADMIN_PASSWORD = '<password>'    (PowerShell)
$AdminUser = if ($env:INFRA_ADMIN_USER) { $env:INFRA_ADMIN_USER } else { 'admin' }
$AdminPass = $env:INFRA_ADMIN_PASSWORD
if (-not $AdminPass) { throw 'Set $env:INFRA_ADMIN_PASSWORD before running this script.' }
$body = @{ username = $AdminUser; password = $AdminPass } | ConvertTo-Json -Compress
$login = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' -Body $body
$hdr = @{ Authorization = "Bearer $($login.token)" }

$ents = Invoke-RestMethod -Uri "$base/entities?all=true" -Headers $hdr
Write-Output "TOTAL ENTITY: $($ents.Count)"
Write-Output ""
foreach ($e in $ents) {
  $page = Invoke-RestMethod -Uri "$base/records/$($e.id)?pageSize=1" -Headers $hdr
  Write-Output ("{0,-16} slug={1,-20} kind={2,-12} fields={3,-3} records={4}" -f $e.name, $e.slug, $e.kind, $e.fields.Count, $page.total)
}