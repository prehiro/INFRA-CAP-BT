/** Prefix sizes preserve wrapped/signature rows; unseen rows use an estimate until measured. */
export function registerOffsets(rows: readonly { id: number }[], heights: ReadonlyMap<number, number>, estimate: number): number[] {
  const offsets = [0]
  for (const row of rows) offsets.push(offsets[offsets.length - 1]! + (heights.get(row.id) ?? estimate))
  return offsets
}

/** Largest row boundary at/before the viewport. End-of-data is a valid boundary, never an index read. */
export function registerIndexAt(offsets: readonly number[], y: number): number {
  let low = 0, high = Math.max(0, offsets.length - 1)
  while (low < high) {
    const mid = Math.ceil((low + high) / 2)
    if (offsets[mid]! <= y) low = mid
    else high = mid - 1
  }
  return low
}
