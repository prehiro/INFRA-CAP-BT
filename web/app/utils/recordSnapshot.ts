/** Apply the API's complete canonical DTO after a successful Edit. Keep every other row object,
 * array order and record count intact; sorting/filtering remain the page's derived projections.
 * This is not an optimistic update and must only run after the update request succeeds. */
export function replaceSavedRecord<T extends { id: number }>(rows: readonly T[], saved: T): T[] {
  const index = rows.findIndex(row => row.id === saved.id)
  if (index < 0) return rows as T[]
  const next = rows.slice()
  next[index] = saved
  return next
}
