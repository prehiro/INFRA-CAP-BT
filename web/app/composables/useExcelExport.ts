/**
 * Export a logbook to a real .xlsx workbook.
 *
 * WHY EXCELJS: the obvious alternative, SheetJS, cannot embed images. A CCTV access log
 * is signed, and a spreadsheet of names with two blank columns called "PIC Sign" is close
 * to worthless as an audit artefact. ExcelJS writes actual PNGs into the sheet, so the
 * exported file carries the signatures with it.
 *
 * The library is loaded with a DYNAMIC import inside the function. It is ~1MB and this app
 * is served from an office server; making every visitor download it just in case somebody
 * clicks Export would be wasteful. The import only runs when the button is pressed.
 */
/**
 * The row shape is intentionally loose (`values: Record<string, any>`): this composable is
 * meant to serve any metadata-driven entity, not just the CCTV logbook.
 */
type ExportRow = { values: Record<string, any> }

/** Column widths, roughly matched to the on-screen table. */
const WIDTHS = [6, 12, 10, 12, 20, 40, 12, 18, 18, 20, 12, 12]

export async function exportLogbookToExcel(opts: {
  rows: ExportRow[]
  columns: { key: string; label: string; sign?: boolean }[]
  fileName: string
  sheetTitle: string
}) {
  const ExcelJS = (await import('exceljs')).default ?? (await import('exceljs'))
  const wb = new (ExcelJS as any).Workbook()
  wb.creator = 'INFRA-CAP'
  wb.created = new Date()

  const ws = wb.addWorksheet(opts.sheetTitle.slice(0, 31), {
    views: [{ state: 'frozen', ySplit: 1 }]
  })

  const header = ws.addRow(opts.columns.map((c) => c.label.toUpperCase()))
  header.font = { bold: true, color: { argb: 'FFFFFFFF' } }
  header.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF233D4D' } }
  header.alignment = { vertical: 'middle', horizontal: 'center', wrapText: true }
  header.height = 26
  opts.columns.forEach((_, i) => { ws.getColumn(i + 1).width = WIDTHS[i] ?? 14 })

  // Row height leaves room for the embedded signature images (drawn 64px tall on screen).
  for (const r of opts.rows) {
    const values = opts.columns.map((c) => {
      const v = r.values[c.key]
      return v === null || v === undefined ? '' : v
    })
    const row = ws.addRow(values)
    row.alignment = { vertical: 'middle' }
    row.height = 46
  }

  // Embed the signature PNGs. ExcelJS wants a Buffer/Uint8Array, so the data-URL prefix is
  // stripped and the base64 decoded.
  const sigKeys = opts.columns.filter((c) => c.sign).map((c) => c.key)
  if (sigKeys.length) {
    opts.rows.forEach((r, i) => {
      sigKeys.forEach((key) => {
        const src = r.values[key]
        if (typeof src !== 'string' || !src.startsWith('data:image/png;base64,')) return
        const b64 = src.slice('data:image/png;base64,'.length)
        const id = wb.addImage({ extension: 'png', base64: b64 })
        const col = opts.columns.findIndex((c) => c.key === key) + 1
        ws.getRow(i + 2).height = 46
        ws.addImage(id, {
          tl: { col: col - 1 + 0.12, row: i + 1 + 0.16 },
          ext: { width: Math.max(48, (WIDTHS[col - 1] ?? 14) * 5), height: 34 },
          editAs: 'oneCell'
        })
      })
    })
  }

  const buf = await wb.xlsx.writeBuffer()
  const blob = new Blob([buf], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
  })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = opts.fileName
  document.body.appendChild(a)
  a.click()
  a.remove()
  // Give the browser a beat to start the download before revoking.
  setTimeout(() => URL.revokeObjectURL(url), 2000)
}
