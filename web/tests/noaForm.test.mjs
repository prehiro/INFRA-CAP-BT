import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { PDFDocument } from 'pdf-lib'
import { DEFAULT_NOA_WASTE, generateNoaPdf, noaMonth, validateNoaInput } from '../app/utils/noaForm.ts'

const template = readFileSync(new URL('../app/assets/forms/noa-r3-template.pdf', import.meta.url))
const input = {
  changes: 'New LAN and Access Point installation at mezzanine floor capacitor',
  waste: DEFAULT_NOA_WASTE,
  poNo: 'PTIDS__26100005',
  what: 'New LAN and Access Point installation at mezzanine floor capacitor',
  why: 'Additional WiFi device to cover second floor area',
  where: 'Capacitor Building 2',
  when: '23 Oct 2026 - 31 Dec 2026',
  who: 'PT DUTA COMPUTER'
}

test('NOA month uses local month and year at generation time', () => {
  assert.equal(noaMonth(new Date(2026, 9, 31, 23, 59)), 'October 2026')
  assert.equal(noaMonth(new Date(2026, 10, 1, 0, 1)), 'November 2026')
  assert.throws(() => noaMonth(new Date(NaN)), /Invalid/)
})
test('NOA field validation keeps editable waste optional, requires the seven requested inputs', () => {
  assert.equal(validateNoaInput(input), null)
  assert.equal(validateNoaInput({ ...input, waste: '' }), null)
  for (const field of ['changes', 'poNo', 'what', 'why', 'where', 'when', 'who']) {
    assert.match(validateNoaInput({ ...input, [field]: ' ' }), /Enter/)
  }
  assert.match(validateNoaInput({ ...input, what: 'First\nSecond' }), /single line/)
})
test('NOA generator preserves the one-page master and does not write form fields', async () => {
  const source = await PDFDocument.load(template)
  const filled = await PDFDocument.load(await generateNoaPdf(template, input, new Date(2026, 9, 26)))
  assert.equal(source.getPageCount(), 1)
  assert.equal(filled.getPageCount(), 1)
  assert.deepEqual(filled.getPage(0).getSize(), source.getPage(0).getSize())
  assert.equal(filled.getForm().getFields().length, source.getForm().getFields().length)
})
test('NOA generator rejects clipping and unsupported glyphs rather than corrupting master', async () => {
  await assert.rejects(generateNoaPdf(template, { ...input, who: 'LONG'.repeat(200) }), /too long/)
  await assert.rejects(generateNoaPdf(template, { ...input, who: '名字' }), /Latin/)
  await assert.rejects(generateNoaPdf(template, { ...input, poNo: '' }), /PO NO/)
  const bytes = new Uint8Array(template)
  bytes[0] = 0
  await assert.rejects(generateNoaPdf(bytes, input), /PDF|header|document/i)
})
