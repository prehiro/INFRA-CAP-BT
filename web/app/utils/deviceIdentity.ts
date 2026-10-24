export function normalizeDeviceIdentity(value: unknown): string {
  return String(value ?? '').trim().toUpperCase()
}

export function shouldValidateDeviceIdentity(value: unknown, original: unknown, editing: boolean): boolean {
  const normalized = normalizeDeviceIdentity(value)
  return !!normalized && (!editing || normalized !== normalizeDeviceIdentity(original))
}

type DeviceRow = { id: number | string; values?: Record<string, unknown> }


/** Exact, normalized matching over all server pages, never a limited substring search.
 * Existing duplicates are grandfathered by the caller ONLY when the edited value is unchanged.
 * Throws on incomplete/network reads; the caller then defers to authoritative API validation. */
export async function findDuplicateDeviceIdentity<T extends DeviceRow>(
  key: string, value: unknown, excludeId: number | string | null,
  loadedRows: readonly T[], loadPage: (page: number) => Promise<{ items: T[]; total: number }>
): Promise<T | undefined> {
  const wanted = normalizeDeviceIdentity(value)
  if (!wanted) return undefined
  const find = (rows: readonly T[]) => rows.find(row =>
    (excludeId === null || String(row.id) !== String(excludeId)) &&
    normalizeDeviceIdentity(row.values?.[key]) === wanted)
  const local = find(loadedRows)
  if (local) return local
  let read = 0
  for (let page = 1; ; page++) {
    const result = await loadPage(page)
    const hit = find(result.items)
    if (hit) return hit
    read += result.items.length
    if (read >= result.total) return undefined
    if (!result.items.length) throw new Error('Incomplete device identity lookup')
  }
}
