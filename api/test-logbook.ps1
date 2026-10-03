# E2E test for the CCTV Log Book feature.
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5099/api'
$pass = 0; $fail = 0

function Check($name, $cond, $extra = '') {
  if ($cond) { $script:pass++; Write-Output "  PASS  $name" }
  else { $script:fail++; Write-Output "  FAIL  $name $extra" }
}

# --- login ---

# Credentials come from the environment so nothing secret is committed to git:
#   $env:INFRA_ADMIN_PASSWORD = '<password>'    (PowerShell)
$AdminUser = if ($env:INFRA_ADMIN_USER) { $env:INFRA_ADMIN_USER } else { 'admin' }
$AdminPass = $env:INFRA_ADMIN_PASSWORD
if (-not $AdminPass) { throw 'Set $env:INFRA_ADMIN_PASSWORD before running this script.' }
$body = @{ username = $AdminUser; password = $AdminPass } | ConvertTo-Json -Compress
$login = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' -Body $body
$hdr = @{ Authorization = "Bearer $($login.token)" }
Write-Output "Login OK sebagai $($login.user.username)"

# Leftovers from an interrupted run would shift every sequence assertion below, so the
# table is emptied first. This test owns this entity exclusively.
$ents0 = Invoke-RestMethod -Uri "$base/entities?all=true" -Headers $hdr
$cctv0 = $ents0 | Where-Object { $_.slug -eq 'cctv_log_book' }
if ($cctv0) {
  $leftover = Invoke-RestMethod -Uri "$base/records/$($cctv0.id)?pageSize=500" -Headers $hdr
  foreach ($r in $leftover.items) {
    Invoke-RestMethod -Uri "$base/records/$($cctv0.id)/$($r.id)" -Method Delete -Headers $hdr | Out-Null
  }
  Write-Output "Cleanup: $($leftover.total) baris sisa dihapus"
}

# --- entity seeded ---
$ents = Invoke-RestMethod -Uri "$base/entities?all=true" -Headers $hdr
$cctv = $ents | Where-Object { $_.slug -eq 'cctv_log_book' }
Check 'Entity cctv_log_book ter-seed' ($null -ne $cctv) "slug tidak ditemukan"
if ($null -eq $cctv) { Write-Output "GAGAL: entity tidak ada"; exit 1 }
Check '12 field terpasang' ($cctv.fields.Count -eq 12) "actual=$($cctv.fields.Count)"
Check 'Kind = Transaction' ($cctv.kind -eq 'Transaction') "actual=$($cctv.kind)"

# --- next-no, plain integer (1, 2, 3, ...). Format changed from 1/2026/001 on 2026-10-02.
$year = (Get-Date).Year
$n1 = (Invoke-RestMethod -Uri "$base/logbook/cctv/next-no" -Headers $hdr).nomor
Check 'Next-no format integer' ($n1 -match '^\d+$') "actual=$n1"
Check 'Next-no mulai dari angka > 0' ([int]$n1 -gt 0) "actual=$n1"

# --- create a row (mirrors row 1 of the paper form) ---
$body = @{
  values = @{
    nomor = $n1
    tanggal = "$year-01-20T00:00:00Z"
    departemen = 'ISD'
    no_pegawai = '940900'
    nama_pemohon = 'Dedi Winoto'
    tujuan = 'CCTV record at 15 Jan 2022, from time 07.00am - 08.00am'
    waktu_diminta = '09.00am'
    tanda_pemohon = $null
    pic_isd = 'Pasrama'
    pic_mulai = '21 Jan 2022 | 09.30am'
    pic_selesai = '21 Jan 2022 | 10.00am'
    tanda_isd = $null
  }
} | ConvertTo-Json -Depth 5
$created = Invoke-RestMethod -Uri "$base/records/$($cctv.id)" -Method Post -Headers $hdr `
  -ContentType 'application/json' -Body $body
Check 'Create row sukses' ($created.id -gt 0) "id=$($created.id)"
Check 'Nilai tujuan tersimpan' ($created.values.tujuan -like '*07.00am*') "actual=$($created.values.tujuan)"

# --- numbering advanced ---
$n2 = (Invoke-RestMethod -Uri "$base/logbook/cctv/next-no" -Headers $hdr).nomor
Check "Next-no jadi $n1 + 1" ([int]$n2 -eq ([int]$n1 + 1)) "actual=$n2 (prev=$n1)"

# --- previous-year date: the number no longer embeds a year, so the date parameter
#     must not change the counter. It is compared against $n2 (taken AFTER the row was
#     created), not $n1 - the counter legitimately advanced by one in between. ---
$py = $year - 1
$npy = (Invoke-RestMethod -Uri "$base/logbook/cctv/next-no?date=$py-06-01" -Headers $hdr).nomor
Check 'Nomor tidak dipengaruhi tanggal (tanpa komponen tahun)' ($npy -eq $n2) "actual=$npy (expected=$n2)"

# --- signature round-trip (data-URL PNG) ---
# PUT has replace semantics: MaterializeAsync re-validates every required field, so the
# full row must be resent, not just the changed field. The logbook page does this.
$sig = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg=='
$row = $created.values
# SaveRecordRequest wraps the fields in a "values" property; sending the flat map
# yields "The Values field is required".
$full = @{
  values = @{
    nomor = $row.nomor; tanggal = $row.tanggal; departemen = $row.departemen
    no_pegawai = $row.no_pegawai; nama_pemohon = $row.nama_pemohon; tujuan = $row.tujuan
    waktu_diminta = $row.waktu_diminta; tanda_pemohon = $sig
    pic_isd = $row.pic_isd; pic_mulai = $row.pic_mulai; pic_selesai = $row.pic_selesai
    tanda_isd = $null
  }
} | ConvertTo-Json -Depth 5
$after = $null
try {
  $after = Invoke-RestMethod -Uri "$base/records/$($cctv.id)/$($created.id)" -Method Put -Headers $hdr `
    -ContentType 'application/json' -Body $full
} catch {
  $resp = $_.Exception.Response
  $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
  Write-Output "  DEBUG body: $full"
  Write-Output "  DEBUG resp: $($reader.ReadToEnd())"
  throw
}
Check 'Signature PNG tersimpan utuh' ($after.values.tanda_pemohon -eq $sig) "terpanas=$($after.values.tanda_pemohon.Length)"
Check 'Update dicatat di audit' ($after.updatedBy -eq 'admin') "updatedBy=$($after.updatedBy)"

# --- uniqueness of NO ---
$dup = @{ values = @{ nomor = $n1; nama_pemohon = 'X'; tujuan = 'Y' } } | ConvertTo-Json -Depth 5
$rejected = $false
try { Invoke-RestMethod -Uri "$base/records/$($cctv.id)" -Method Post -Headers $hdr -ContentType 'application/json' -Body $dup | Out-Null }
catch { $rejected = $true }
Check 'NO duplikat ditolak' $rejected

# --- list ---
$page = Invoke-RestMethod -Uri "$base/records/$($cctv.id)?pageSize=200" -Headers $hdr
Check 'List mengembalikan baris' ($page.total -ge 1) "total=$($page.total)"

# --- cleanup test row ---
Invoke-RestMethod -Uri "$base/records/$($cctv.id)/$($created.id)" -Method Delete -Headers $hdr | Out-Null
$gone = $false
try { Invoke-RestMethod -Uri "$base/records/$($cctv.id)/$($created.id)" -Headers $hdr | Out-Null }
catch { $gone = $true }
Check 'Delete (soft) menyembunyikan baris' $gone

# numbering reuses the freed number: acceptable (gap only appears on delete of the tail)
$n3 = (Invoke-RestMethod -Uri "$base/logbook/cctv/next-no" -Headers $hdr).nomor
Check 'Next-no setelah delete' ($n3 -eq $n1) "actual=$n3 (expected=$n1)"

Write-Output ""
Write-Output "PASS=$pass FAIL=$fail"
if ($fail -gt 0) { exit 1 }