import { PDFDocument, PDFName, PDFSignature, StandardFonts } from 'pdf-lib'

export type IssuedAsset = { brandModel: string; serialNo: string; poNo: string; remark: string }
export type IssuedFormInput = { user: string; employeeNo: string; assets: IssuedAsset[] }
export const ASSET_KEYS = ['brandModel', 'serialNo', 'poNo', 'remark'] as const
export const emptyIssuedAsset = (): IssuedAsset => ({ brandModel: '', serialNo: '', poNo: '', remark: '' })

/** Local calendar date at the instant Generate is clicked, never UTC slicing. */
export function issuedPdfDate(date: Date): string {
  if (Number.isNaN(date.getTime())) throw new Error('Invalid generation date.')
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
  return `${String(date.getDate()).padStart(2, '0')}-${months[date.getMonth()]}-${String(date.getFullYear()).slice(-2)}`
}

export function validateIssuedInput(input: IssuedFormInput): string | null {
  if (!input.user.trim() || !input.employeeNo.trim()) return 'Enter User and Employee No.'
  if (!input.assets.length || input.assets.length > 6) return 'The original PDF supports one to six equipment rows.'
  if (!input.assets.some(row => row.brandModel.trim())) return 'Enter at least one Brand & Model.'
  if (input.assets.some(row => ASSET_KEYS.some(k => row[k].trim()) && !row.brandModel.trim())) return 'Each completed equipment row needs a Brand & Model.'
  return null
}

/** Fill original AcroForm fields, never flatten/rasterize/rebuild the page.
 * Update only A, B, C Received By name/date; leave ALL other values/APs/actions intact.
 */
export async function generateIssuedPdf(template: Uint8Array, input: IssuedFormInput, generatedAt = new Date()): Promise<Uint8Array> {
  const error = validateIssuedInput(input)
  if (error) throw new Error(error)
  const doc = await PDFDocument.load(template, { updateMetadata: false })
  const form = doc.getForm()
  const signatures = form.getFields().filter(f => f instanceof PDFSignature)
  if (signatures.length !== 8) throw new Error('Unexpected PDF template: eight signature fields are required.')
  if (signatures.some(f => f.acroField.dict.has(PDFName.of('V')))) throw new Error('Use the unsigned original template, not an already signed PDF.')
  const font = await doc.embedFont(StandardFonts.Helvetica)
  const values: Record<string, string> = {}
  for (let row = 0; row < 6; row++) {
    for (const [col, key] of ASSET_KEYS.entries()) values[`Text${row * 4 + col + 1}`] = (input.assets[row]?.[key] ?? '').trim()
  }
  Object.assign(values, { Text25: input.user.trim(), Text26: input.employeeNo.trim(), Text29: input.user.trim(), Date2: issuedPdfDate(generatedAt) })
  for (const [name, value] of Object.entries(values)) {
    const field = form.getTextField(name)
    // Reject unsupported/overlong text instead of silently clipping it in this fixed-size form.
    let width: number
    try { width = font.widthOfTextAtSize(value, 1) } catch { throw new Error('This PDF font supports Latin text only. Please use Latin characters.') }
    if (/[\r\n\t]/.test(value)) throw new Error('Use a single line in each PDF field.')
    const rect = field.acroField.getWidgets()[0]!.getRectangle()
    const size = Math.min(11, (rect.width - 6) / Math.max(width, 0.01), rect.height - 5)
    if (size < 6) throw new Error(`Text is too long for the PDF field ${name}. Shorten the entry before generating.`)
    field.setText(value)
    field.disableReadOnly()
    field.setFontSize(size)
    field.defaultUpdateAppearances(font)
  }
  // Avoid regenerating appearances on untouched C/D fields or the signature widgets.
  return doc.save({ updateFieldAppearances: false })
}
