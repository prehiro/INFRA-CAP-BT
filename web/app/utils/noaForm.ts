import { PDFDocument, StandardFonts, rgb } from 'pdf-lib'

export const DEFAULT_NOA_WASTE = 'Excess wire, pipe, plactic, and dust'
export type NoaInput = {
  changes: string
  waste: string
  poNo: string
  what: string
  why: string
  where: string
  when: string
  who: string
}

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']

/** Use the user's local calendar month at the instant Generate is clicked. */
export function noaMonth(date: Date): string {
  if (Number.isNaN(date.getTime())) throw new Error('Invalid generation date.')
  return `${MONTHS[date.getMonth()]} ${date.getFullYear()}`
}

export function validateNoaInput(input: NoaInput): string | null {
  for (const [key, label] of [
    ['changes', 'Please specify the changes'], ['poNo', 'PO NO'], ['what', '1. What'],
    ['why', '2. Why'], ['where', '3. Where'], ['when', '4. When'], ['who', '5. Who']
  ] as const) {
    if (!input[key]?.trim()) return `Enter ${label}.`
  }
  for (const [key, value] of Object.entries(input)) {
    if (/[\r\n\t\u0000-\u001f]/.test(value)) return `${key} must be a single line.`
  }
  return null
}

/** Draw only the variable text on the PDF exported from the original Word master.
 * All fixed text, check marks, ruled lines, header/footer and item 6 remain in the template.
 * Coordinates are measured from Word's own PDF export (A4: 595.44 x 841.68 pt).
 */
export async function generateNoaPdf(template: Uint8Array, input: NoaInput, generatedAt = new Date()): Promise<Uint8Array> {
  const error = validateNoaInput(input)
  if (error) throw new Error(error)
  const doc = await PDFDocument.load(template, { updateMetadata: false })
  if (doc.getPageCount() !== 1) throw new Error('Unexpected NOA template: one A4 page is required.')
  const page = doc.getPage(0)
  const { width, height } = page.getSize()
  if (Math.abs(width - 595.44) > 0.1 || Math.abs(height - 841.68) > 0.1) throw new Error('Unexpected NOA template page size.')
  const font = await doc.embedFont(StandardFonts.TimesRoman)
  const black = rgb(0, 0, 0)
  function draw(label: string, value: string, x: number, baseline: number, maxX: number, size = 12) {
    const text = value.trim()
    let unitWidth: number
    try { unitWidth = font.widthOfTextAtSize(text, 1) } catch { throw new Error(`${label} supports Latin text only. Please use Latin characters.`) }
    const fit = Math.min(size, (maxX - x) / Math.max(unitWidth, 0.01))
    if (fit < (size === 10 ? 9 : 10)) throw new Error(`${label} is too long for the original NOA line. Shorten it before generating.`)
    if (text) page.drawText(text, { x, y: height - baseline, size: fit, font, color: black })
  }
  draw('For month of', noaMonth(generatedAt), 229.73, 80.66, 358, 10)
  draw('Please specify the changes', input.changes, 98.66, 317.09, 558)
  // The master prefixes ("Waste :-" and "PO NO :") are part of the unmodified page.
  const waste = input.waste.trim()
  draw('Waste', waste && !/[.!?]$/.test(waste) ? `${waste}.` : waste, 141.86, 335.09, 558)
  draw('PO NO', input.poNo, 143.65, 353.09, 558)
  draw('1. What', input.what, 132.86, 502.27, 562)
  draw('2. Why', input.why, 130.22, 522.19, 562)
  draw('3. Where', input.where, 138.86, 540.19, 562)
  draw('4. When', input.when, 135.86, 558.22, 562)
  draw('5. Who', input.who, 130.22, 577.90, 562)
  return doc.save({ updateFieldAppearances: false })
}
