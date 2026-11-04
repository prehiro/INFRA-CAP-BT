// NOA's When text stays editable; calendar selections use a stable Indonesian display format.
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'Mei', 'Jun', 'Jul', 'Agu', 'Sep', 'Okt', 'Nov', 'Des'] as const

type DateRange = { from: string; to: string }

function dateFromIso(value: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (!match) return null
  const year = Number(match[1])
  const month = Number(match[2])
  const day = Number(match[3])
  const date = new Date(year, month - 1, day)
  return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day ? date : null
}

function toIso(year: number, month: number, day: number): string {
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`
}

export function displayNoaDate(value: string): string | null {
  const date = dateFromIso(value)
  if (!date) return null
  return `${String(date.getDate()).padStart(2, '0')} ${MONTHS[date.getMonth()]} ${date.getFullYear()}`
}

export function noaMonthGrid(year: number, month: number, from: string, to: string) {
  const first = new Date(year, month, 1)
  const offset = (first.getDay() + 6) % 7 // Monday first
  const start = new Date(year, month, 1 - offset)
  return Array.from({ length: 42 }, (_, index) => {
    const date = new Date(start.getFullYear(), start.getMonth(), start.getDate() + index)
    const iso = toIso(date.getFullYear(), date.getMonth() + 1, date.getDate())
    return {
      iso,
      day: date.getDate(),
      inMonth: date.getMonth() === month,
      isStart: iso === from,
      isEnd: iso === to,
      between: Boolean(from && to && iso > from && iso < to)
    }
  })
}

export function selectNoaDate(range: DateRange, which: 'from' | 'to', value: string): DateRange {
  if (value && !dateFromIso(value)) return range
  if (which === 'from') {
    return { from: value, to: range.to && value && value > range.to ? '' : range.to }
  }
  return { from: range.from && value && value < range.from ? '' : range.from, to: value }
}

export function chooseNoaRangeDay(range: DateRange, step: 'from' | 'to', value: string) {
  if (!dateFromIso(value)) return { range, nextStep: step }
  if (step === 'from' || !range.from || value < range.from) {
    return { range: selectNoaDate({ from: '', to: '' }, 'from', value), nextStep: 'to' as const }
  }
  return { range: selectNoaDate({ from: range.from, to: '' }, 'to', value), nextStep: 'from' as const }
}

export function formatNoaDateRange(from: string, to: string): string {
  const start = displayNoaDate(from)
  const end = displayNoaDate(to)
  return start && end && from <= to ? `${start} – ${end}` : ''
}

export function parseNoaDateRange(value: string): DateRange | null {
  const datePart = '(\\d{1,2})\\s+([A-Za-z]{3})\\s+(\\d{4})'
  const match = new RegExp(`^\\s*${datePart}\\s+[–-]\\s+${datePart}\\s*$`, 'i').exec(value)
  if (!match) return null
  const parsePart = (day: string, month: string, year: string) => {
    const monthIndex = MONTHS.findIndex(name => name.toLowerCase() === month.toLowerCase())
    return monthIndex < 0 ? '' : toIso(Number(year), monthIndex + 1, Number(day))
  }
  const from = parsePart(match[1]!, match[2]!, match[3]!)
  const to = parsePart(match[4]!, match[5]!, match[6]!)
  return from && to && formatNoaDateRange(from, to) ? { from, to } : null
}
