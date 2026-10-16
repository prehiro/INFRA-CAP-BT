/** The numeric sequence is independent for each chassis code; holes are never reused. */
const CHASSIS_CODES: Record<string, string> = { desktop: 'PC', laptop: 'NB', tablet: 'TB' }

export function nextPcHostname(chassis: string, hostnames: Iterable<unknown>): string | null {
  const code = CHASSIS_CODES[chassis.trim().toLowerCase()]
  if (!code) return null
  const pattern = new RegExp(`^BAL([0-9]+)${code}29384$`, 'i')
  let highest = 0n
  for (const hostname of hostnames) {
    const match = String(hostname ?? '').trim().match(pattern)
    if (!match) continue
    const sequence = BigInt(match[1]!)
    if (sequence > highest) highest = sequence
  }
  const sequence = String(highest + 1n).padStart(3, '0')
  return `BAL${sequence}${code}29384`
}

export type PcHostnamePage = {
  items: { id: number; values?: Record<string, unknown> }[]
  total: number
}

/** Read every real API page, not the currently loaded/filtered table (which caps at 500).
 * This proposes a hostname; it does not reserve one or change any stored record. */
export async function loadNextPcHostname(
  chassis: string,
  loadPage: (page: number) => Promise<PcHostnamePage>
): Promise<string | null> {
  if (!CHASSIS_CODES[chassis.trim().toLowerCase()]) return null
  const ids = new Set<number>()
  const hostnames: unknown[] = []
  for (let page = 1; ; page++) {
    const result = await loadPage(page)
    if (!Array.isArray(result.items) || !Number.isSafeInteger(result.total) || result.total < 0) {
      throw new Error('Invalid hostname list response.')
    }
    const before = ids.size
    for (const item of result.items) {
      if (ids.has(item.id)) continue
      ids.add(item.id)
      hostnames.push(item.values?.japan_hostname)
    }
    if (ids.size >= result.total) return nextPcHostname(chassis, hostnames)
    if (ids.size === before) throw new Error('The hostname list could not be read completely.')
  }
}
