$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5099'
# Unique suffix per run so the script is repeatable (unique-constraint columns would otherwise clash).
$run = Get-Date -Format 'HHmmss'

function Show($label, $value) { Write-Output ("{0,-34}: {1}" -f $label, $value) }

# 1. login

# Credentials come from the environment so nothing secret is committed to git:
#   $env:INFRA_ADMIN_PASSWORD = '<password>'    (PowerShell)
$AdminUser = if ($env:INFRA_ADMIN_USER) { $env:INFRA_ADMIN_USER } else { 'admin' }
$AdminPass = $env:INFRA_ADMIN_PASSWORD
if (-not $AdminPass) { throw 'Set $env:INFRA_ADMIN_PASSWORD before running this script.' }
$body = @{ username = $AdminUser; password = $AdminPass } | ConvertTo-Json -Compress
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body $body
Show "1. LOGIN" ("OK, user=" + $login.user.username + " role=" + ($login.user.roles.name -join ','))
$hdr = @{ Authorization = "Bearer $($login.token)" }

# 2. seeded entities
$ents = Invoke-RestMethod -Uri "$base/api/entities" -Headers $hdr
Show "2. ENTITIES seeded" (($ents | ForEach-Object { $_.slug }) -join ', ')
Show "   fields per entity" (($ents | ForEach-Object { "$($_.slug)=$($_.fields.Count)" }) -join ' ')

# 3. create a customer
$custBody = @{ values = @{ kode="C$run"; nama='PT Contoh Jaya'; email='info@contoh.co.id'; telpon='021-5551234'; status=$true } } | ConvertTo-Json -Depth 5
$entCustomer = $ents | Where-Object { $_.slug -eq 'customer' }
$newCust = Invoke-RestMethod -Uri "$base/api/records/$($entCustomer.id)" -Method Post -Headers $hdr -ContentType 'application/json' -Body $custBody
Show "3. CREATE customer" ("id=$($newCust.id) display='$($newCust.display)'")

# 4. duplicate code must be rejected
try {
    Invoke-RestMethod -Uri "$base/api/records/$($entCustomer.id)" -Method Post -Headers $hdr -ContentType 'application/json' -Body $custBody | Out-Null
    Show "4. UNIQUE guard" "FAIL - duplicate accepted!"
} catch {
    Show "4. UNIQUE guard" "OK rejected (400)"
}

# 5. required field validation
try {
    $bad = @{ values = @{ kode="X$run"; nama='' } } | ConvertTo-Json -Depth 5
    Invoke-RestMethod -Uri "$base/api/records/$($entCustomer.id)" -Method Post -Headers $hdr -ContentType 'application/json' -Body $bad | Out-Null
    Show "5. REQUIRED guard" "FAIL - empty accepted!"
} catch {
    Show "5. REQUIRED guard" "OK rejected (400)"
}

# 6. product, then a sales order referencing it via lookup
$prodBody = @{ values = @{ sku="SKU-$run"; nama='Kabel LAN Cat5'; kategori='Networking'; harga=75000; stok=100 } } | ConvertTo-Json -Depth 5
$entProduct = $ents | Where-Object { $_.slug -eq 'product' }
$newProd = Invoke-RestMethod -Uri "$base/api/records/$($entProduct.id)" -Method Post -Headers $hdr -ContentType 'application/json' -Body $prodBody
Show "6. CREATE product" ("id=$($newProd.id) display='$($newProd.display)'")

$entSO = $ents | Where-Object { $_.slug -eq 'sales_order' }
$soBody = @{ values = @{ nomor="SO-$run"; tanggal='2026-09-30'; customer=$newCust.id; product=$newProd.id; qty=5; harga_satuan=75000; total=375000; catatan='Test order otomatis' } } | ConvertTo-Json -Depth 5
$so = Invoke-RestMethod -Uri "$base/api/records/$($entSO.id)" -Method Post -Headers $hdr -ContentType 'application/json' -Body $soBody
Show "7. CREATE sales order" ("id=$($so.id) display='$($so.display)'")
Show "   lookup resolved customer" $so.values.customer
Show "   lookup resolved product"  $so.values.product

# 8. invalid lookup must be rejected
try {
    $badSo = @{ values = @{ nomor="SO-BAD-$run"; tanggal='2026-09-30'; customer=999999; product=999999; qty=1; harga_satuan=1 } } | ConvertTo-Json -Depth 5
    Invoke-RestMethod -Uri "$base/api/records/$($entSO.id)" -Method Post -Headers $hdr -ContentType 'application/json' -Body $badSo | Out-Null
    Show "8. LOOKUP guard" "FAIL - dangling reference accepted!"
} catch {
    Show "8. LOOKUP guard" "OK rejected (400)"
}

# 9. search must pierce the lookup display name
$s1 = Invoke-RestMethod -Uri "$base/api/records/$($entSO.id)?search=Jaya" -Headers $hdr
Show "9. SEARCH 'Jaya' (lookup)" ("hits=" + $s1.total + " display='" + $s1.items[0].display + "'")

# 10. filter by lookup value
$custFieldId = ($entSO.fields | Where-Object { $_.name -eq 'customer' }).id
$s2 = Invoke-RestMethod -Uri "$base/api/records/$($entSO.id)?lookupFieldId=$custFieldId&lookupValue=$($newCust.id)" -Headers $hdr
Show "10. FILTER by customer" ("hits=" + $s2.total)

# 11. unauthorized access must fail
try {
    Invoke-RestMethod -Uri "$base/api/entities" | Out-Null
    Show "11. AUTH guard" "FAIL - anonymous allowed!"
} catch {
    Show "11. AUTH guard" ("OK 401 -> " + $_.Exception.Response.StatusCode)
}

# 12. runtime-created entity, proving metadata-driven works without code changes
$newEntBody = @{
    name = "Supplier$run"; slug = "supplier_$run"; description = 'Pemasok barang'; kind = 'Master'
    displayField = 'nama'; sortOrder = 4
    fields = @(
        @{ name='kode'; label='Kode'; type='Text'; isRequired=$true; isUnique=$true; maxLength=20 },
        @{ name='nama'; label='Nama Supplier'; type='Text'; isRequired=$true; maxLength=200 },
        @{ name='kontak'; label='Kontak'; type='Text'; maxLength=100 }
    )
} | ConvertTo-Json -Depth 6
$sup = Invoke-RestMethod -Uri "$base/api/entities" -Method Post -Headers $hdr -ContentType 'application/json' -Body $newEntBody
Show "12. CREATE entity at runtime" ("slug=$($sup.slug) fields=$($sup.fields.Count)")

$supRec = Invoke-RestMethod -Uri "$base/api/records/$($sup.id)" -Method Post -Headers $hdr -ContentType 'application/json' `
    -Body (@{ values = @{ kode='S001'; nama='PT Distributor Utama'; kontak='0812-34567890' } } | ConvertTo-Json -Depth 5)
Show "13. Record on new entity" ("id=$($supRec.id) display='$($supRec.display)'")

# 14. paged list
$page = Invoke-RestMethod -Uri "$base/api/records/$($entSO.id)?page=1&pageSize=10" -Headers $hdr
Show "14. PAGED list" ("total=$($page.total) returned=$($page.items.Count)")

# 15. soft delete
Invoke-RestMethod -Uri "$base/api/records/$($sup.id)/$($supRec.id)" -Method Delete -Headers $hdr
$after = Invoke-RestMethod -Uri "$base/api/records/$($sup.id)" -Headers $hdr
Show "15. SOFT DELETE" ("total after delete=" + $after.total)

Write-Output ""
Write-Output "=== ALL E2E TESTS EXECUTED ==="
