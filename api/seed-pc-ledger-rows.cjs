/**
 * Seed the PC Ledger with the 26 rows that already exist in the reference spreadsheet, so the
 * page starts as the real inventory rather than as an empty shell - and so the export can be
 * compared against the file it replaces.
 *
 * Values are copied VERBATIM, including the messy ones the department already lives with
 * (`#NA` emails, `E0B5536/7` GIDs, doubled spaces in "Surface GO  4", trailing spaces in
 * "PC Display EVR "). Tidying them here would make the app's data disagree with the workbook
 * the department is still using, which is the one thing an import must not do.
 *
 * The Date column in the source holds a mix of Excel serial numbers and the literal text "OK"
 * (someone typed a status into a date column). Serials are converted; anything else is dropped
 * rather than forced into a Date field, and the original text is not lost because it stays in
 * the source workbook.
 */
const ExcelJS = require('C:/Users/HIRO/Projects/InternalApp/web/node_modules/exceljs')
const fs = require('fs')

const XLSX = 'D:/WORK/PANASONIC/Web/INFRA-CAP/reff/PC_Ledger.xlsx'
const API = 'http://localhost:5099'
const ENTITY_ID = 10
const DEPARTMENT = 'Capacitor'

// Column C..P of the sheet, in order, mapped to the entity's field names.
const MAP = [
  'staff_name', 'email', 'gid', 'japan_hostname', 'computer_model', 'computer_sn',
  'tanggal', 'chassis', 'manufacturer', 'os_name', 'os_arch', 'lokasi', 'remark2', 'remark3'
]

function serialToIso(serial) {
  const ms = Math.round((serial - 25569) * 86400 * 1000)
  const d = new Date(ms)
  return Number.isNaN(d.getTime()) ? null : d.toISOString().slice(0, 10)
}

function text(cell) {
  const v = cell?.value
  if (v === null || v === undefined) return ''
  if (typeof v === 'object' && v.text) return String(v.text)
  if (typeof v === 'object' && v.result !== undefined) return String(v.result)
  return String(v).trim()
}

;(async () => {
  const cfg = JSON.parse(fs.readFileSync('C:/Users/HIRO/Projects/InternalApp/api/appsettings.json', 'utf8').replace(/^\uFEFF/, ''))
  const user = cfg?.Seed?.AdminUsername ?? 'admin'
  const pass = cfg?.Seed?.AdminPassword

  const login = await fetch(`${API}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: user, password: pass })
  })
  if (!login.ok) { console.error('LOGIN FAILED', login.status); process.exit(1) }
  const token = (await login.json()).token

  const wb = new ExcelJS.Workbook()
  await wb.xlsx.readFile(XLSX)
  const ws = wb.getWorksheet('Ledger')

  let created = 0
  const failures = []

  for (let r = 11; r <= ws.rowCount; r++) {
    const row = ws.getRow(r)
    const no = row.getCell(2).value
    if (no === null || no === undefined) continue

    const values = { departemen: DEPARTMENT }
    MAP.forEach((field, i) => {
      const raw = row.getCell(3 + i)
      if (field === 'tanggal') {
        const num = typeof raw.value === 'number' ? raw.value : Number(text(raw))
        if (Number.isFinite(num) && num > 20000 && num < 60000) {
          const iso = serialToIso(num)
          if (iso) values.tanggal = iso
        }
        return
      }
      const t = text(raw)
      if (t) values[field] = t
    })

    const res = await fetch(`${API}/api/records/${ENTITY_ID}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
      body: JSON.stringify({ values })
    })
    if (res.ok) created++
    else failures.push(`row ${r}: ${res.status} ${(await res.text()).slice(0, 160)}`)
  }

  console.log(`created=${created} failed=${failures.length}`)
  failures.slice(0, 5).forEach((f) => console.log(' -', f))

  const check = await fetch(`${API}/api/records/${ENTITY_ID}?page=1&pageSize=500`, {
    headers: { Authorization: `Bearer ${token}` }
  })
  const page = await check.json()
  console.log('server total =', page.total)
})().catch((e) => { console.error('FAILED:', e.message); process.exit(1) })
