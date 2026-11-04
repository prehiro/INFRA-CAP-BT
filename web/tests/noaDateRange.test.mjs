import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { PDFDocument } from 'pdf-lib'
import { chooseNoaRangeDay, formatNoaDateRange, noaMonthGrid, parseNoaDateRange, selectNoaDate } from '../app/utils/noaDateRange.ts'
import { DEFAULT_NOA_INPUT, generateNoaPdf } from '../app/utils/noaForm.ts'

test('NOA picker formats an Indonesian date range with an en dash', () => {
  assert.equal(formatNoaDateRange('2026-10-23', '2026-12-31'), '23 Okt 2026 – 31 Des 2026')
  assert.deepEqual(parseNoaDateRange('23 Okt 2026 – 31 Des 2026'), { from: '2026-10-23', to: '2026-12-31' })
  assert.deepEqual(parseNoaDateRange('23 okt 2026 - 31 des 2026'), { from: '2026-10-23', to: '2026-12-31' })
})

test('NOA picker rejects invalid or reversed ranges without changing free text', () => {
  assert.equal(formatNoaDateRange('2026-02-30', '2026-12-31'), '')
  assert.equal(formatNoaDateRange('2026-12-31', '2026-10-23'), '')
  assert.equal(parseNoaDateRange('31 Feb 2026 – 31 Des 2026'), null)
  assert.equal(parseNoaDateRange('Custom date text'), null)
})

test('NOA datepicker selection updates both endpoints and rejects reversed order', () => {
  let range = selectNoaDate({ from: '', to: '' }, 'from', '2026-10-23')
  assert.equal(formatNoaDateRange(range.from, range.to), '')
  range = selectNoaDate(range, 'to', '2026-12-31')
  assert.equal(formatNoaDateRange(range.from, range.to), '23 Okt 2026 – 31 Des 2026')
  range = selectNoaDate(range, 'from', '2027-01-02')
  assert.deepEqual(range, { from: '2027-01-02', to: '' })
  range = selectNoaDate(range, 'to', '2026-10-23')
  assert.deepEqual(range, { from: '', to: '2026-10-23' })
  assert.deepEqual(selectNoaDate(range, 'from', '2026-02-30'), range)
})

test('NOA single calendar chooses start then end and supports changing either endpoint', () => {
  const initial = { from: '2026-10-23', to: '2026-12-31' }
  const first = chooseNoaRangeDay(initial, 'from', '2026-11-01')
  assert.deepEqual(first, { range: { from: '2026-11-01', to: '' }, nextStep: 'to' })
  const second = chooseNoaRangeDay(first.range, first.nextStep, '2026-12-15')
  assert.deepEqual(second, { range: { from: '2026-11-01', to: '2026-12-15' }, nextStep: 'from' })
  const changeEnd = chooseNoaRangeDay(initial, 'to', '2027-01-10')
  assert.deepEqual(changeEnd.range, { from: '2026-10-23', to: '2027-01-10' })
  const earlier = chooseNoaRangeDay(initial, 'to', '2026-10-01')
  assert.deepEqual(earlier, { range: { from: '2026-10-01', to: '' }, nextStep: 'to' })
})

test('NOA range calendar keeps six rows and highlights the selected interval', () => {
  const october = noaMonthGrid(2026, 9, '2026-10-23', '2026-12-31')
  const december = noaMonthGrid(2026, 11, '2026-10-23', '2026-12-31')
  assert.equal(october.length, 42)
  assert.equal(december.length, 42)
  assert.equal(october[0].iso, '2026-09-28')
  assert.equal(october.find(day => day.iso === '2026-10-23')?.isStart, true)
  assert.equal(october.find(day => day.iso === '2026-10-24')?.between, true)
  assert.equal(december.find(day => day.iso === '2026-12-31')?.isEnd, true)
})

test('NOA form defaults are editable seed values', () => {
  assert.deepEqual(DEFAULT_NOA_INPUT, {
    changes: 'New LAN Point installation at ',
    waste: 'Excess wire, pipe, plactic, and dust',
    poNo: 'PTCAP__',
    what: 'LAN Point installation at ',
    why: 'Additional WiFi device to cover 2nd floor area',
    where: 'Capacitor Building 2',
    when: '23 Okt 2026 – 31 Des 2026',
    who: 'PT DUTA COMPUTER'
  })
  const editable = { ...DEFAULT_NOA_INPUT }
  editable.changes = 'Custom alteration'
  editable.when = 'Custom schedule'
  assert.equal(editable.changes, 'Custom alteration')
  assert.equal(editable.when, 'Custom schedule')
})

test('NOA PDF accepts the selected date range and the requested editable defaults', async () => {
  const template = readFileSync(new URL('../app/assets/forms/noa-r3-template.pdf', import.meta.url))
  const bytes = await generateNoaPdf(template, {
    ...DEFAULT_NOA_INPUT,
    when: formatNoaDateRange('2026-10-23', '2026-12-31')
  }, new Date(2026, 9, 23))
  assert.equal((await PDFDocument.load(bytes)).getPageCount(), 1)
})
