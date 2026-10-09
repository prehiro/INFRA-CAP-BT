<script setup lang="ts">
/**
 * PC Ledger — the app's replacement for the manual Excel sheet (IT FORM SG031, sheet "Ledger").
 *
 * COLUMN SET AND ORDER ARE NOT A DESIGN CHOICE. They are the reference workbook's 16 columns,
 * in the reference's order, because the whole point of the page is that its export can be
 * handed back to the department as the workbook they already use. The entity in the API
 * (`pc_ledger`, id 10) was created to mirror it field for field.
 *
 * `department` is the one addition: the workbook carries a single "Department:" cell above the
 * table, which cannot describe a per-row value, so it lives on the record instead and is written
 * back into that cell on export. It NO LONGER appears on this page at all - not as a table column,
 * not in the add/edit form, not as a filter (HIRO: "hapus kolom departement dari page pc ledger,
 * form add juga. departement tidak dipakai di page pc ledger"). The field and the values stored on
 * the existing records are kept for that export cell alone, so the excelExport code below still
 * reads them; everything that used to surface the field on screen is gone.
 *
 * NO INPUT REWRITING happens on this page. Identity fields (`.\\capuser`, `JAPAN\\29384_DTS05`,
 * `pidbt.dts05@sg.panasonic.com`, `E0B5536/7`) are copied out of the source spreadsheet verbatim
 * and the app has a standing rule that identity and credential surfaces are never
 * auto-capitalised or otherwise "tidied".
 */
import type { EntityMeta, RecordRow } from '~/types'

const ENTITY_SLUG = 'pc_ledger'
/** Reference workbook sheet name, used for the exported worksheet tab. */
const SHEET_NAME = 'Ledger'

const toast = useToast()

/* ---------------- columns (mirror of the reference sheet) ---------------- */
/**
 * `w` IS A PERCENTAGE SHARE of the table's width, not a fixed width. Every column used to carry an
 * explicit rem width that added up to 269rem - 4304px measured inside a 1650px viewport - so the
 * register was 2.6 screens wide and each column was as wide as its longest possible value. HIRO:
 * "buat lebar kolomnya responsive? tujuanya agar lebih ramping dan tidak terlalu melebar".
 * Percentages let the columns share whatever width is available: the register fits the viewport and
 * grows only when the screen does.
 *
 * The shares are hand-picked rather than the old rem values scaled down mechanically - a proportional
 * conversion would have given the No column 1.8% (30px) and the Date column 4%. The columns carrying
 * long values (Email, Hostname, Location) still get the biggest shares. They sum to 95%, leaving 5%
 * for Actions, and the pinned pair is positioned by MEASUREMENT now - see pinOffset.
 */
type Column = { key: string; label: string; short?: string; w: number; align?: 'center' }

const COLUMNS: Column[] = [
  { key: 'nomor', label: 'No', w: 3, align: 'center' },
  { key: 'staff_name', label: 'Staff Name', w: 7 },
  { key: 'email', label: 'Email Address', short: 'Email', w: 9 },
  { key: 'gid', label: 'GID', w: 5 },
  { key: 'japan_hostname', label: 'JAPAN Hostname', short: 'JAPAN Host', w: 8 },
  { key: 'computer_model', label: 'Computer Model', short: 'Model', w: 7 },
  { key: 'computer_sn', label: 'Computer S/N', short: 'S/N', w: 7 },
  { key: 'tanggal', label: 'Date', w: 4, align: 'center' },
  { key: 'chassis', label: 'Computer Chassis', short: 'Chassis', w: 6 },
  { key: 'manufacturer', label: 'Computer Manufacturer', short: 'Vendor', w: 6 },
  { key: 'os_name', label: 'Computer O/S Name', short: 'O/S Name', w: 7 },
  { key: 'os_arch', label: 'Computer O/S Architecture', short: 'O/S Arch', w: 6 },
  { key: 'lokasi', label: 'Location', w: 9 },
  /* The KEYS stay `remark2` / `remark3` - those are the database field names - while the LABELS are
     Remark1 / Remark2. HIRO: "rename remark2 jadi Remark1, dan Remark3 jadi Remark2". Renaming the
     keys would be a field rename in the EAV store, which is a different (and much larger) change. */
  { key: 'remark2', label: 'Remark1', w: 7 },
  { key: 'remark3', label: 'Remark2', w: 4 }
]

/**
 * Column visibility: the user chooses which of the register's 15 columns are on screen.
 *
 * STORED PER BROWSER, not on the server. Which columns someone wants to look at is a reading
 * preference, not shared department state - the same reasoning that put the theme choice in a
 * cookie rather than in the database.
 *
 * `nomor` is not special-cased and can be hidden like any other column, but at least one column
 * always stays visible: an empty table with nothing to explain itself reads as a broken page.
 */
/**
 * The columns on screen the FIRST time someone opens the register. HIRO picked these eleven:
 * No, Staff Name, Email, GID, JAPAN Host, Model, S/N, Chassis, Location, Remark1, Remark2 - the
 * register as it is actually read day to day. The four left out (Date, Vendor, O/S Name, O/S Arch)
 * are one tick away in the column picker.
 */
const DEFAULT_VISIBLE = [
  'nomor',
  'staff_name',
  'email',
  'gid',
  'japan_hostname',
  'computer_model',
  'computer_sn',
  'chassis',
  'lokasi',
  'remark2',
  'remark3'
]

/**
 * VERSIONED STORAGE KEY, on purpose. The key used to be `infra-cap.pcledger.columns`, and every
 * browser that had ever opened the register had its own fifteen-column choice stored under it -
 * which would have quietly overridden this new default and left HIRO looking at no change at all.
 * A new suffix lets the default take effect once; the old value is simply never read again.
 */
const STORAGE_KEY = 'infra-cap.pcledger.columns.v2'
const visibleKeys = ref<string[]>([...DEFAULT_VISIBLE])

const visibleColumns = computed(() => COLUMNS.filter((c) => visibleKeys.value.includes(c.key)))

const isVisible = (key: string) => visibleKeys.value.includes(key)

/**
 * Position of a column among the VISIBLE ones, or -1 when it is hidden.
 *
 * Every column is always rendered now (a hidden one is animated to zero width instead of being
 * removed), so the DOM index no longer equals the visible index - and the pinning rules are
 * defined over the visible set. Without this the pinned pair would land on two hidden columns
 * after the first hide.
 */
const visibleIndex = (key: string) => visibleColumns.value.findIndex((c) => c.key === key)

function loadColumnChoice() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return
    const saved = JSON.parse(raw)
    if (!Array.isArray(saved)) return
    // Unknown keys are dropped rather than trusted: a stale key saved by an older build would
    // otherwise make the table render a column that no longer exists.
    const known = saved.filter((k) => COLUMNS.some((c) => c.key === k))
    if (known.length) visibleKeys.value = known
  } catch {
    // A corrupt value must never brick the page - fall back to the default column set.
  }
}

function saveColumnChoice() {
  try { localStorage.setItem(STORAGE_KEY, JSON.stringify(visibleKeys.value)) } catch { /* storage full or blocked */ }
}

function toggleColumn(key: string, on: boolean) {
  if (on) {
    if (!visibleKeys.value.includes(key)) visibleKeys.value = [...visibleKeys.value, key]
  } else {
    if (visibleKeys.value.length <= 1) return // never hide the last one
    visibleKeys.value = visibleKeys.value.filter((k) => k !== key)
  }
  saveColumnChoice()
}

function showAllColumns() {
  visibleKeys.value = COLUMNS.map((c) => c.key)
  saveColumnChoice()
}

/**
 * Column show/hide movement, driven by a WATCH rather than by the update lifecycle hooks.
 *
 * THAT CHOICE IS THE RESULT OF AN INSTRUMENTATION RUN, not a preference. A temporary logger was
 * placed in `onBeforeUpdate` and `onUpdated`; toggling a column produced ZERO entries from either
 * hook while the same toggle produced a `watch` entry. Whatever the reason the two update hooks
 * never fire in this component, relying on them is precisely why the first FLIP attempt did
 * nothing at all and looked (from the outside) like a mechanism that had been written wrongly.
 *
 * The watch runs in the pre-flush phase, so the DOM still holds the OLD layout when it starts -
 * which is exactly the "before" measurement a FLIP needs. `await nextTick()` then makes the new
 * layout readable, and the cells are moved back by the difference and released onto the same
 * overshooting curve the rows use, so a column toggle bounces instead of snapping.
 *
 * The cleanup timeout is not decoration: a FLIP that leaves a transform behind paints cells at the
 * wrong offset forever, so the inline styles are always cleared.
 */
const tableEl = ref<HTMLTableElement | null>(null)

/**
 * The wrapper around the table, and the reason it exists: the frozen scroll extent is applied HERE
 * and not on the table. A `min-height` on a `<table>` is distributed among its rows, so freezing the
 * extent to stop the scrollbar flickering also STRETCHED every row for the whole animation - HIRO:
 * "row melebar sebentar lalu kembali normal, ini jelek". A block wrapper takes the same height without
 * touching the rows at all.
 */
const tableWrapEl = ref<HTMLElement | null>(null)
const COL_ANIM_MS = 340

/**
 * Left position of EVERY cell in the table, grouped by row, plus the header row.
 *
 * THE FIRST VERSION OF THIS ONLY MEASURED `querySelector('tbody tr')` - the first row - so only
 * row 1 received a transform and bounced while every other row snapped. HIRO caught it:
 * "kenapa cuma row 1 yang bounce, saya mau semua row". The header is measured too, and that is not
 * a nicety: when a column is hidden, the header cells shift as well, and animating only the body
 * would leave the header sitting at the new layout while the body is still travelling - a visible
 * misalignment for the whole 340ms.
 */
type Lefts = { head: number[] | null; rows: number[][] }

function snapshotLefts(): Lefts | null {
  const table = tableEl.value
  if (!table) return null
  const rows = [...table.querySelectorAll('tbody tr')]
  if (!rows.length) return null
  const headRow = table.querySelector('thead tr')
  return {
    head: headRow ? [...headRow.querySelectorAll('th')].map((th) => th.getBoundingClientRect().left) : null,
    rows: rows.map((r) => [...r.querySelectorAll('td')].map((td) => td.getBoundingClientRect().left))
  }
}

watch(visibleKeys, async () => {
  const before = snapshotLefts()
  await nextTick()
  const after = snapshotLefts()
  if (!before || !after) return
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return

  const table = tableEl.value
  if (!table) return

  // Every element that has to travel, with the distance it has to travel back by.
  const movers: Array<[HTMLElement, number]> = []
  const collect = (cells: HTMLElement[], b: number[], a: number[]) => {
    cells.forEach((el, i) => {
      const was = b[i]
      const now = a[i]
      if (was === undefined || now === undefined) return
      const dx = was - now
      if (Math.abs(dx) < 1) return
      movers.push([el, dx])
    })
  }

  const headRow = table.querySelector('thead tr')
  if (headRow) {
    collect([...headRow.querySelectorAll('th')] as HTMLElement[], before.head ?? [], after.head ?? [])
  }
  const rows = [...table.querySelectorAll('tbody tr')] as HTMLElement[]
  rows.forEach((tr, ri) => {
    collect([...tr.querySelectorAll('td')] as HTMLElement[], before.rows[ri] ?? [], after.rows[ri] ?? [])
  })

  if (!movers.length) return

  movers.forEach(([el, dx]) => {
    el.style.transition = 'none'
    el.style.transform = `translateX(${dx}px)`
  })

  requestAnimationFrame(() => {
    movers.forEach(([el]) => {
      el.style.transition = `transform ${COL_ANIM_MS}ms cubic-bezier(0.34, 1.56, 0.64, 1)`
      el.style.transform = ''
    })
    window.setTimeout(() => {
      movers.forEach(([el]) => { el.style.transition = ''; el.style.transform = '' })
      // The visible set has changed, so the first column may be a different one now - re-measure the
      // pinned offset once the animation has settled, not during it, or the pinned pair would jump.
      measurePinOffset()
    }, COL_ANIM_MS + 100)
  })
}, { deep: true })
/**
 * The first two VISIBLE columns stay pinned while the rest scroll sideways under them.
 * A class rather than `nth-child`, and the offset is computed rather than hard-coded: hiding the
 * No column moves Staff Name into the first slot, and with a fixed `left: 5rem` on the second
 * slot it would have been pushed 5rem into the table and left a visible hole.
 */
/**
 * The second pinned column's `left` is MEASURED, not derived from a declared width. The columns are
 * percentage shares now, so the first column's pixel width depends on the viewport and changes when a
 * column is hidden or shown - the old `left: 5rem` read the No column's declared width, which would
 * leave the sticky pair misaligned at any width where 5rem is not the No column's real width.
 *
 * The measurement skips zero-width cells on purpose: a hidden column keeps its <th> in the DOM at
 * width 0, so "the first th" is not necessarily the first VISIBLE column.
 */
const pinOffset = ref(0)

function measurePinOffset() {
  const ths = [...(tableEl.value?.querySelectorAll('thead tr th') ?? [])] as HTMLElement[]
  const firstVisible = ths.find((th) => th.getBoundingClientRect().width > 0)
  const w = firstVisible?.getBoundingClientRect().width ?? 0
  if (w) pinOffset.value = Math.round(w)
}

onMounted(() => {
  window.addEventListener('resize', measurePinOffset)
  measurePinOffset()
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', measurePinOffset)
})

function pinClass(i: number) {
  if (i === 0) return 'pl-pin pl-pin-first'
  if (i === 1) return 'pl-pin'
  return ''
}

function pinStyle(i: number) {
  if (i !== 1) return undefined
  return { left: `${pinOffset.value}px` }
}

/** Kept in sync with COLUMNS by key; the modal groups the fields into three sections. */
const FORM_SECTIONS = [
  {
    title: 'Identity',
    icon: 'i-lucide-user-round',
    fields: ['staff_name', 'email', 'gid']
  },
  {
    title: 'Hardware',
    icon: 'i-lucide-hard-drive',
    fields: ['japan_hostname', 'computer_model', 'computer_sn', 'tanggal', 'chassis', 'manufacturer']
  },
  {
    title: 'System and placement',
    icon: 'i-lucide-wrench',
    fields: ['os_name', 'os_arch', 'lokasi', 'remark2', 'remark3']
  }
] as const

const LABELS: Record<string, string> = {
  nomor: 'No',
  staff_name: 'Staff Name',
  email: 'Email Address',
  gid: 'GID',
  japan_hostname: 'JAPAN Hostname',
  computer_model: 'Computer Model',
  computer_sn: 'Computer S/N',
  tanggal: 'Date',
  chassis: 'Computer Chassis',
  manufacturer: 'Computer Manufacturer',
  os_name: 'Computer O/S Name',
  os_arch: 'Computer O/S Architecture',
  lokasi: 'Location',
  remark2: 'Remark1',
  remark3: 'Remark2'
}

/* ---------------- data ---------------- */
const loading = ref(true)
const saving = ref(false)
const deleting = ref(false)
const exporting = ref(false)
const loadError = ref('')
const entities = ref<EntityMeta[]>([])
const rows = ref<RecordRow[]>([])
const searching = ref('')
/**
 * Sentinel for "no filter". Reka UI refuses an empty string as a SelectItem value — the option
 * is read as "clear the selection and show the placeholder" and it throws, which took the whole
 * page to a 500 the first time this ran. An explicit sentinel is also clearer at the call site
 * than a falsy check that happens to mean "all".
 */
const ALL = '__all__'
const filterChassis = ref(ALL)
const filterLocation = ref(ALL)
const sortKey = ref<string>('nomor')
const sortDir = ref<'asc' | 'desc'>('asc')

const entity = computed(() => entities.value.find((e) => e.slug === ENTITY_SLUG))
const entityId = computed(() => entity.value?.id ?? 0)

const cell = (r: RecordRow, key: string): string => {
  const v = r.values?.[key]
  if (v === null || v === undefined) return ''
  return String(v)
}

/**
 * The haystack is built from the DELIBERATELY VISIBLE columns only. The CCTV page once had a
 * bug where its search ran over `JSON.stringify(row.values)` and therefore matched base64
 * signature data; this page has no blobs, but the principle stands — search what the user can
 * see, nothing else.
 */
function haystack(r: RecordRow): string {
  return COLUMNS.map((c) => cell(r, c.key)).join(' ').toLowerCase()
}

const visibleRows = computed(() => {
  const q = searching.value.trim().toLowerCase()
  let out = rows.value
  if (q) out = out.filter((r) => haystack(r).includes(q))
  if (filterChassis.value !== ALL) out = out.filter((r) => cell(r, 'chassis') === filterChassis.value)
  if (filterLocation.value !== ALL) out = out.filter((r) => cell(r, 'lokasi') === filterLocation.value)

  const key = sortKey.value
  const dir = sortDir.value === 'asc' ? 1 : -1
  return [...out].sort((a, b) => {
    // The No column is padded text ("1", "10", "2"), so compare it numerically or 10 sorts
    // before 2 - the same class of bug as ordering CCTV's nomor as a string.
    if (key === 'nomor') {
      return ((Number(cell(a, key)) || 0) - (Number(cell(b, key)) || 0)) * dir
    }
    if (key === 'tanggal') {
      const ta = Date.parse(cell(a, key)) || 0
      const tb = Date.parse(cell(b, key)) || 0
      return (ta - tb) * dir
    }
    return cell(a, key).localeCompare(cell(b, key), undefined, { sensitivity: 'base' }) * dir
  })
})

/**
 * ROW MOVEMENT, driven here rather than by Vue's `move-class`, for the same measured reason the
 * columns are: Vue's TransitionGroup movement is unreliable on this table. With a leave transition
 * present it never ran at all - leaving rows hold their space, so the survivors' positions are
 * unchanged when the move is measured, no transform is ever applied, and the accumulated height was
 * released in a single frame when the leave ended (measured: 0 frames carrying a transform during a
 * chassis filter, plus a ~250px step). With the leave removed it ran on one run and not on another.
 *
 * THE DISTANCE IS COMPUTED FROM THE DATA, NOT MEASURED FROM THE DOM. That is the fix for a bug that
 * only appeared on the FILTER path, and it came out of instrumenting rather than reasoning: with a
 * temporary logger in this callback, a chassis filter reported
 * `{ beforeCount: 26, afterCount: 26, same: true }` - the table still held the pre-filter rows one
 * tick later, one animation frame later, and even from a `flush: 'post'` hook. So a DOM measurement
 * reliably reads zero movement there and the FLIP silently does nothing, while the very same code
 * works on a SORT (measured: 22 frames carrying a transform, 20 frames of travel) because on that
 * path the patch lands inside the tick.
 *
 * Every row in this table is the same height (measured 43px, min === max in every sample), so a
 * row's displacement is exactly (old visible index - new visible index) x row height - computable
 * from the two lists alone, with no dependence on patch timing. Rows that are new or removed are
 * skipped: the enter animation owns the new ones, and a removed row needs no transform.
 *
 * A `translateY` on a `<tr>` does not break the pinned columns: the sticky pair was sampled during a
 * row move and its left stayed at the scroller's left edge in every frame.
 */
const ROW_ANIM_MS = 340
const ROW_ANIM_MAX_MS = 560

/**
 * LONGER TRAVELS GET MORE TIME, so every row moves at roughly the same speed instead of the distant
 * ones flashing past while a near neighbour drifts into place. The flat 340ms baseline was measured
 * against a 94px glide, which it suits; on a sort a row travels up to 860px in that same time, and
 * large displacements are what read as rough.
 */
function rowAnimMs(dy: number): number {
  return Math.min(ROW_ANIM_MAX_MS, ROW_ANIM_MS + Math.round(Math.max(0, Math.abs(dy) - 60) / 3))
}

/* PINNING IS SELF-CORRECTING, recomputed on each frame from `offsetTop`, and the reason is a defect
   the first version really had: it pinned a FIXED delta (index difference x row height) in one shot,
   but Vue's patch lands later than that hook on this path, so the row was pushed a full delta away
   from a STALE layout and visibly popped before gliding. Measured on the first attempt: a row at 918
   jumped to 1004 for a frame, then glided up to 826.

   `offsetTop` is the LAYOUT position and transforms do not affect it, so `old offset - current
   offset` is exactly the transform that holds a row at its old VISUAL spot - correct whether the
   patch has landed or not, and correct mid-flight. The pin is re-applied each frame until the layout
   has actually moved and then held one more frame, which is when the glide is released.

   The old offsets are captured in a PRE-flush watcher, where the DOM is still the old layout.
   (`oldV` from a post watcher would also give the old ORDER, but not the old POSITIONS, and this
   needs positions.) */
let rowOffsetsBefore: Record<string, number> | null = null

/** The scroll area's height before the change, so the glide cannot resize the scrollbar. */
let rowScrollBefore = 0

watch(visibleRows, () => {
  const rows = tableEl.value?.querySelectorAll('tbody tr')
  if (!rows?.length) { rowOffsetsBefore = null; return }
  const out: Record<string, number> = {}
  rows.forEach((tr) => {
    const el = tr as HTMLElement
    if (el.dataset.id) out[el.dataset.id] = el.offsetTop
  })
  rowOffsetsBefore = out
  const scroller = tableEl.value?.parentElement
  rowScrollBefore = scroller ? scroller.scrollHeight : 0
})

watch(visibleRows, (v) => {
  const before = rowOffsetsBefore
  rowOffsetsBefore = null
  if (!before) return
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return

  const ids = new Set(v.map((r) => String(r.id)))
  const all = tableEl.value?.querySelectorAll('tbody tr')
  if (!all?.length) return

  // Rows present in BOTH lists. New rows are the enter animation's business; removed rows need
  // nothing, and a removed element must not be touched or it would shift on its last frame.
  const movers = ([...all] as HTMLElement[]).filter((el) => {
    const id = el.dataset.id
    return !!id && before[id] !== undefined && ids.has(id)
  })
  // THE SCROLL AREA'S HEIGHT IS FROZEN FOR EVERY ROW-SET CHANGE, including one where nothing has to
  // travel. A transformed row still counts toward the scroll container's scrollable overflow, so
  // pinning rows back to lower positions stretched `scrollHeight` while the real content had just
  // got shorter, and the extent moved on every frame of the animation - which repaints the thumb
  // every frame. Measured through a chassis filter: scrollHeight ran 813 -> 795 -> 779 -> 765 -> 753
  // -> 744 -> 736 -> 730 -> 727 against a clientHeight of 727. Holding the table at its pre-change
  // height keeps scrollHeight constant for the whole glide.
  //
  // It is applied BEFORE the movers check on purpose: a filter whose surviving rows happen to keep
  // their positions (the Notebook filter, 16 rows down to 2) has nothing to animate, and the first
  // version returned early there and left the extent unfrozen - which is the gap that was still
  // flickering. When there is nothing to glide, the extent is given back on a short timeout instead.
  const unfreeze = () => { if (tableWrapEl.value) tableWrapEl.value.style.minHeight = '' }
  if (tableWrapEl.value && rowScrollBefore) tableWrapEl.value.style.minHeight = `${rowScrollBefore}px`

  if (!movers.length) {
    window.setTimeout(unfreeze, 60)
    return
  }

  const lastDy = new Map<HTMLElement, number>()
  const pin = () => {
    movers.forEach((el) => {
      const id = el.dataset.id as string
      const dy = before[id] - el.offsetTop
      lastDy.set(el, dy)
      el.style.transition = 'none'
      el.style.transform = Math.abs(dy) < 1 ? '' : `translateY(${dy}px)`
    })
  }
  const release = () => {
    movers.forEach((el) => {
      // A GENTLER OVERSHOOT than the column bounce: on a 43px row the stronger curve
      // (0.34, 1.56, 0.64, 1) reads as a wobble at the end of the glide rather than a landing, and the
      // glide itself is what HIRO asked to smooth out.
      el.style.transition = `transform ${rowAnimMs(lastDy.get(el) ?? 0)}ms cubic-bezier(0.22, 1.06, 0.36, 1)`
      el.style.transform = ''
    })
    window.setTimeout(() => {
      movers.forEach((el) => { el.style.transition = ''; el.style.transform = '' })
      unfreeze()
    }, ROW_ANIM_MAX_MS + 140)
  }

  // The release fires on the FIRST frame the layout is seen to have moved. Requiring it to still be
  // moved on a second frame cost 16ms of dead time at the start of every glide - a stall the eye reads
  // as a hitch exactly when the filter is applied. The ceiling stays as the safety net.
  let frames = 0
  const step = () => {
    pin()
    const movedNow = movers.some((el) => Math.abs(before[el.dataset.id as string] - el.offsetTop) >= 1)
    if (movedNow || ++frames > 8) { release(); return }
    requestAnimationFrame(step)
  }
  requestAnimationFrame(step)
}, { flush: 'post' })

// The register is loaded asynchronously, so the first real measurement of the pinned offset can only
// happen once the rows exist. This watcher sits HERE, not next to measurePinOffset(), because `rows`
// is declared in this section: placed any earlier, `watch(undefined, ...)` throws during setup and the
// whole page fails to render (measured: a 500 reading "Cannot read properties of undefined").
watch(rows, () => { measurePinOffset() }, { flush: 'post' })

/* The Department field is gone from this page entirely (HIRO: "hapus kolom departement dari page pc
   ledger, form add juga. departement tidak dipakai di page pc ledger"), so `departments` went with it -
   it had survived the removal of the Department FILTER only to seed the form's Department input, and
   that input no longer exists. `locations` is the filter's source, built the same way as `chassisTypes`
   so the two behave identically. */
const chassisTypes = computed(() =>
  [...new Set(rows.value.map((r) => cell(r, 'chassis')).filter(Boolean))].sort()
)
const locations = computed(() =>
  [...new Set(rows.value.map((r) => cell(r, 'lokasi')).filter(Boolean))].sort()
)

function toggleSort(key: string) {
  if (sortKey.value === key) sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  else { sortKey.value = key; sortDir.value = 'asc' }
}

/* ---------------- formatting ---------------- */
function fmtDate(v: unknown): string {
  if (!v) return ''
  const d = new Date(String(v))
  if (Number.isNaN(d.getTime())) return String(v)
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

/** `yyyy-mm-dd` for <input type="date">, from whatever the API stored. */
function toDateInput(v: unknown): string {
  if (!v) return ''
  const s = String(v)
  const d = new Date(s)
  if (Number.isNaN(d.getTime())) return ''
  return d.toISOString().slice(0, 10)
}

/* ---------------- load ---------------- */
async function load() {
  loading.value = true
  loadError.value = ''
  try {
    entities.value = await apiListEntities(true)
    const id = entityId.value
    if (!id) throw new Error('entity pc_ledger not found')
    const page = await apiListRecords(id, { page: 1, pageSize: 500 })
    rows.value = page.items ?? []
  } catch (e) {
    loadError.value = 'Cannot load the PC Ledger. Check that the API is running.'
    console.error('[pc-ledger]', e)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadColumnChoice()
  load()
})

/* ---------------- add / edit ---------------- */
const showForm = ref(false)
const editingId = ref<number | null>(null)
const form = reactive<Record<string, any>>({})
const formError = ref('')

function blankForm() {
  for (const k of Object.keys(LABELS)) form[k] = ''
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  blankForm()
  showForm.value = true
}

function openEdit(row: RecordRow) {
  editingId.value = row.id
  formError.value = ''
  blankForm()
  for (const k of Object.keys(LABELS)) {
    form[k] = k === 'tanggal' ? toDateInput(row.values?.[k]) : (row.values?.[k] ?? '')
  }
  showForm.value = true
}

/**
 * `nomor` is deliberately never sent. The API's LogbookNumberService fills a required+unique
 * field named `nomor` on create, and sending our own value would either collide with that or
 * rename an existing record's number on edit.
 */
function payload(): Record<string, any> {
  const values: Record<string, any> = {}
  for (const [k, v] of Object.entries(form)) {
    if (k === 'nomor') continue
    if (v === '' || v === null || v === undefined) continue
    values[k] = k === 'tanggal' ? String(v).slice(0, 10) : v
  }
  return values
}

async function save() {
  if (!entityId.value) return
  formError.value = ''
  saving.value = true
  try {
    if (editingId.value) {
      await apiUpdateRecord(entityId.value, editingId.value, payload())
    } else {
      await apiCreateRecord(entityId.value, payload())
    }
    showForm.value = false
    await load()
    toast.add({
      title: editingId.value ? 'Record updated' : 'Record created',
      icon: 'i-lucide-circle-check',
      color: 'success'
    })
  } catch (e: any) {
    formError.value = e?.data?.message || e?.message || 'Could not save the record.'
  } finally {
    saving.value = false
  }
}

/* ---------------- delete ---------------- */
const showDelete = ref(false)
const deleteTarget = ref<RecordRow | null>(null)

function askDelete(row: RecordRow) {
  deleteTarget.value = row
  showDelete.value = true
}

async function confirmDelete() {
  const target = deleteTarget.value
  if (!target || !entityId.value) return
  deleting.value = true
  try {
    await apiDeleteRecord(entityId.value, target.id)
    showDelete.value = false
    await load()
    toast.add({ title: 'Record deleted', icon: 'i-lucide-trash-2', color: 'success' })
  } catch (e: any) {
    toast.add({
      title: 'Delete failed',
      description: e?.data?.message || e?.message || '',
      icon: 'i-lucide-triangle-alert',
      color: 'error'
    })
  } finally {
    deleting.value = false
  }
}

/* ---------------- export ---------------- */
/**
 * Reproduces the reference workbook, not a generic table dump:
 *   row 1   the form title, bold 16pt, merged across the table
 *   row 3-6 the "Requests to IT Reps" notes, in column C exactly as the source has them
 *   row 9   "Department:" with the value beside it
 *   row 10  the header, white bold on a dark fill, matching the source's header row
 *   row 11+ one row per record, every column except No carrying the source's light-yellow
 *           FFFFFFCC fill, which is what tells the IT reps what they are allowed to edit
 * Column widths are the source's own (including the 2.44-wide spacer in column A), so the file
 * opens looking like the spreadsheet it replaces rather than like a raw export.
 */
const SHEET_WIDTHS = [2.44, 6.11, 35.89, 45.44, 15.89, 23.33, 25.66, 21, 17.66, 23, 23.55, 35.66, 18.89, 42.11, 42.66, 33.33]
const YELLOW = 'FFFFFFCC'
const HEADER_FILL = 'FF233D4D'

const NOTES = [
  'Requests to IT Reps:-',
  '1. Please fill in yellow columns',
  '2. For corporate PCs that is not listed, do insert new rows and inform ISD.  ',
  '3. Factory Used PC will not be include in this List. \nPlease record by yourself in another separate list.'
]

async function exportExcel() {
  const data = visibleRows.value
  if (!data.length) {
    toast.add({ title: 'Nothing to export', description: 'No records match the current filters.', icon: 'i-lucide-info', color: 'warning' })
    return
  }
  exporting.value = true
  try {
    const ExcelJS = (await import('exceljs')).default ?? (await import('exceljs'))
    const wb = new (ExcelJS as any).Workbook()
    wb.creator = 'INFRA-CAP'
    wb.created = new Date()

    const ws = wb.addWorksheet(SHEET_NAME, { views: [{ state: 'frozen', ySplit: 10 }] })
    SHEET_WIDTHS.forEach((w, i) => { ws.getColumn(i + 1).width = w })

    // --- title block ---
    ws.mergeCells(1, 1, 1, COLUMNS.length + 1)
    const title = ws.getCell(1, 1)
    title.value = 'IT FORM SG031 Department PC Ledger Form v7'
    title.font = { bold: true, size: 16 }
    title.alignment = { horizontal: 'center', vertical: 'middle' }
    ws.getRow(1).height = 21

    NOTES.forEach((text, i) => {
      const c = ws.getCell(3 + i, 3) // column C, matching the source sheet
      c.value = text
      c.font = { bold: true, size: 11 }
      c.alignment = { wrapText: true, vertical: 'top' }
    })
    ws.getRow(6).height = 28

    const deptLabel = ws.getCell(9, 3)
    deptLabel.value = 'Department:'
    deptLabel.font = { bold: true, size: 11 }

    /* `departemen` is read here even though nothing on the page shows the field any more. This is the
       workbook's own "Department:" cell, and a sheet without it would no longer match IT FORM SG031.
       It is not dead code: it is the single remaining reader of that field. The API merges on update,
       so the values on the existing records survive edits that no longer carry the field. */
    const deptValues = [...new Set(data.map((r) => cell(r, 'departemen')).filter(Boolean))]
    const deptCell = ws.getCell(9, 4)
    deptCell.value = deptValues.length === 1 ? deptValues[0] : deptValues.join(', ')
    deptCell.font = { bold: true, size: 11 }

    // --- header (row 10) ---
    const headerRow = ws.getRow(10)
    headerRow.height = 31.2
    ws.getCell(10, 2).value = 'No'
    COLUMNS.slice(1).forEach((c, i) => { ws.getCell(10, i + 3).value = c.label })
    for (let col = 2; col <= COLUMNS.length + 1; col++) {
      const c = ws.getCell(10, col)
      c.font = { bold: true, size: 12, color: { argb: 'FFFFFFFF' } }
      c.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: HEADER_FILL } }
      c.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true }
      c.border = { top: { style: 'thin' }, left: { style: 'thin' }, bottom: { style: 'thin' }, right: { style: 'thin' } }
    }

    // --- data (row 11+) ---
    data.forEach((r, i) => {
      const excelRow = 11 + i
      ws.getRow(excelRow).height = 15

      const noCell = ws.getCell(excelRow, 2)
      noCell.value = Number(cell(r, 'nomor')) || cell(r, 'nomor')
      noCell.alignment = { horizontal: 'center', vertical: 'middle' }

      COLUMNS.slice(1).forEach((c, ci) => {
        const target = ws.getCell(excelRow, ci + 3)
        const raw = cell(r, c.key)
        if (c.key === 'tanggal' && raw) {
          const d = new Date(raw)
          if (!Number.isNaN(d.getTime())) {
            target.value = d
            target.numFmt = 'dd-mmm-yy'
          }
        } else {
          target.value = raw
        }
        target.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: YELLOW } }
        target.alignment = { vertical: 'middle', wrapText: false }
        target.border = { top: { style: 'thin' }, left: { style: 'thin' }, bottom: { style: 'thin' }, right: { style: 'thin' } }
        target.font = { size: 11 }
      })
    })

    const buf = await wb.xlsx.writeBuffer()
    const blob = new Blob([buf], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    const stamp = new Date().toISOString().slice(0, 10)
    const suffix = (searching.value || filterChassis.value !== ALL || filterLocation.value !== ALL) ? '_filtered' : ''
    a.href = url
    a.download = `PC_Ledger_${stamp}${suffix}.xlsx`
    document.body.appendChild(a)
    a.click()
    a.remove()
    setTimeout(() => URL.revokeObjectURL(url), 2000)
    toast.add({
      title: 'Export ready',
      description: `${data.length} row${data.length === 1 ? '' : 's'} written to PC_Ledger_${stamp}${suffix}.xlsx`,
      icon: 'i-lucide-file-spreadsheet',
      color: 'success'
    })
  } catch (e) {
    console.error('[pc-ledger export]', e)
    toast.add({ title: 'Export failed', description: String((e as Error)?.message ?? e), icon: 'i-lucide-triangle-alert', color: 'error' })
  } finally {
    exporting.value = false
  }
}
</script>

<template>
  <UDashboardPanel :ui="{ body: 'p-6' }">
    <template #header>
      <PageHeader title="PC Ledger" />
    </template>

    <template #body>
      <div class="space-y-6">
        <!-- Sheet header: identity of the form + the actions, same shape as the CCTV register. -->
        <div class="anim-fade-up rounded-lg border border-default bg-elevated p-4">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div class="min-w-0">
              <h1 class="text-lg font-bold tracking-wide">PC Ledger</h1>
              <p class="text-xs text-muted">
                IT FORM SG031 &middot; Department PC Ledger Form v7
                <span v-if="!loading"> &middot; {{ rows.length }} record{{ rows.length === 1 ? '' : 's' }}</span>
              </p>
            </div>

            <div class="flex flex-wrap items-center gap-2">
              <UInput
                v-model="searching"
                icon="i-lucide-search"
                placeholder="Search staff, hostname, S/N, location..."
                class="w-72"
                :ui="{ base: 'h-9' }"
              >
                <template v-if="searching" #trailing>
                  <UButton color="neutral" variant="link" icon="i-lucide-x" size="xs" aria-label="Clear search" @click="searching = ''" />
                </template>
              </UInput>

              <USelect
                v-model="filterChassis"
                :items="[{ label: 'All chassis', value: ALL }, ...chassisTypes.map(d => ({ label: d, value: d }))]"
                class="w-40"
                :ui="{ base: 'h-9' }"
              />

              <USelect
                v-model="filterLocation"
                :items="[{ label: 'All locations', value: ALL }, ...locations.map(d => ({ label: d, value: d }))]"
                class="w-64"
                :ui="{ base: 'h-9' }"
              />

              <!-- Column chooser. A popover of checkboxes rather than a dropdown menu: it is a
                   persistent list with a live count, not a list of one-shot actions. -->
              <UPopover>
                <UButton
                  color="soft"
                  icon="i-lucide-columns-3"
                  :label="`Columns (${visibleColumns.length}/${COLUMNS.length})`"
                />
                <template #content>
                  <div class="w-64 p-3">
                    <div class="mb-2 flex items-center justify-between gap-2">
                      <p class="text-[11px] font-semibold uppercase tracking-wider text-muted">Visible columns</p>
                      <UButton
                        v-if="visibleColumns.length !== COLUMNS.length"
                        color="neutral"
                        variant="link"
                        size="xs"
                        label="Show all"
                        @click="showAllColumns"
                      />
                    </div>
                    <div class="max-h-72 space-y-0.5 overflow-y-auto">
                      <UCheckbox
                        v-for="c in COLUMNS"
                        :key="c.key"
                        :model-value="visibleKeys.includes(c.key)"
                        :label="c.label"
                        :disabled="visibleKeys.length <= 1 && visibleKeys.includes(c.key)"
                        variant="list"
                        @update:model-value="(v: any) => toggleColumn(c.key, !!v)"
                      />
                    </div>
                    <p class="mt-2 text-[11px] leading-snug text-muted">
                      Remembered for this browser. Excel keeps exporting all 15 columns.
                    </p>
                  </div>
                </template>
              </UPopover>

              <UButton
                color="soft"
                icon="i-lucide-file-spreadsheet"
                label="Excel"
                :loading="exporting"
                @click="exportExcel"
              />
              <UButton
                color="primary"
                icon="i-lucide-plus"
                label="Add Record"
                @click="openCreate"
              />
            </div>
          </div>
        </div>

        <UAlert
          v-if="loadError"
          color="error"
          variant="soft"
          icon="i-lucide-triangle-alert"
          :title="loadError"
        />

        <!-- Register -->
        <!-- A plain div, NOT a UCard, and that is what makes the footer match. UCard renders its
             #footer slot inside a wrapper carrying `p-4 sm:px-6`, so the bar came out 69px tall and
             24px narrower than the card on each side, with a transparent band above it and UCard's
             own `divide-y` drawing a second rule - while `bg-default/30` cannot paint outside that
             wrapper. Measured on the real pages: CCTV's bar is a direct child of the card, 37px
             tall and full width. Same class string as the CCTV card so the two registers share one
             shape rather than two similar ones. -->
        <div class="anim-fade-up overflow-hidden rounded-xl border border-default bg-elevated">
          <div v-if="loading" class="flex items-center justify-center gap-2 p-12 text-sm text-muted">
            <UIcon name="i-lucide-loader-circle" class="size-5 animate-spin" />
            Loading the register...
          </div>

          <div v-else-if="!rows.length" class="p-12 text-center">
            <UIcon name="i-lucide-hard-drive" class="mx-auto size-8 text-dimmed" />
            <p class="mt-3 text-sm font-medium">No PCs recorded yet</p>
            <p class="mt-1 text-xs text-muted">
              Start with Add Record, or copy the rows across from the existing spreadsheet.
            </p>
          </div>

          <div v-else class="pl-scroll">
            <!-- The frozen scroll extent lives on THIS wrapper, never on the table itself: a
                 min-height on a <table> is shared out among its rows, which stretched every row for
                 the length of the animation. See tableWrapEl in the script. -->
            <div ref="tableWrapEl">
            <table ref="tableEl" class="pl-table">
              <colgroup>
                <col
                  v-for="c in COLUMNS"
                  :key="c.key"
                  :style="{ width: isVisible(c.key) ? c.w + '%' : '0%' }"
                />
                <col style="width: 5%" />
              </colgroup>
              <thead>
                <tr>
                  <th
                    v-for="c in COLUMNS"
                    :key="c.key"
                    :style="pinStyle(visibleIndex(c.key))"
                    :class="[
                      c.align === 'center' ? 'text-center' : 'text-left',
                      isVisible(c.key) ? '' : 'pl-col-hidden',
                      pinClass(visibleIndex(c.key))
                    ]"
                    @click="toggleSort(c.key)"
                  >
                    <span class="inline-flex min-w-0 items-center gap-1" :title="c.label">
                      <span class="pl-th-label">{{ c.short ?? c.label }}</span>
                      <UIcon
                        v-if="sortKey === c.key"
                        :name="sortDir === 'asc' ? 'i-lucide-arrow-up' : 'i-lucide-arrow-down'"
                        class="size-3"
                      />
                    </span>
                  </th>
                  <th class="text-center">Actions</th>
                </tr>
              </thead>
              <!-- Row transitions. The ENTER animation is Vue's; the MOVE is not, and that is a
                   measured decision rather than a style preference.

                   Vue's TransitionGroup movement (`move-class`/`move-active-class`) was tried and it
                   is not reliable here: with a leave transition present it never runs at all (zero
                   frames carrying a transform during a chassis filter, because the leaving rows hold
                   their space and the survivors' positions are unchanged when the move is measured),
                   and with the leave removed it still did not run on one run while running on
                   another. Rows are therefore moved by the same self-driven FLIP as the columns -
                   snapshot the tops, `await nextTick()`, translate back and release on the curve -
                   which is deterministic, already proven in this file, and measurable.

                   A `translateY` on a `<tr>` does NOT break the pinned columns: the sticky pair was
                   sampled during a row FLIP and reported the same left in every frame. -->
              <TransitionGroup
                tag="tbody"
                enter-active-class="row-enter-active"
                enter-from-class="row-enter-from"
              >
                <tr v-for="row in visibleRows" :key="row.id" :data-id="row.id">
                  <td
                    v-for="c in COLUMNS"
                    :key="c.key"
                    :style="pinStyle(visibleIndex(c.key))"
                    :class="[
                      c.align === 'center' ? 'text-center' : '',
                      c.key === 'nomor' ? 'font-medium' : '',
                      'tabular-nums',
                      isVisible(c.key) ? '' : 'pl-col-hidden',
                      pinClass(visibleIndex(c.key))
                    ]"
                    :title="cell(row, c.key)"
                  >
                    <span class="pl-ellipsis">{{ c.key === 'tanggal' ? fmtDate(row.values?.[c.key]) : cell(row, c.key) }}</span>
                  </td>
                  <!-- Matched to the CCTV register's Actions cell: centred, and the two buttons
                       sit directly in the cell with no flex wrapper. The `gap-1` wrapper is gone
                       because CCTV has none - the buttons carry their own padding, so they read
                       as a pair there and now here too. `pl-actions` brings CCTV's narrower
                       `px-1 py-2` padding with it, which the shared `.pl-table td` rule cannot
                       express as a utility (it would lose on specificity). -->
                  <td class="pl-actions text-center">
                    <UButton color="neutral" variant="ghost" icon="i-lucide-pencil" size="xs" aria-label="Edit" @click="openEdit(row)" />
                    <UButton color="error" variant="ghost" icon="i-lucide-trash-2" size="xs" aria-label="Delete" @click="askDelete(row)" />
                  </td>
                </tr>
              </TransitionGroup>
            </table>
            </div>
          </div>

          <!-- Sibling of the scroll area, exactly like the CCTV register, so the bar spans the full
               card width and its `border-t` lands on the card's own edge instead of floating inside
               a padded slot. Same class string as CCTV's footer verbatim.
               The LEFT count follows the FILTERS so it can never disagree with the "Showing X of Y"
               beside it - the CCTV page once printed the raw total there and read
               "1 row / Showing 0 of 1", which looks like a bug. -->
          <div v-if="!loading && rows.length" class="flex items-center justify-between gap-3 border-t border-default bg-default/30 px-4 py-2.5 text-xs text-muted">
            <span>{{ visibleRows.length }} {{ visibleRows.length === 1 ? 'record' : 'records' }}</span>
            <span class="tabular-nums">Showing {{ visibleRows.length }} of {{ rows.length }}</span>
          </div>
        </div>
      </div>

      <!-- ---------------- add / edit ---------------- -->
      <UModal v-model:open="showForm" :ui="{ content: 'max-w-4xl pl-record-modal' }">
        <template #content>
          <!-- `relative z-10` puts the whole dialog above the accent glow that the .pl-record-modal
               rule paints as a ::after on the content element (see the non-scoped style block at the
               foot of this file). One class on this wrapper is enough - it covers the header, body
               and footer at once - where the CCTV and Users dialogs set it on their header and body
               separately because those two use the vendor's header/body slots. -->
          <div class="relative z-10 flex max-h-[85vh] flex-col">
            <div class="flex items-center gap-3 border-b border-default px-5 py-4">
              <span class="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary ring-1 ring-inset ring-primary/20">
                <UIcon :name="editingId ? 'i-lucide-pencil' : 'i-lucide-plus'" class="size-5" />
              </span>
              <div class="min-w-0">
                <h2 class="text-sm font-semibold leading-tight">{{ editingId ? 'Edit PC record' : 'New PC record' }}</h2>
                <p class="text-xs text-muted">
                  {{ editingId ? 'Changes are written to the ledger immediately.' : 'Add new PC to the ledger system.' }}
                </p>
              </div>
            </div>

            <div class="flex-1 overflow-y-auto px-5 py-4">
              <div v-for="section in FORM_SECTIONS" :key="section.title" class="mb-5 last:mb-0">
                <p class="mb-3 flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wider text-muted">
                  <UIcon :name="section.icon" class="size-3.5" />
                  {{ section.title }}
                </p>
                <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  <UFormField
                    v-for="key in section.fields"
                    :key="key"
                    :label="LABELS[key]"
                    :ui="{ label: 'text-xs font-medium' }"
                  >
                    <UInput
                      v-if="key === 'tanggal'"
                      v-model="form[key]"
                      type="date"
                      class="w-full"
                      :ui="{ base: 'h-9' }"
                    />
                    <UInput
                      v-else
                      v-model="form[key]"
                      class="w-full"
                      :ui="{ base: 'h-9' }"
                    />
                  </UFormField>
                </div>
              </div>

              <UAlert
                v-if="formError"
                color="error"
                variant="soft"
                icon="i-lucide-circle-alert"
                :title="formError"
                class="mt-4"
              />
            </div>

            <div class="flex items-center justify-end gap-2 border-t border-default px-5 py-3">
              <UButton color="neutral" variant="ghost" label="Cancel" :disabled="saving" @click="showForm = false" />
              <UButton color="primary" :label="editingId ? 'Save changes' : 'Add record'" :loading="saving" @click="save" />
            </div>
          </div>
        </template>
      </UModal>

      <!-- ---------------- delete ---------------- -->
      <UModal v-model:open="showDelete" :ui="{ content: 'max-w-md' }">
        <template #content>
          <!-- Same construction as the CCTV row-delete and Users delete dialogs: a wrapper that clips,
               a soft danger wash bleeding in from the top-right corner, the identity of the row on a
               raised box above that wash, and a footer band on the elevated surface. The wash is a
               real div here rather than a ::after, because it is part of the dialog's own content in
               those two dialogs as well and it carries no vendor class to hang a rule on. -->
          <div class="overflow-hidden rounded-xl">
            <div class="relative overflow-hidden px-5 pb-5 pt-5">
              <div
                aria-hidden="true"
                class="pointer-events-none absolute -right-16 -top-24 size-48 rounded-full bg-error/20 blur-3xl"
              />
              <div class="relative flex items-start gap-3">
                <span class="grid size-11 shrink-0 place-items-center rounded-full bg-error/10 text-error ring-1 ring-inset ring-error/25">
                  <UIcon name="i-lucide-trash-2" class="size-5" />
                </span>
                <div class="min-w-0">
                  <h2 class="text-sm font-semibold">Delete this record?</h2>
                  <p class="mt-1 text-xs text-muted">
                    The register row disappears from the ledger. This cannot be undone from the UI.
                  </p>
                </div>
              </div>

              <!-- The row identity is repeated because a destructive action on a register of
                   numbered records has to prove WHICH row is about to vanish. -->
              <dl v-if="deleteTarget" class="relative mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 rounded-lg bg-elevated/60 p-3 text-xs ring-1 ring-inset ring-default">
                <dt class="text-muted">No</dt>
                <dd class="truncate font-medium">{{ cell(deleteTarget, 'nomor') }}</dd>
                <dt class="text-muted">Staff Name</dt>
                <dd class="truncate">{{ cell(deleteTarget, 'staff_name') || '-' }}</dd>
                <dt class="text-muted">JAPAN Hostname</dt>
                <dd class="truncate tabular-nums">{{ cell(deleteTarget, 'japan_hostname') || '-' }}</dd>
                <dt class="text-muted">Location</dt>
                <dd class="truncate">{{ cell(deleteTarget, 'lokasi') || '-' }}</dd>
              </dl>
            </div>

            <div class="flex items-center justify-end gap-2 bg-elevated/40 px-5 py-4">
              <UButton color="neutral" variant="ghost" label="Cancel" :disabled="deleting" @click="showDelete = false" />
              <UButton color="error" label="Delete record" :loading="deleting" @click="confirmDelete" />
            </div>
          </div>
        </template>
      </UModal>
    </template>
  </UDashboardPanel>
</template>

<style scoped>
/* A 15-column register cannot fit any laptop, so the table scrolls horizontally and the
   scrollbar is reserved from the first paint (`scrollbar-gutter: stable`) - otherwise it
   appears when the rows load, steals ~15px of width, and every column jumps sideways in one
   frame. Same reasoning as the CCTV register. */
.pl-scroll {
  /* A FIXED height, not `max-height`, on HIRO's instruction: "buat tinggi table nya tetap seperti
     original, walau pun record banyak/dikit tidak berubah". With max-height the box grew and shrank
     with the row count, so every filter moved the scrollbar's own track and thumb - and on the
     extreme filter (16 rows down to 2) the box collapsed from 727px to 125px in the middle of the
     transition, which is a second, structural source of flicker that freezing the extent could not
     cover. At a fixed 70vh the track is the same height whether the register holds 26 rows or 2, and
     the footer below it cannot move either. The cost, accepted deliberately: a filtered-down
     register keeps a tall empty area under its last row.
     One more thing this box does NOT change: its height no longer depends on the rows at all, so the
     only thing left that can move is scrollHeight - which the row FLIP freezes while it runs. */
  height: 70vh;
  /* overflow-y-scroll, not auto, and not `overflow: auto` - matched to the CCTV register. The
     track is rendered at all times, so the scrollbar can never blink in and out as the row count
     crosses the threshold. `overflow-x: auto` stays: the horizontal bar is only useful when the
     table genuinely overflows sideways (here it always does, 4304px of table in a 1650px box). */
  overflow-y: scroll;
  overflow-x: auto;
  scrollbar-gutter: stable;
}

/* THE SCROLLBAR LOOK, copied from the CCTV register so the two registers match rather than merely
   resemble each other. Kept in this page's own scoped block instead of reaching for `.logbook-scroll`
   in cctvacc.vue: a shared class would couple two pages that are otherwise independent, and a later
   change to one register's scrollbar would silently restyle the other.
   12px track with a 2px inset border leaves an 8px pill: quiet, but unmistakably a scrollbar.
   The thumb is `--ui-text-dimmed` because `--ui-border` resolves to the SAME value as
   `--ui-bg-elevated`, the surface the bar sits on - a thumb painted its own background is invisible
   in either theme, which is the bug CCTV's comment records. */
.pl-scroll::-webkit-scrollbar {
  width: 12px;
  height: 12px;
}

.pl-scroll::-webkit-scrollbar-track {
  background-color: color-mix(in oklab, var(--ui-text-dimmed) 10%, transparent);
}

/* background-COLOR, not the `background` shorthand: the shorthand resets background-clip and would
   undo the pill shape. */
.pl-scroll::-webkit-scrollbar-thumb {
  background-color: var(--ui-text-dimmed);
  border-radius: 9999px;
  border: 2px solid transparent;
  background-clip: content-box;
}

.pl-scroll::-webkit-scrollbar-thumb:hover {
  background-color: var(--ui-text-muted);
  background-clip: content-box;
}

.pl-table {
  /* fixed, not auto: with auto layout the browser treats a declared width as a hint and
     redistributes the leftover unevenly, so two columns that declare the same width end up
     different at a wider viewport. */
  table-layout: fixed;
  /* A floor for narrow screens, with a percentage colgroup above it: the columns share the available
     width down to this point, then the register scrolls sideways rather than squeezing them into
     unreadable slivers. Same model as the CCTV register (full width + a min-width + % columns).
     `min-width: max-content` is gone with the rem widths - it was what pinned the table at 4304px. */
  min-width: 1200px;
  width: 100%;
  border-collapse: separate;
  border-spacing: 0;
  /* 14px, the CCTV register's `text-sm` - this was 13px, which made the two registers read as two
     different documents side by side. */
  font-size: 0.875rem;
}

.pl-table thead {
  /* STICKY ON THE THEAD, NOT ONLY ON EACH <th>, and it carries the band's background.
     That is what killed the light grey flash: with the colour painted only on the <th> elements,
     the horizontal movement of a column toggle left gaps between them for a few frames, and what
     showed through was the CARD's surface (--ui-bg-elevated, a lighter grey) - HIRO: "ketika filter
     kolom di tick/untick terlihat ada warna abu-abu terang". The thead spans the whole band, sticks
     with it, paints underneath the cells and therefore fills any such gap with the SAME colour.
     Also what the CCTV register does (`sticky top-0` on its thead). */
  position: sticky;
  top: 0;
  z-index: 10;
  background: color-mix(in oklab, var(--ui-bg) 60%, var(--ui-bg-elevated));
}

.pl-table th {
  position: sticky;
  top: 0;
  z-index: 2;
  /* Matched to CCTV's `bg-default/60 backdrop-blur-sm` header, as an OPAQUE mix. CCTV can get away
     with a translucent blurred header because nothing scrolls horizontally under its header; here
     the first two columns are pinned, so a translucent header would let the columns sliding
     underneath show through the header text. `color-mix(bg 60%, elevated)` is the exact colour
     CCTV's header renders to, so the two are indistinguishable where they are not scrolling. */
  background: color-mix(in oklab, var(--ui-bg) 60%, var(--ui-bg-elevated));
  border-bottom: 1px solid var(--ui-border);
  /* 8px rather than CCTV's 10px vertically, together with the short header labels above: the header
     used to wrap to two lines and stood 57px tall, which HIRO asked to bring down. One line plus this
     padding lands at 35px. The horizontal padding stays 12px so the text's left edge still lines up
     with the cell text below it. */
  padding: 0.5rem 0.75rem;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.025em;
  /* The accent colour, matching the No column - HIRO: "buat header tabel text color sesuai warna
     accent, sama seperti NO". Sorting no longer changes this colour (see the zebra note below), so
     the sorted column is marked by its arrow alone. */
  color: var(--ui-primary);
  /* THE HEADER IS ONE LINE AT EVERY VIEWPORT WIDTH - never two.
     Measured before this: 35px at 1920 but 53px at 1600 / 1440 / 1280, because the labels wrapped to a
     second line as the percentage columns narrowed - HIRO: "tolong adjust height nya header table ini".
     A header whose height changes with the window width reads as a bug.
     `nowrap` + `overflow: hidden` pins it to one line everywhere; the ellipsis itself belongs to
     `.pl-th-label` below, so that the sort arrow beside the label is never the thing that gets cut.
     The full name stays available in the column picker and in this header's own tooltip. */
  white-space: nowrap;
  overflow: hidden;
  cursor: pointer;
  user-select: none;
}

/* `.pl-table th.is-sorted` used to live here, recolouring the sorted column to the accent colour.
   With every header on the accent colour now, that rule had nothing left to do, so it and the
   class binding in the markup are both gone rather than left behind looking meaningful. The sorted
   column is marked by its arrow alone. */

.pl-table td {
  /* CCTV's cell metrics: px-3 py-2.5 and a 60% border. The border was full-strength, which drew a
     harder grid than the CCTV register shows. */
  padding: 0.625rem 0.75rem;
  border-bottom: 1px solid color-mix(in oklab, var(--ui-border) 60%, transparent);
  color: var(--ui-text);
  /* Equal-width digits in the app's own font, so codes and numbers line up column-wise. */
  font-variant-numeric: tabular-nums;
}

/* ALTERNATING ROW COLOURS - HIRO: "warna antar row buat selang seling biar memudahkan view user".
   THE FORMULA IS CCTV'S, taken from its rendered value rather than invented: CCTV paints the even
   row with `bg-default/20`, measured as --ui-bg at 20% alpha over the elevated card, which resolves
   to L 0.261. Applied here as an opaque mix against the same elevated surface so it renders as the
   identical colour, and stays opaque where a data cell is pinned.
   Worth knowing, because it is a property of CCTV's own band and not of this port: in DARK mode it
   is a clear step (0.261 against 0.274), but in LIGHT mode `--ui-bg` is white over an almost-white
   elevated surface, so the band nearly vanishes there too. Matching CCTV exactly means inheriting
   that. If the band is ever asked to carry its weight in light mode, the fix is a text-colour mix
   rather than a surface mix.

   TWO ORDERING DETAILS THAT ARE LOAD-BEARING:
   - This rule is declared BEFORE the hover rule below. Both are (0,2,0), so source order decides,
     and the hover must win or an even row would simply refuse to light up.
   - It targets every `td`, including the pinned ones. `.pl-pin` is only (0,1,0), so the band runs
     unbroken across the pinned pair instead of stopping at the first two columns - the same
     failure that made No and Staff Name look like a different table. The pinned HOVER rule is
     (0,3,0), so it still takes precedence over the band. */
.pl-table tbody tr:nth-child(even) td {
  background: color-mix(in oklab, var(--ui-bg) 20%, var(--ui-bg-elevated));
}

.pl-table tbody tr:hover td {
  /* CCTV's `hover:bg-primary/5`, verbatim - this was 6%, which was close enough to look wrong
     next to the CCTV register without being obviously a different number. */
  background: color-mix(in oklab, var(--ui-primary) 5%, transparent);
}

/* The first two VISIBLE columns stay pinned while the rest of the register scrolls under them:
   on a 15-column table the columns that identify a row are useless once they scroll away.
   Driven by a class rather than `nth-child`, because the pinned pair changes when the user hides
   a column - with a fixed `left: 5rem` on the second slot, hiding No would have pushed Staff Name
   5rem into the table and left a visible hole. The second slot's `left` is set inline from the
   first column's own declared width. */
.pl-pin {
  position: sticky;
  z-index: 3;
  /* The opaque colour must be the surface the table ACTUALLY sits on, which is now
     `--ui-bg-elevated` - the card was moved onto the elevated surface to match the CCTV register.
     It was `--ui-bg` while this card sat on the base surface, and it has to move with the card:
     picking the wrong token is exactly what once painted the No and Staff Name columns a shade off
     every other column (HIRO: "pada tabel no dan staff name warna nya kenapa berbeda?"). A sticky
     column must stay opaque so the columns scrolling underneath cannot show through it; the only
     question is WHICH opaque colour. */
  background: var(--ui-bg-elevated);
}

.pl-pin-first {
  left: 0;
}

/* There is deliberately NO divider on the right edge of the second pinned column. There used to be
   one - `box-shadow: 1px 0 0 var(--ui-border)` - which drew a line between Staff Name and Email
   Address; HIRO asked for it gone. The cost, accepted knowingly: while the register is scrolled
   sideways there is no visible seam where the pinned pair ends, only the opaque background hiding
   what passes underneath. */

.pl-table th.pl-pin {
  z-index: 4;
  /* No background of its own any more. The header cells all share ONE colour (`.pl-table th`
     above), so the pinned two cannot disagree with the rest of the band - and because that colour
     is already opaque, a pinned header is still solid when the columns slide under it. All this
     rule carries now is the higher z-index, which keeps the pinned header above the pinned cells
     rather than below them. */
}

/* A pinned cell must stay OPAQUE. A translucent hover would let the columns scrolling underneath
   show through it, which is the classic broken sticky-column look - so the hover tint is mixed
   into the opaque surface rather than layered over it. */
.pl-table tbody tr:hover td.pl-pin {
  background: color-mix(in oklab, var(--ui-primary) 5%, var(--ui-bg-elevated));
}

/* CCTV's Actions cell padding (measured 8px 4px, `px-1 py-2`) is narrower than a data cell's, which
   is what keeps the two icon buttons reading as one pair. It cannot be expressed as a utility here:
   a Tailwind class would land at (0,1,0) and `.pl-table td` above is (0,1,1), so the utility would
   simply lose. Hence a rule keyed to the cell's own class. */
.pl-table td.pl-actions {
  padding: 0.5rem 0.25rem;
}

/* ONE FONT IN THIS TABLE. Email, GID, JAPAN Hostname and S/N used to be rendered in a monospace
   stack, which made the register look like two different documents spliced together - HIRO:
   "font nya samakan dong jangan beda-beda". Alignment, which is what the monospace was actually
   buying, now comes from `font-variant-numeric: tabular-nums` on the cells above: equal-width
   digits in the same Public Sans, so codes still line up without a second typeface. */

/* ---- row transitions ----------------------------------------------------------------
   Taken from the CCTV register (cctvacc.vue) instead of reinvented, so the two registers behave
   identically: 220ms on the app's monotonic cubic-bezier(0.22, 1, 0.36, 1), a FLIP move for the
   rows that SURVIVE a filter change, and an 8px rise for the rows that newly match.
   TransitionGroup is used because it is the only mechanism that knows which rows a filter change
   actually added or moved; a class-keyed animation would re-run on all 26 rows on every keystroke,
   including the twenty that did not change.

   THE MOVE IS DRIVEN IN THE SCRIPT, not by `move-class`: rows are moved by the same self-driven
   FLIP as the columns (the `watch(visibleRows, ...)` pin above). Measured reason: Vue's move
   handling never ran with a leave transition present (zero frames carrying a transform on a chassis
   filter, because the leaving rows hold their space and the survivors' positions are identical when
   the move is measured), and it was inconsistent once the leave was removed.

   THERE IS NO LEAVE ANIMATION, and that is the fix rather than an omission. Two versions were built
   and both made the chassis filter worse: holding leaving rows in the DOM is exactly what stops the
   survivors from moving smoothly, and the leftover height was released in a single frame either way
   (~250px measured). Filtered-out rows now disappear in the frame the filter applies, as they do in
   the CCTV register. */
/* New rows rise into place on the same overshooting curve as the move, so a row that arrives
   after a filter lands with the same little bounce as the rows that travelled. The OPACITY stays
   on the monotonic curve on purpose: a non-monotonic curve would push opacity past 1 mid-flight
   (clamped, so harmless, but it shortens the visible fade for no gain). */
.row-enter-active {
  transition: opacity 220ms cubic-bezier(0.22, 1, 0.36, 1), transform 340ms cubic-bezier(0.34, 1.56, 0.64, 1);
}

.row-enter-from {
  opacity: 0;
  transform: translateY(-8px);
}

/* THE CURVE OVERSHOOTS ON PURPOSE - a small bounce as the rows land against each other after a
   filter, which is what HIRO asked for. cubic-bezier(0.34, 1.56, 0.64, 1) is the classic
   ease-out-back: the row travels a little past its final line and settles back into it.
   WHY THIS IS SAFE despite the app's monotonic-curve rule, which exists because a non-monotonic
   curve can leave an element off its settled value: a CSS transition ALWAYS ends exactly on its
   target, and the target here is `transform: none`, so the row lands precisely on its grid line.
   The overshoot only exists mid-flight. The rule was also written for scaled TEXT (a glyph
   rasterises soft at an in-between size); this moves rows, it does not scale anything.
   340ms rather than the CCTV register's 220ms: on a 43px row pitch a shorter overshoot is not
   perceptible, it just reads as a slightly late landing.
   The script writes exactly this curve onto the moving rows as an inline transition, so it is ONE
   definition used by two mechanisms. The `.row-move` / `.row-move-active` classes that used to
   carry it are gone, removed together with Vue's move handling rather than left behind looking like
   they still move rows. */

/* THERE ARE NO `.row-leave-*` RULES ANY MORE, and their absence is the fix rather than an omission.
   A leave transition was tried in two versions and both made the chassis filter worse, because the
   problem was never how the leaving rows animated - it was that animating them at all stops the
   surviving rows from moving smoothly:

   - Leaving the rows in the DOM holds their space, so the survivors' positions are unchanged when
     the FLIP move is measured at patch time. The move transition therefore never runs (measured:
     zero frames with a transform on any row), and when the leave finally ends the accumulated
     height is released in a single frame - 250px in one frame on a chassis filter, which is the
     jerkiness HIRO reported.
   - Collapsing the leaving row's padding first did not help: it only gave back 16-18px of the 43px
     row, so the remainder was still released at once. Adding `font-size: 0` to collapse the rest
     released the text's line box instantly instead of animating it, and the buttons in the Actions
     cell held the row at ~25px in any case (measured: 24px button inside a 43px row).

   With no leave transition the rows are removed in the frame the filter applies, the survivors'
   positions change immediately, and the FLIP runs: measured 19 frames carrying a transform and 13
   consecutive frames of motion on a single row, gliding and springing onto its new position. */

/* ---- column show / hide -----------------------------------------------------------------
   TWO mechanisms were tried here and only one of them is kept, because keeping both hides the
   effect the user actually asked for.

   WHAT WAS TRIED AND REMOVED: `transition: width` on the <col>/th/td. It does animate in this
   table (a cell was measured going 256px -> 32px through 14 values), and it slides the neighbouring
   columns because the collapsing column narrows progressively. But it cannot BOUNCE: the curve
   would have to overshoot past a width of zero, and a negative width is invalid, so the collapse
   always lands flat. Worse, it also absorbed the movement that the FLIP below needs to see - the
   cells' positions changed by 0 at the moment the FLIP measured them, so the transform was never
   applied (measured: 0 frames with a transform while the column still slid smoothly).

   WHAT IS KEPT: the width snaps and the FLIP animates the cells that had to move, on the
   overshooting curve. That is the "columns collide and settle" effect requested. */
.pl-col-hidden {
  width: 0 !important;
  padding-left: 0 !important;
  padding-right: 0 !important;
  overflow: hidden;
}

/* No fade on the hidden cells either: the column is zero-width and clipped, so its content is
   already gone by the time an opacity transition would have started. A rule there would only be
   decoration pretending to be an animation. */

.pl-ellipsis {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  transition: opacity 200ms ease-out;
}

/* The header label that truncates. It owns the ellipsis rather than the <th>, because the sort arrow
   sits beside it in the same flex row: truncating at the <th> would clip the arrow instead of the text.
   `min-width: 0` on the parent span lets this shrink below its content width. */
.pl-th-label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (prefers-reduced-motion: reduce) {
  /* Cancel outright rather than shorten - a partial fade is still motion, which is exactly what
     the OS setting asks us to avoid. Same rule as main.css and as the CCTV register. */
  .row-enter-active {
    transition: none !important;
    opacity: 1 !important;
    transform: none !important;
  }
}
</style>

<!-- NON-SCOPED, and it has to be. UModal teleports its content to <body>, so a scoped rule - which
     compiles to .pl-record-modal[data-v-xxx] - can never match the dialog element, because it carries
     no scope id from this component. Same reason the CCTV and Users dialogs keep this CSS in a plain
     <style> block. -->
<style>
/* ---- accent glow in the top-right corner of the record dialog ----
   The same recipe as the Users and CCTV dialogs, which was measured against the reference template:
   it is NOT a CSS gradient, it is a plain circle of `bg-primary` with a large blur, so the
   "gradient" is nothing but the blur falloff. `bg-primary` is a semantic token, so the glow follows
   whichever accent the user picked in the sidebar Appearance menu.

   HIRO: "style background modal new pc record samakan dengan yang lain" - before this the dialog had
   no glow at all, which is why it read as flatter than the rest of the app.

   `pointer-events: none` is essential: the orb sits over the header, and without it it would swallow
   clicks on the close button. z-index 0 keeps it behind the dialog's own content, which carries
   `relative z-10` in the component's markup. */
.pl-record-modal[data-slot='content']::after {
  content: '';
  position: absolute;
  top: -110px;
  right: -90px;
  width: 250px;
  height: 250px;
  border-radius: 9999px;
  background-color: var(--ui-primary);
  opacity: 0.14;
  filter: blur(60px);
  pointer-events: none;
  z-index: 0;
}

/* Light mode needs a lighter touch still: the value that is barely perceptible on the dark surface
   turns into a visible stain on a near-white one. Same override the Users dialog uses. */
:root:not(.dark) .pl-record-modal[data-slot='content']::after {
  opacity: 0.09;
}
</style>
