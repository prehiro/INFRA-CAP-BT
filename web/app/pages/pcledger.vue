<script setup lang="ts">
import { loadNextPcHostname } from '~/utils/pcHostnames'

/**
 * PC Ledger — the app's replacement for the manual Excel sheet (IT FORM SG031, sheet "Ledger").
 *
 * COLUMN SET AND ORDER ARE NOT A DESIGN CHOICE. They are the reference workbook's 16 columns,
 * in the reference's order, because the whole point of the page is that its export can be
 * handed back to the department as the workbook they already use. The entity in the API
 * (`pc_ledger`, id 10) was created to mirror it field for field.
 *
 * The 15 columns below are the workbook's columns, in the workbook's order, and the export writes them
 * exactly as the sheet has them. One field that used to be involved is gone from BOTH surfaces now -
 * HIRO had it taken off the page first and then out of the Excel export - so nothing on this page reads
 * it any more, and row 9 of the generated sheet is left empty where it used to sit. It is still stored
 * on the entity, with its values intact, because removing data is not something a UI change should do.
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

/**
 * "Copied!" used to arrive twice: a tooltip on hover and a toast in the page corner. HIRO:
 * "hilangkan tooltip nya, dan ketika user click muncul toast Copied! di area pointer" - so the
 * confirmation now appears exactly where the click happened and removes itself.
 * An array rather than one value because rapid clicks on different cells must each get their own
 * pill; the oldest is dropped once four are alive, so a stray double-click cannot pile up.
 */
type CopyPill = { id: number; x: number; y: number }
const copyPills = ref<CopyPill[]>([])
const copyAnnounce = ref('')
let copyPillSeq = 0
function showCopyPill(event?: MouseEvent) {
  // Keyboard activation reports 0/0 for the pointer, so centre the pill on the button instead.
  const rect = (event?.currentTarget as HTMLElement | null)?.getBoundingClientRect()
  const hasPointer = Boolean(event && (event.clientX || event.clientY))
  const x = hasPointer ? event!.clientX : rect ? rect.left + rect.width / 2 : window.innerWidth / 2
  const y = hasPointer ? event!.clientY : rect ? rect.top + rect.height / 2 : window.innerHeight / 2
  const id = ++copyPillSeq
  copyPills.value = [...copyPills.value, { id, x, y }].slice(-4)
  copyAnnounce.value = ''
  requestAnimationFrame(() => { copyAnnounce.value = 'Copied!' })
  window.setTimeout(() => { copyPills.value = copyPills.value.filter(p => p.id !== id) }, 1200)
}

async function copyCellText(value: string, event?: MouseEvent) {
  if (!value) return
  try {
    if (window.isSecureContext && navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(value)
    } else {
      // Office deployment uses HTTP: retain a user-gesture fallback there.
      const previous = document.activeElement as HTMLElement | null
      const selection = window.getSelection()
      const ranges = selection ? Array.from({ length: selection.rangeCount }, (_, i) => selection.getRangeAt(i).cloneRange()) : []
      const input = document.createElement('textarea')
      input.value = value
      input.readOnly = true
      input.style.cssText = 'position:fixed;left:-9999px;top:0;opacity:0'
      document.body.appendChild(input)
      try {
        input.select()
        if (!document.execCommand('copy')) throw new Error('Clipboard unavailable')
      } finally {
        input.remove()
        previous?.focus({ preventScroll: true })
        if (selection) { selection.removeAllRanges(); ranges.forEach(range => selection.addRange(range)) }
      }
    }
    showCopyPill(event)
  } catch {
    toast.add({ id: 'pc-cell-copy', title: 'Unable to copy', description: 'Allow clipboard access in your browser or copy the text manually.', color: 'error' })
  }
}

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
 * long values (Email, Hostname, Location) still get the biggest shares. The action column needs
 * 6% for three 24px controls; the browser normalizes the extra percentage when all fields show.
 * The pinned pair is positioned by MEASUREMENT now - see pinOffset.
 */
type Column = { key: string; label: string; short?: string; w: number; align?: 'center' }

const COLUMNS: Column[] = [
  { key: 'nomor', label: 'No', w: 3, align: 'center' },
  { key: 'staff_name', label: 'Staff Name', w: 7 },
  { key: 'email', label: 'Email Address', short: 'Email', w: 9 },
  { key: 'gid', label: 'GID', w: 5 },
  { key: 'japan_hostname', label: 'JAPAN Hostname', short: 'Hostname', w: 8 },
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
const fireworkColumn = ref<string | null>(null)
const fireworkPoint = ref({ left: 0, top: 0 })
let fireworkTimer: ReturnType<typeof setTimeout> | undefined
onBeforeUnmount(() => clearTimeout(fireworkTimer))

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

function toggleColumn(key: string, on: boolean, label: string) {
  if (on) {
    if (!visibleKeys.value.includes(key)) {
      visibleKeys.value = [...visibleKeys.value, key]
      if (!window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        const checkbox = [...document.querySelectorAll<HTMLElement>('.pl-column-choice [role="checkbox"]')]
          .find((el) => el.getAttribute('aria-label') === label)
        const popup = checkbox?.closest<HTMLElement>('.pl-column-popover')
        if (checkbox && popup) {
          const rect = checkbox.getBoundingClientRect()
          const parent = popup.getBoundingClientRect()
          fireworkPoint.value = {
            left: rect.left + rect.width / 2 - parent.left,
            top: rect.top + rect.height / 2 - parent.top
          }
          clearTimeout(fireworkTimer)
          fireworkColumn.value = key
          fireworkTimer = setTimeout(() => { fireworkColumn.value = null }, 700)
        }
      }
    }
  } else {
    if (visibleKeys.value.length <= 1) return // never hide the last one
    visibleKeys.value = visibleKeys.value.filter((k) => k !== key)
    if (fireworkColumn.value === key) fireworkColumn.value = null
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
type Lefts = { head: number[] | null; body: number[] }

function snapshotLefts(): Lefts | null {
  const table = tableEl.value
  if (!table) return null
  const firstRow = table.querySelector('tbody tr[data-id]')
  if (!firstRow) return null
  const headRow = table.querySelector('thead tr')
  return {
    head: headRow ? [...headRow.querySelectorAll('th')].map((th) => th.getBoundingClientRect().left) : null,
    // table-layout:fixed shares column geometry across every body row. Measure it once,
    // still apply the resulting displacement to ALL rows (not just the first one).
    body: [...firstRow.querySelectorAll('td')].map(td => td.getBoundingClientRect().left)
  }
}

watch(visibleKeys, async () => {
  const before = snapshotLefts()
  await nextTick()

  // RE-MEASURE THE PIN OFFSET HERE - BEFORE the "after" snapshot, not when the animation ends.
  //
  // The columns are percentage shares, so ticking a column changes the No column's own width
  // (measured: 64px -> 61px the moment Date was ticked on), and the second pinned column's `left`
  // is stale from that instant. A stale pin HOLDS that column at its old x, so it sat 3px out for
  // the whole 340ms and then snapped left in a single frame - HIRO: "setiap kali filter kolom di
  // tick, kolom staff name bergeser ke kiri". Frame trace of the old code: the No column's right
  // edge moved at frame 7 while the Staff Name cell stayed put until frame 33 (the cleanup), then
  // jumped 297 -> 294.
  //
  // Measuring before the snapshot makes the pinned pair ordinary movers, so they glide with every
  // other column instead of lagging behind and correcting later.
  measurePinOffset()
  // The reactive `:style` binding lands on the NEXT tick, so the new `left` has to be in the DOM
  // before the snapshot is taken or the FLIP would measure the stale position and see no movement.
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
  const rows = [...table.querySelectorAll('tbody tr[data-id]')] as HTMLElement[]
  rows.forEach((tr) => {
    collect([...tr.querySelectorAll('td')] as HTMLElement[], before.body, after.body)
  })

  if (!movers.length) return

  /* SUPPRESS THE HORIZONTAL SCROLLBAR THE FLIP WOULD OTHERWISE SUMMON.
   *
   * A transformed cell still counts toward its scroll container's SCROLLABLE OVERFLOW - the same
   * rule that caused the vertical flicker on a chassis filter, seen this time in the other axis.
   * Measured on an untick: 405 cells carried a transform, scrollWidth ran 1650 -> 1657 -> ... -> 1650,
   * a 12px horizontal scrollbar appeared for nine frames, and clientHeight fell 756 -> 744 -> 756, so
   * the visible area jogged as well - HIRO: "setiap kali filter kolom di untick, ada scrollbar muncul
   * sebentar dibagian bawah tabel, saya tidak mau itu terjadi".
   *
   * `overflow-x: hidden` for the length of the glide means the transient overflow cannot summon one.
   * The container is left ALONE when it is already showing a scrollbar: this page's table is wider than
   * a narrow viewport, so hiding and re-showing that bar would jog the content by the bar's own height -
   * the same class of bug, in the axis nobody asked about. At a normal window there is no horizontal bar
   * to begin with, so the switch is invisible and the final layout is untouched either way.
   */
  const scroller = tableWrapEl.value?.closest('.pl-scroll') as HTMLElement | null
  const alreadyScrolls = scroller ? scroller.scrollWidth > scroller.clientWidth + 1 : false
  if (scroller && !alreadyScrolls) scroller.style.overflowX = 'hidden'

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
      if (scroller && !alreadyScrolls) scroller.style.overflowX = ''
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
  measureRowViewport()
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
  return { left: 'var(--pc-pin-offset, 0px)' }
}

/** Kept in sync with COLUMNS by key. HIRO's layout: Identity, then one Hardware & System block, then
 *  Placement and remarks. The grid is three columns (1 / 2 / 3 by breakpoint), so the order of this
 *  array IS the visual order, read row by row. */
const FORM_SECTIONS = [
  {
    title: 'Identity',
    icon: 'i-lucide-user-round',
    fields: ['staff_name', 'email', 'gid']
  },
  {
    // HIRO: "HARDWARE & SYSTEM / Japan Hostname | Computer S/N | Computer Chasis / Computer
    // Manufacture | Computer Model | Date Inserted / Computer O/S Name | Computer OS Architecture".
    // The O/S pair used to sit in the last section; it describes the machine, so it belongs here.
    title: 'Hardware & System',
    icon: 'i-lucide-hard-drive',
    fields: ['japan_hostname', 'computer_sn', 'chassis', 'manufacturer', 'computer_model', 'tanggal', 'os_name', 'os_arch']
  },
  {
    // HIRO: "PLACEMENT AND REMARKS / Location Remark1 Remark2".
    title: 'Placement and remarks',
    icon: 'i-lucide-wrench',
    fields: ['lokasi', 'remark2', 'remark3']
  }
] as const

/** Placeholders for the two fields whose meaning is not obvious from the label alone. */
const FIELD_PLACEHOLDERS: Record<string, string> = {
  japan_hostname: 'PC Hostname',
  computer_sn: 'PC Serial number'
}

const LABELS: Record<string, string> = {
  nomor: 'No',
  staff_name: 'Staff Name',
  email: 'Email Address',
  gid: 'GID',
  japan_hostname: 'PC Hostname',
  computer_model: 'Computer Model',
  computer_sn: 'Computer S/N',
  tanggal: 'Date Inserted',
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
// Replace snapshots after load or a confirmed Edit response; never mutate rows in place.
// Avoid thousands of deep reactive getter/proxy calls during the initial table render.
const rows = shallowRef<RecordRow[]>([])
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
const openFilter = ref<'chassis' | 'location' | 'columns' | null>(null)
const chassisOpen = computed({
  get: () => openFilter.value === 'chassis',
  set: (open: boolean) => {
    if (open) openFilter.value = 'chassis'
    else if (openFilter.value === 'chassis') openFilter.value = null
  }
})
const locationOpen = computed({
  get: () => openFilter.value === 'location',
  set: (open: boolean) => {
    if (open) openFilter.value = 'location'
    else if (openFilter.value === 'location') openFilter.value = null
  }
})
const columnsOpen = computed({
  get: () => openFilter.value === 'columns',
  set: (open: boolean) => {
    if (open) openFilter.value = 'columns'
    else if (openFilter.value === 'columns') openFilter.value = null
  }
})
let pointerSelectedFilterOption = false
function onFilterOptionPointerDown(event: PointerEvent) {
  pointerSelectedFilterOption = !!(event.target as Element).closest('[role="option"]')
}
function onSelectCloseAutoFocus(event: Event) {
  // A Select closing restores focus to its trigger; that would dismiss Columns
  // immediately after the same click opened its popover. Pointer selection also
  // inherits focus-visible from the menu and paints a green ring on the trigger.
  if (openFilter.value === 'columns' || pointerSelectedFilterOption) event.preventDefault()
  pointerSelectedFilterOption = false
}
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

// Index and sort only when the API snapshot/sort choice changes, not on each keystroke.
const searchIndex = computed(() => new Map(rows.value.map(row => [row.id, haystack(row)])))
const sortedRows = computed(() => {
  const key = sortKey.value
  const dir = sortDir.value === 'asc' ? 1 : -1
  return [...rows.value].sort((a, b) => {
    if (key === 'nomor') return ((Number(cell(a, key)) || 0) - (Number(cell(b, key)) || 0)) * dir
    if (key === 'tanggal') return ((Date.parse(cell(a, key)) || 0) - (Date.parse(cell(b, key)) || 0)) * dir
    return cell(a, key).localeCompare(cell(b, key), undefined, { sensitivity: 'base' }) * dir
  })
})
const visibleRows = computed(() => {
  const q = searching.value.trim().toLowerCase()
  const chassis = filterChassis.value
  const location = filterLocation.value
  if (!q && chassis === ALL && location === ALL) return sortedRows.value
  return sortedRows.value.filter(row =>
    (!q || searchIndex.value.get(row.id)?.includes(q)) &&
    (chassis === ALL || cell(row, 'chassis') === chassis) &&
    (location === ALL || cell(row, 'lokasi') === location)
  )
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
// Keep all records in search/export; mount just the scroll viewport plus overscan.
const viewportTop = ref(0)
const viewportHeight = ref(600)
const ledgerRowHeight = ref(43)
const ledgerHeaderHeight = ref(35)
const WINDOW_OVERSCAN = 6
const printView = ref(false)
const windowed = computed(() => !printView.value && visibleRows.value.length > 80)
const windowSize = computed(() => Math.ceil(viewportHeight.value / ledgerRowHeight.value) + WINDOW_OVERSCAN * 2)
const windowStart = computed(() => !windowed.value ? 0 : Math.min(
  Math.max(0, visibleRows.value.length - windowSize.value),
  Math.max(0, Math.floor((viewportTop.value - ledgerHeaderHeight.value) / ledgerRowHeight.value) - WINDOW_OVERSCAN)
))
const renderedRows = computed(() => windowed.value
  ? visibleRows.value.slice(windowStart.value, windowStart.value + windowSize.value)
  : visibleRows.value)
const spacerTop = computed(() => windowStart.value * ledgerRowHeight.value)
const spacerBottom = computed(() => Math.max(0, visibleRows.value.length - windowStart.value - renderedRows.value.length) * ledgerRowHeight.value)
let scrollingWindow = false
let scrollFrame = 0
function measureRowViewport() {
  const scroller = tableEl.value?.closest('.pl-scroll') as HTMLElement | null
  if (!scroller) return
  viewportHeight.value = scroller.clientHeight
  viewportTop.value = scroller.scrollTop
  const first = tableEl.value?.querySelector('tbody tr[data-id]') as HTMLElement | null
  if (first?.offsetHeight) ledgerRowHeight.value = first.offsetHeight
  const head = tableEl.value?.querySelector('thead') as HTMLElement | null
  if (head?.offsetHeight) ledgerHeaderHeight.value = head.offsetHeight
}
function onLedgerScroll(event: Event) {
  const scroller = event.currentTarget as HTMLElement
  if (viewportTop.value === scroller.scrollTop) return
  scrollingWindow = true
  viewportTop.value = scroller.scrollTop
  viewportHeight.value = scroller.clientHeight
  cancelAnimationFrame(scrollFrame)
  scrollFrame = requestAnimationFrame(() => { scrollingWindow = false })
}
watch(tableEl, () => measureRowViewport(), { flush: 'post' })
onBeforeUnmount(() => cancelAnimationFrame(scrollFrame))
let printMedia: MediaQueryList | undefined
function beforeLedgerPrint() { printView.value = true }
function afterLedgerPrint() { printView.value = false }
function ledgerPrintMediaChange(event: MediaQueryListEvent) { printView.value = event.matches }
onMounted(() => {
  printMedia = window.matchMedia('print')
  printView.value = printMedia.matches
  printMedia.addEventListener('change', ledgerPrintMediaChange)
  window.addEventListener('beforeprint', beforeLedgerPrint)
  window.addEventListener('afterprint', afterLedgerPrint)
})
onBeforeUnmount(() => {
  printMedia?.removeEventListener('change', ledgerPrintMediaChange)
  window.removeEventListener('beforeprint', beforeLedgerPrint)
  window.removeEventListener('afterprint', afterLedgerPrint)
})

const ROW_ANIM_MS = 340
const ROW_ANIM_MAX_MS = 560

// CSS TransitionGroup auto-detection reads computed style while each removed row changes
// table layout. With hundreds of records this serializes full-table reflows. Keep the same
// entrance visually, but use explicit animations and synchronous removal (:css=false).
const rowEntrances = new WeakMap<Element, Animation[]>()
function enterLedgerRow(element: Element, done: () => void) {
  if (!element.hasAttribute('data-id') || scrollingWindow || window.matchMedia('(prefers-reduced-motion: reduce), print').matches) { done(); return }
  const fade = element.animate([{ opacity: 0 }, { opacity: 1 }], {
    duration: 220, easing: 'cubic-bezier(0.22, 1, 0.36, 1)'
  })
  const rise = element.animate([{ transform: 'translateY(-8px)' }, { transform: 'translateY(0)' }], {
    duration: 340, easing: 'cubic-bezier(0.34, 1.56, 0.64, 1)'
  })
  rowEntrances.set(element, [fade, rise])
  rise.onfinish = () => { rowEntrances.delete(element); done() }
}
function cancelLedgerRowEntrance(element: Element) {
  rowEntrances.get(element)?.forEach(animation => animation.cancel())
  rowEntrances.delete(element)
}

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
  scrollingWindow = false
  const scrollerEl = tableEl.value?.closest('.pl-scroll') as HTMLElement | null
  if (scrollerEl) {
    const maxTop = Math.max(0, visibleRows.value.length * ledgerRowHeight.value + ledgerHeaderHeight.value - scrollerEl.clientHeight)
    if (scrollerEl.scrollTop > maxTop) scrollerEl.scrollTop = maxTop
    viewportTop.value = scrollerEl.scrollTop
  }
  const rows = tableEl.value?.querySelectorAll('tbody tr[data-id]')
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
  const all = tableEl.value?.querySelectorAll('tbody tr[data-id]')
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
    // Read ALL layout offsets before writing transforms. Interleaving reads/writes forces
    // a full table layout per surviving row once the register grows to hundreds of rows.
    const offsets = movers.map(el => before[el.dataset.id as string] - el.offsetTop)
    movers.forEach((el, index) => {
      const dy = offsets[index] ?? 0
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
    const movedNow = movers.some((el) => Math.abs(lastDy.get(el) ?? 0) >= 1)
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
const ledgerDateFormatter = new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
function fmtDate(v: unknown): string {
  if (!v) return ''
  const d = new Date(String(v))
  if (Number.isNaN(d.getTime())) return String(v)
  return ledgerDateFormatter.format(d)
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
const originalIdentity = ref<Record<string, unknown>>({})
const form = reactive<Record<string, any>>({})
const formError = ref('')
const identityHints = reactive<Record<string, string>>({ staff_name: '', email: '', gid: '' })
const hostnamePending = ref(false)
let hostnameRequest = 0
let lastAutoHostname = ''
const recordFormSections = computed(() => FORM_SECTIONS.map(section =>
  !editingId.value && section.title === 'Hardware & System'
    ? { ...section, fields: ['chassis', 'computer_sn', 'japan_hostname', 'manufacturer', 'computer_model', 'tanggal', 'os_name', 'os_arch'] as const }
    : section
))

function blankForm() {
  for (const key of Object.keys(identityHints)) identityHints[key] = ''
  for (const k of Object.keys(LABELS)) form[k] = ''
  // Defaults HIRO asked for on a NEW record. The date is today in LOCAL time - the picker parses
  // local, and `new Date('yyyy-mm-dd')` is UTC and can land on the previous day in WIB. Both O/S
  // fields are the standard build and stay editable.
  const now = new Date()
  const p = (n: number) => String(n).padStart(2, '0')
  form.tanggal = `${now.getFullYear()}-${p(now.getMonth() + 1)}-${p(now.getDate())}`
  form.os_name = 'Microsoft Windows 11 Pro'
  form.os_arch = '64-bit'
}

/** Notebook is the canonical NB chassis, matching the imported workbook.
 *  Other legacy/custom values remain visible when editing their existing records. */
const CHASSIS_OPTIONS = ['Desktop', 'Notebook', 'Tablet']
/** New only: commit the highlighted chassis on forward Tab, then let native tab order
 *  move to Computer S/N in the reordered hardware row. Search text is never stored. */
function onNewChassisKeydown(event: KeyboardEvent) {
  if (event.key !== 'Tab' || event.shiftKey || event.isComposing) return
  const input = event.target as HTMLInputElement
  if (input.getAttribute('aria-expanded') !== 'true' || !input.value.trim()) return
  const menuId = input.getAttribute('aria-controls')
  const menu = menuId ? document.getElementById(menuId) : null
  const options = [...(menu?.querySelectorAll<HTMLElement>('[role="option"]') ?? [])]
    .filter(option => option.getAttribute('aria-disabled') !== 'true' && CHASSIS_OPTIONS.includes(option.textContent?.trim() ?? ''))
  const activeId = input.getAttribute('aria-activedescendant')
  const selected = options.find(option => option.id === activeId) ?? options[0]
  // Use the real item's selection path so the combobox also resets its displayed search text.
  selected?.click()
  // Do not preventDefault: native Tab follows the New form's Chassis -> S/N -> Hostname order.
}
const chassisOptions = computed(() => {
  const cur = String(form.chassis ?? '').trim()
  const list = cur && !CHASSIS_OPTIONS.includes(cur) ? [...CHASSIS_OPTIONS, cur] : [...CHASSIS_OPTIONS]
  return list.map((v) => ({ label: v, value: v }))
})

// New records only. Each committed chassis selection re-reads every server page; neither the
// visible table's filters nor its 500-row cap may hide the highest used sequence.
async function assignNewChassisHostname(value: unknown) {
  const chassis = String(value ?? '')
  const request = ++hostnameRequest
  hostnamePending.value = false
  if (!showForm.value || editingId.value) { lastAutoHostname = ''; return }
  if (!CHASSIS_OPTIONS.includes(chassis)) {
    if (lastAutoHostname && form.japan_hostname === lastAutoHostname) form.japan_hostname = ''
    lastAutoHostname = ''
    return
  }
  if (lastAutoHostname && form.japan_hostname === lastAutoHostname) form.japan_hostname = ''
  lastAutoHostname = ''
  const startingHostname = String(form.japan_hostname ?? '')
  const targetEntity = entityId.value
  if (!targetEntity) return
  hostnamePending.value = true
  try {
    const hostname = await loadNextPcHostname(chassis, page => apiListRecords(targetEntity, { page, pageSize: 500 }))
    if (request !== hostnameRequest || !showForm.value || editingId.value || form.chassis !== chassis) return
    // A user may type a manual hostname while the lookup runs; never replace that newer input.
    if (hostname && String(form.japan_hostname ?? '') === startingHostname) {
      form.japan_hostname = hostname
      lastAutoHostname = hostname
    }
  } catch {
    if (request === hostnameRequest && showForm.value && !editingId.value) {
      formError.value = 'Could not check the next hostname. Enter JAPAN Hostname manually, or select the chassis again to retry.'
    }
  } finally {
    if (request === hostnameRequest) hostnamePending.value = false
  }
}
watch([showForm, editingId], () => {
  hostnameRequest++
  hostnamePending.value = false
  lastAutoHostname = ''
}, { flush: 'sync' })
onBeforeUnmount(() => { hostnameRequest++; hostnamePending.value = false })

/** Distinct values ALREADY in the register, most used first - so the field offers what the factory
 *  really has instead of everyone inventing a spelling. Shared by model, manufacturer and location;
 *  before this only the model had it. */
const REGISTER_SUGGEST_FIELDS = ['computer_model', 'manufacturer', 'lokasi']
const SUGGEST_HEADERS: Record<string, string> = {
  computer_model: 'Models already in the register',
  manufacturer: 'Manufacturers already in the register',
  lokasi: 'Locations already used in the register'
}
function registerSuggestPlaceholder(key: string) {
  if (key === 'manufacturer') return 'Type or pick a manufacturer'
  if (key === 'lokasi') return 'Type or pick a location'
  return 'Type or pick a model'
}
function registerSuggestionsFor(key: string) {
  const counts = new Map<string, number>()
  for (const r of rows.value) {
    const v = String(cell(r, key) ?? '').trim()
    if (v) counts.set(v, (counts.get(v) ?? 0) + 1)
  }
  return [...counts.entries()]
    .sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))
    .map(([value, n]) => ({
      value,
      label: value,
      meta: n === 1 ? 'used once' : `used ${n} times`,
      // Shown in the chip under the box after a pick, alongside the GID list's own sentence.
      hint: 'Selected from existing records'
    }))
}

/** ---------------- uniqueness of Japan Hostname and Computer S/N ----------------
 *
 *  HIRO: "textboxt japan hostname dan SN harus unik tidak boleh ada record yang sama, berikan warning
 *  jika ada duplikat dengan record di db".
 *
 *  New records check both fields; Edit checks only changes against its open-time snapshot,
 *  comparing case-insensitively after trim. Local matches are immediate; the fallback reads
 *  all API pages for exact values instead of relying on a limited substring search. A failed
 *  lookup defers to the API, which independently enforces the same device identity policy.
 */
const UNIQUE_FIELDS = ['japan_hostname', 'computer_sn']
const uniqueErrors = ref<Record<string, string>>({})
const uniqueWarned = ref('')

/** How a duplicate is described to the user: the ledger number and who holds it. */
function recordLabel(r: any) {
  const no = String(cell(r, 'nomor') ?? '').trim()
  const who = String(cell(r, 'staff_name') ?? '').trim()
  return [no ? 'No ' + no : '', who].filter(Boolean).join(' - ') || 'record ' + r.id
}

async function checkUnique(key: string) {
  if (!UNIQUE_FIELDS.includes(key)) return
  const value = String(form[key] ?? '').trim()
  if (!shouldValidateDeviceIdentity(value, originalIdentity.value[key], editingId.value !== null)) {
    uniqueErrors.value[key] = ''; refreshUniqueWarning(); return
  }
  try {
    const id = entityId.value
    if (!id) return
    const hit = await findDuplicateDeviceIdentity(key, value, editingId.value, rows.value,
      page => apiListRecords(id, { page, pageSize: 500 }))
    uniqueErrors.value[key] = hit ? `${LABELS[key]} "${value}" is already used by ${recordLabel(hit)}.` : ''
  } catch {
    uniqueErrors.value[key] = ''
  } finally {
    refreshUniqueWarning()
  }
}

/** One warning line for the whole form, so the reason is visible even when the field is scrolled away. */
function refreshUniqueWarning() {
  const first = UNIQUE_FIELDS.map((k) => uniqueErrors.value[k]).filter(Boolean)[0]
  uniqueWarned.value = first || ''
}

// HIRO: "berikan warning duplikat ketika user klik add record saja jangan live check" - so there is no
// debounced query while typing. The only live behaviour left is CLEARING a warning that is already on
// screen once the value changes, because otherwise a stale red message would sit under a field the user
// has already corrected. The authoritative check runs in save().
watch(() => UNIQUE_FIELDS.map((k) => String(form[k] ?? '')).join('\u0000'), () => {
  UNIQUE_FIELDS.forEach((k) => { if (uniqueErrors.value[k]) uniqueErrors.value[k] = '' })
  refreshUniqueWarning()
  // Also drop the summary banner: it is the same fact, and leaving it on screen after the value has
  // been corrected reads as if the form is still refusing to save.
  formError.value = ''
})

/** Japan Hostname and Computer S/N are codes and are stored upper-case. HIRO: "saya mau value di textbox
 *  hostname dan sn full jadi kapital". A watcher rather than a keydown handler so paste and the smart-fill
 *  paths are covered too; `sync` because otherwise the lowercase would be painted for a frame. */
const UPPER_FIELDS = ['japan_hostname', 'computer_sn']
watch(() => UPPER_FIELDS.map((k) => String(form[k] ?? '')), (vals) => {
  UPPER_FIELDS.forEach((k, i) => {
    const up = vals[i].toUpperCase()
    if (vals[i] !== up) form[k] = up
  })
}, { flush: 'sync' })

/** One row of the imported Global ID list, shaped for the field currently being typed in. */
function gidSuggestions(key: 'staff_name' | 'email' | 'gid') {
  const own = key === 'staff_name' ? 'name' : key
  return gidCandidates.value
    .map((r) => ({
      value: String(r[own] ?? '').trim(),
      label: String(r.name ?? '').trim() || String(r.gid ?? '').trim(),
      meta: [r.email, r.gid, r.employee_no].filter(Boolean).map(String).join('   |   '),
      keys: [r.name, r.email, r.gid, r.employee_no].map((v) => String(v ?? '')),
      fill: r,
      hint: 'Auto-filled from the GID List'
    }))
    .filter((s) => s.value)
}

/** The other two fields, so a suggestion that agrees with them can be lifted. */
function siblingValues(key: string) {
  const others = key === 'staff_name' ? ['email', 'gid'] : key === 'email' ? ['staff_name', 'gid'] : ['staff_name', 'email']
  return others.map((k) => String(form[k] ?? ''))
}

/* ---------------- smart fill from the imported GID list ----------------
 * Staff Name, Email Address and GID are three views of the same person, and they used to be typed by
 * hand three times - a typo in any of them silently breaks the link between a machine and its owner.
 * The Global ID list imported on the Data Import page is turned into suggestions here: type any part
 * of a name, an email, a GID or an employee number, pick the person once, and all three fields are
 * filled from the same source. Nothing else on this page reads that list.
 */
const SUGGEST_FIELDS = ['staff_name', 'email', 'gid'] as const
const identityComplete = computed(() => SUGGEST_FIELDS.every(key => String(form[key] ?? '').trim()))

function focusNewJapanHostname() {
  if (editingId.value || !showForm.value || !identityComplete.value) return
  nextTick(() => {
    if (editingId.value || !showForm.value || !identityComplete.value) return
    document.querySelector<HTMLInputElement>('.pl-record-modal[data-state="open"] input[data-pc-input="japan_hostname"]')?.focus()
  })
}

function onNewIdentityKeydown(event: KeyboardEvent) {
  if (event.key !== 'Tab' || event.shiftKey || event.ctrlKey || event.altKey || event.metaKey || event.isComposing || editingId.value || !identityComplete.value) return
  // A Tab pick fills the whole identity before this bubbled event. Prevent its default step
  // so it cannot move focus again after we jump directly to the Hardware & System section.
  event.preventDefault()
  focusNewJapanHostname()
}
const gidCandidates = ref<any[]>([])
const gidCandidatesLoaded = ref(false)

async function ensureGidCandidates() {
  if (gidCandidatesLoaded.value) return
  try {
    const all = await apiListEntities(true)
    const gidEntity = all.find((e: any) => e.slug === 'gid_list')
    if (!gidEntity) return
    const rows: any[] = []
    for (let page = 1; ; page++) {
      const result = await apiListRecords(gidEntity.id, { page, pageSize: 500 })
      rows.push(...(result.items ?? []))
      if (rows.length >= result.total || !(result.items ?? []).length) break
    }
    gidCandidates.value = rows.map((r) => r.values ?? {})
    gidCandidatesLoaded.value = true
  } catch (e) {
    // A missing list must never block the form: without it the fields simply stay hand-typed.
    console.error('[pc-ledger] gid list', e)
  }
}

watch(showForm, (isOpen) => { if (isOpen) ensureGidCandidates() })

/** One pick fills all three - that is the whole point of importing the list. The suggestion carries
 *  the list row in `fill`, so the three fields come from one source instead of three lookups. */
function applyGidSuggestion(s: any) {
  const row = s?.fill
  if (!row) return
  form.staff_name = String(row.name ?? form.staff_name ?? '')
  form.email = String(row.email ?? form.email ?? '')
  form.gid = String(row.gid ?? form.gid ?? '')
  // Mark every field actually retrieved, not just the input where the pick happened.
  for (const key of SUGGEST_FIELDS) {
    const sourceKey = key === 'staff_name' ? 'name' : key
    identityHints[key] = String(row[sourceKey] ?? '').trim() ? 'Auto-filled from the GID List' : ''
  }
  focusNewJapanHostname()
}

function openCreate() {
  editingId.value = null
  originalIdentity.value = {}
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
  originalIdentity.value = Object.fromEntries(UNIQUE_FIELDS.map(key => [key, form[key]]))
  showForm.value = true
}

/* Read-only detail uses the full row snapshot, including columns hidden from the table. */
const showView = ref(false)
const viewTarget = shallowRef<RecordRow | null>(null)
function openView(row: RecordRow) {
  viewTarget.value = row
  showView.value = true
}
function detailValue(row: RecordRow, key: string): string {
  const value = key === 'tanggal' ? fmtDate(row.values?.[key]) : cell(row, key)
  return value.trim() || '—'
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
  if (!entityId.value || (!editingId.value && hostnamePending.value)) return
  formError.value = ''
  saving.value = true
  try {
    // New checks both identities; Edit checks only normalized changes against its open-time snapshot.
    const checkedId = editingId.value
    const checkedIdentity = UNIQUE_FIELDS.map(key => normalizeDeviceIdentity(form[key])).join('\u0000')
    await Promise.all(UNIQUE_FIELDS.map((k) => checkUnique(k)))
    if (!showForm.value || editingId.value !== checkedId) return
    if (checkedIdentity !== UNIQUE_FIELDS.map(key => normalizeDeviceIdentity(form[key])).join('\u0000')) {
      formError.value = 'Device details changed during validation. Please save again.'
      return
    }
    if (uniqueWarned.value) {
      formError.value = `${uniqueWarned.value} Hostname and Serial number must be unique.`
      return
    }
    if (editingId.value) {
      const saved = await apiUpdateRecord(entityId.value, editingId.value, payload())
      rows.value = replaceSavedRecord(rows.value, saved)
      showForm.value = false
    } else {
      await apiCreateRecord(entityId.value, payload())
      showForm.value = false
      await load()
    }
    toast.add({
      title: editingId.value ? 'Record updated' : 'Record created',
      icon: 'i-lucide-circle-check',
      color: 'primary'
    })
  } catch (e: any) {
    formError.value = Object.values(e?.data?.errors ?? {}).flat().join(' ') || e?.data?.message || e?.message || 'Could not save the record.'
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
    toast.add({ title: 'Record deleted', icon: 'i-lucide-trash-2', color: 'primary' })
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
 *   row 9   left EMPTY on purpose - a spacer row. The sheet-level value that used to be written here
 *           is not tracked by this page any more, and the rows below keep their absolute addresses
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
    title.value = 'List PC Ledger CAP Only'
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

    /* Row 9 is deliberately left empty now: the register no longer carries a sheet-level value there,
       so nothing is written to cells (9,3) or (9,4). The rows below are addressed absolutely - the
       notes at rows 3-6, the header at row 10, the data from row 11 - so the row itself stays as a
       spacer rather than being deleted and renumbering everything under it. The entity still stores
       such a field, but no code on this page reads it. */

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
      color: 'primary'
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
            <div class="flex min-w-0 items-center gap-3">
              <span class="pc-ledger-title-icon grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">
                <RecordAddIcon size="md" random-halo>
                  <PcLedgerIcon class="pc-add-tilt size-7" />
                </RecordAddIcon>
              </span>
              <div class="min-w-0">
              <h1 class="text-lg font-bold tracking-wide">Domain Joined PC Ledger</h1>
              <p class="text-xs text-muted">
                List of PC, Notebook, Tablet &middot; CAPACITOR Only
                <span v-if="!loading"> &middot; {{ rows.length }} record{{ rows.length === 1 ? '' : 's' }}</span>
              </p>
              </div>
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
                v-model:open="chassisOpen"
                :content="{ onCloseAutoFocus: onSelectCloseAutoFocus, onPointerdown: onFilterOptionPointerDown }"
                :items="[{ label: 'All chassis', value: ALL }, ...chassisTypes.map(d => ({ label: d, value: d }))]"
                class="pl-filter-trigger w-40"
                :ui="{ base: 'h-9 pointer-events-auto', item: 'pl-filter-option-row' }"
              />

              <USelect
                v-model="filterLocation"
                v-model:open="locationOpen"
                :content="{ onCloseAutoFocus: onSelectCloseAutoFocus, onPointerdown: onFilterOptionPointerDown }"
                :items="[{ label: 'All locations', value: ALL }, ...locations.map(d => ({ label: d, value: d }))]"
                class="pl-filter-trigger w-64"
                :ui="{ base: 'h-9 pointer-events-auto', item: 'pl-filter-option-row' }"
              />

              <!-- Column chooser. A popover of checkboxes rather than a dropdown menu: it is a
                   persistent list with a live count, not a list of one-shot actions. -->
              <UPopover v-model:open="columnsOpen">
                <UButton
                  color="soft"
                  :label="`Columns (${visibleColumns.length}/${COLUMNS.length})`"
                  class="pl-filter-trigger toolbar-line-hover pointer-events-auto"
                >
                  <template #leading><ToolbarStrokeIcon name="columns" /></template>
                </UButton>
                <template #content>
                  <div class="pl-column-popover w-64 p-3">
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
                      <div v-for="c in COLUMNS" :key="c.key" class="pl-column-choice">
                        <UCheckbox
                          :model-value="visibleKeys.includes(c.key)"
                          :label="c.label"
                          :disabled="visibleKeys.length <= 1 && visibleKeys.includes(c.key)"
                          variant="list"
                          @update:model-value="(v: any) => toggleColumn(c.key, !!v, c.label)"
                        />
                      </div>
                    </div>
                    <p class="mt-2 text-[11px] leading-snug text-muted">
                      Remembered for this browser. Excel keeps exporting all 15 columns.
                    </p>
                    <!-- Paint outside the scrollable list, but stay anchored to this popover. -->
                    <span v-if="fireworkColumn" class="pl-column-firework" :style="{ left: `${fireworkPoint.left}px`, top: `${fireworkPoint.top}px` }" aria-hidden="true">
                      <i v-for="ray in 8" :key="ray" :style="{ '--ray': ray - 1 }" />
                    </span>
                  </div>
                </template>
              </UPopover>

              <UButton
                color="soft"
                label="Excel"
                :loading="exporting"
                @click="exportExcel"
                class="toolbar-line-hover toolbar-line-hover--excel"
              >
                <template #leading><ToolbarStrokeIcon :name="exporting ? 'loading' : 'excel'" /></template>
              </UButton>
              <UButton
                color="primary"
                label="Add Record" class="record-add-hover stable-hover-hitbox"
                @click="openCreate"
              >
                <template #leading><ChannelAddIcon /></template>
              </UButton>
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
        <!-- The entrance animation must start when the DATA is in, not when the card mounts.
             With `anim-fade-up` present from the first render the 340ms fade ran while the card was
             still showing its loader, so the rows appeared after the motion had finished - measured:
             the card sat at opacity 0.875 at the exact frame the 26 rows arrived. HIRO: "halaman dan
             table muncul secara tiba-tiba. saya mau ada animasi untuk memberikan efek visual smooth
             seperti halaman logbook". Gating the class on !loading makes the POPULATED table fade
             up, and the sheet header above still animates at mount - the same two-step rhythm the
             logbook has. -->
        <div :class="{ 'anim-fade-up': !loading }" class="overflow-hidden rounded-xl border border-default bg-elevated">
          <div v-if="loading" class="flex items-center justify-center p-12 text-sm text-muted">
            <RegisterLoading />
          </div>

          <div v-else-if="!rows.length" class="p-12 text-center">
            <UIcon name="i-lucide-hard-drive" class="mx-auto size-8 text-dimmed" />
            <p class="mt-3 text-sm font-medium">No PCs recorded yet</p>
            <p class="mt-1 text-xs text-muted">
              Start with Add Record, or copy the rows across from the existing spreadsheet.
            </p>
          </div>

          <div v-else class="pl-scroll" @scroll.passive="onLedgerScroll">
            <!-- The frozen scroll extent lives on THIS wrapper, never on the table itself: a
                 min-height on a <table> is shared out among its rows, which stretched every row for
                 the length of the animation. See tableWrapEl in the script. -->
            <div ref="tableWrapEl">
            <table ref="tableEl" class="pl-table" :style="{ '--pc-pin-offset': `${pinOffset}px` }" :aria-rowcount="visibleRows.length + 1">
              <colgroup>
                <col
                  v-for="c in COLUMNS"
                  :key="c.key"
                  :style="{ width: isVisible(c.key) ? c.w + '%' : '0%' }"
                />
                <col style="width: 6%" />
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
                :css="false"
                @enter="enterLedgerRow"
                @enter-cancelled="cancelLedgerRowEntrance"
              >
                <tr v-if="spacerTop" key="window-top" class="pl-window-spacer" aria-hidden="true"><td :colspan="COLUMNS.length + 1" :style="{ height: `${spacerTop}px` }" /></tr>
                <tr v-for="(row, rowIndex) in renderedRows" :key="row.id" v-memo="[row, visibleKeys, windowStart + rowIndex]" :data-id="row.id" :aria-rowindex="windowStart + rowIndex + 2" :class="{ 'pl-row-even': (windowStart + rowIndex + 1) % 2 === 0 }">
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
                  >
                      <button
                        type="button"
                        class="pl-ellipsis pc-copy-cell w-full cursor-copy focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-2"
                        :aria-label="`Copy ${c.label}: ${c.key === 'tanggal' ? fmtDate(row.values?.[c.key]) : cell(row, c.key)}`"
                        @click.stop="copyCellText(c.key === 'tanggal' ? fmtDate(row.values?.[c.key]) : cell(row, c.key), $event)"
                      >{{ c.key === 'tanggal' ? fmtDate(row.values?.[c.key]) : cell(row, c.key) }}</button>
                  </td>
                  <!-- Actions sit directly in the cell with no flex wrapper or gap.
                       `pl-actions` brings CCTV's narrower
                       `px-1 py-2` padding with it, which the shared `.pl-table td` rule cannot
                       express as a utility (it would lose on specificity). -->
                  <td class="pl-actions text-center">
                    <!-- Native equivalents of the measured xs ghost UButtons (24x24).
                         Keep theme tokens, accessible labels, icon animation hooks and handlers;
                         avoid mounting three reactive vendor component trees for EVERY record. -->
                    <button type="button" data-slot="base" class="pl-view-action inline-flex items-center rounded-md p-1 text-primary outline-primary/30 focus-visible:outline-3" :aria-label="`View details for PC ${cell(row, 'nomor') || row.id}`" title="View details" @click="openView(row)">
                      <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                        <path class="pl-view-eye-outline" d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z" />
                        <circle class="pl-view-eye-iris" cx="12" cy="12" r="2.5" />
                      </svg>
                    </button>
                    <button type="button" data-slot="base" class="inline-flex items-center disabled:cursor-not-allowed aria-disabled:cursor-not-allowed disabled:opacity-75 aria-disabled:opacity-75 transition-colors font-medium rounded-md cursor-pointer text-xs gap-1 text-default hover:bg-elevated active:bg-elevated outline-inverted/25 focus-visible:outline-3 hover:disabled:bg-transparent dark:hover:disabled:bg-transparent hover:aria-disabled:bg-transparent dark:hover:aria-disabled:bg-transparent p-1 pencil-hover" aria-label="Edit" @click="openEdit(row)">
                      <PencilIcon class="size-4" data-slot="leadingIcon" />
                    </button>
                    <button type="button" data-slot="base" class="inline-flex items-center disabled:cursor-not-allowed aria-disabled:cursor-not-allowed disabled:opacity-75 aria-disabled:opacity-75 transition-colors font-medium rounded-md cursor-pointer text-xs gap-1 text-error hover:bg-error/10 active:bg-error/10 outline-error/25 focus-visible:outline-3 disabled:bg-transparent aria-disabled:bg-transparent dark:disabled:bg-transparent dark:aria-disabled:bg-transparent p-1 trash-bin-hover" aria-label="Delete" @click="askDelete(row)">
                      <TrashBinIcon class="size-4" data-slot="leadingIcon" />
                    </button>
                  </td>
                </tr>
                <tr v-if="spacerBottom" key="window-bottom" class="pl-window-spacer" aria-hidden="true"><td :colspan="COLUMNS.length + 1" :style="{ height: `${spacerBottom}px` }" /></tr>
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

      <!-- ---------------- read-only detail ---------------- -->
      <UModal v-model:open="showView" :title="viewTarget ? `PC Ledger record ${detailValue(viewTarget, 'nomor')}` : 'PC Ledger record details'" description="Read-only information for this PC ledger record." :ui="{ content: 'w-[calc(100vw-2rem)] max-w-5xl pl-view-modal' }">
        <template #content>
          <div v-if="viewTarget" class="flex max-h-[min(82vh,720px)] flex-col overflow-hidden rounded-xl">
            <div class="relative shrink-0 overflow-hidden border-b border-default px-5 pb-5 pt-6 sm:px-7">
              <div aria-hidden="true" class="pointer-events-none absolute -right-16 -top-24 size-52 rounded-full bg-primary/15 blur-3xl" />
              <div class="relative flex items-start gap-4">
                <span class="grid size-11 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/25">
                  <RecordAddIcon size="md" random-halo>
                    <svg class="pl-view-header-eye pc-add-tilt size-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                      <g class="pl-view-header-lids"><path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z"/><circle class="pl-view-header-pupil" cx="12" cy="12" r="2.5"/></g>
                    </svg>
                  </RecordAddIcon>
                </span>
                <div class="min-w-0 flex-1">
                  <p class="text-[11px] font-semibold uppercase tracking-[0.16em] text-primary">PC Ledger / Record {{ detailValue(viewTarget, 'nomor') }}</p>
                  <h2 class="mt-1 break-words text-lg font-semibold leading-tight text-highlighted sm:text-xl">{{ detailValue(viewTarget, 'japan_hostname') }}</h2>
                  <p class="mt-1 text-sm text-muted">{{ detailValue(viewTarget, 'staff_name') }} <span aria-hidden="true">·</span> {{ detailValue(viewTarget, 'chassis') }}</p>
                </div>
                <button type="button" class="grid size-8 shrink-0 place-items-center rounded-lg text-muted transition-colors hover:bg-error/10 hover:text-error focus-visible:outline-2 focus-visible:outline-error" aria-label="Close details" @click="showView = false">
                  <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M18 6 6 18M6 6l12 12"/></svg>
                </button>
              </div>
            </div>
            <div class="min-h-0 flex-1 overflow-y-auto px-5 py-5 sm:px-7">
              <section v-for="section in FORM_SECTIONS" :key="section.title" class="mb-6 last:mb-0">
                <h3 class="mb-3 flex items-center gap-2 text-[11px] font-semibold uppercase tracking-[0.12em] text-muted">
                  <span class="pl-view-section-dot relative size-1.5 shrink-0 rounded-full bg-primary" aria-hidden="true" />{{ section.title }}
                </h3>
                <dl class="grid grid-cols-1 gap-x-4 gap-y-2 sm:grid-cols-2 lg:grid-cols-3">
                  <div v-for="key in section.fields" :key="key" class="min-w-0 rounded-lg border border-default bg-elevated/40 px-3.5 py-3">
                    <dt class="text-[11px] font-medium text-muted">{{ LABELS[key] }}</dt>
                    <dd class="mt-1 break-words text-sm font-medium leading-relaxed text-highlighted" :class="{ 'text-muted': detailValue(viewTarget, key) === '—' }">{{ detailValue(viewTarget, key) }}</dd>
                  </div>
                </dl>
              </section>
            </div>
            <div class="flex shrink-0 items-center justify-between gap-3 border-t border-default bg-elevated/40 px-5 py-3 sm:px-7">
              <span class="text-xs text-muted">Read-only record details</span>
              <UButton color="neutral" variant="soft" label="Close" class="pl-view-footer-close" @click="showView = false">
                <template #leading>
                  <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="m9 9 6 6m0-6-6 6"/></svg>
                </template>
              </UButton>
            </div>
          </div>
        </template>
      </UModal>

      <!-- ---------------- add / edit ---------------- -->
      <UModal v-model:open="showForm" :ui="{ content: 'w-full max-w-4xl pl-record-modal' }">
        <template #content>
          <!-- `relative z-10` puts the whole dialog above the accent glow that the .pl-record-modal
               rule paints as a ::after on the content element (see the non-scoped style block at the
               foot of this file). One class on this wrapper is enough - it covers the header, body
               and footer at once - where the CCTV and Users dialogs set it on their header and body
               separately because those two use the vendor's header/body slots. -->
          <div class="relative z-10 flex max-h-[85vh] flex-col" :class="{ 'pc-new-record-content': !editingId }">
            <div class="flex items-center gap-3 border-b border-default px-5 py-4">
              <span class="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary ring-1 ring-inset ring-primary/20">
                <RecordAddIcon :name="editingId ? 'i-lucide-pencil' : 'channel-add'" />
              </span>
              <div class="min-w-0 flex-1">
                <h2 class="text-sm font-semibold leading-tight">{{ editingId ? 'Edit PC record' : 'New PC record' }}</h2>
                <p class="text-xs text-muted">
                  {{ editingId ? 'Changes are written to the ledger immediately.' : 'Add new PC to the ledger system.' }}
                </p>
              </div>
              <button type="button" class="grid size-8 shrink-0 place-items-center rounded-lg text-muted transition-colors hover:bg-error/10 hover:text-error focus-visible:outline-2 focus-visible:outline-error disabled:cursor-not-allowed disabled:opacity-50" aria-label="Close record form" :disabled="saving" @click="showForm = false">
                <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M18 6 6 18M6 6l12 12"/></svg>
              </button>
            </div>

            <div class="flex-1 overflow-y-auto px-5 py-4">
              <div v-for="section in recordFormSections" :key="section.title" class="mb-5 last:mb-0">
                <p class="mb-3 flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wider text-muted">
                  <UIcon :name="section.icon" class="size-3.5" />
                  {{ section.title }}
                </p>
                <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  <UFormField
                    v-for="key in section.fields"
                    :key="key"
                    :label="LABELS[key]"
                    :error="uniqueErrors[key] || undefined"
                    :ui="{ label: 'text-xs font-medium', error: 'hidden' }"
                  >
                    <DatePicker
                      v-if="key === 'tanggal'"
                      v-model="form[key]"
                      placeholder="Pick a date"
                    />
                    <UInputMenu
                      v-else-if="key === 'chassis' && !editingId"
                      v-model="form[key]"
                      :items="CHASSIS_OPTIONS"
                      placeholder="Pick a chassis"
                      class="pc-new-chassis w-full"
                      :ui="{ base: 'h-9' }"
                      @update:model-value="assignNewChassisHostname"
                      @keydown.capture="onNewChassisKeydown"
                    />
                    <USelect
                      v-else-if="key === 'chassis'"
                      v-model="form[key]"
                      :items="chassisOptions"
                      placeholder="Pick a chassis"
                      class="w-full"
                      :ui="{ base: 'h-9' }"
                    />
                    <SuggestInput
                      v-else-if="REGISTER_SUGGEST_FIELDS.includes(key)"
                      v-model="form[key]"
                      :suggestions="registerSuggestionsFor(key)"
                      :placeholder="registerSuggestPlaceholder(key)"
                      :header="SUGGEST_HEADERS[key]"
                    />
                    <SuggestInput
                      v-else-if="(SUGGEST_FIELDS as readonly string[]).includes(key)"
                      v-model="form[key]"
                      :suggestions="gidSuggestions(key as 'staff_name' | 'email' | 'gid')"
                      :context="siblingValues(key)"
                      :select-on-tab="!editingId"
                      :data-pc-identity="key"
                      v-model:source-hint="identityHints[key]"
                      :placeholder="key === 'staff_name' ? 'Search or enter a staff name' : key === 'email' ? 'Search or enter an email address' : 'Search or enter a Global ID'"
                      header="Smart Suggestion - Click or Enter ↵"
                      @fill="applyGidSuggestion"
                      @keydown="onNewIdentityKeydown"
                    />
                    <UInput
                      v-else
                      v-model="form[key]"
                      :placeholder="FIELD_PLACEHOLDERS[key]"
                      :data-pc-input="key"
                      :loading="key === 'japan_hostname' && !editingId && hostnamePending"
                      class="w-full"
                      :ui="{ base: 'h-9' }"
                    />
                  </UFormField>
                </div>
              </div>

              <!-- The banner's row is RESERVED (min-h) so showing it does not resize the dialog. HIRO:
                   "tolong atur animasi pelebaran modal ketika notifikasi duplikat yang dibawah muncul
                   karena saat ini modal melebar secara tiba-tiba jelek secara visual".
                   The per-field message is hidden (`error: 'hidden'` on the UFormField) and carried here
                   instead: the message would wrap to three lines inside a one-third-width column and the
                   dialog grew 86px every time it appeared. The field keeps its red ring, so which box is
                   wrong is still obvious, and the dialog now has the same height before and after. -->
              <div class="mt-4 min-h-[64px]">
                <UAlert
                  v-if="formError"
                  color="error"
                  variant="soft"
                  icon="i-lucide-circle-alert"
                  :title="formError"
                  class="anim-fade-in"
                />
              </div>
            </div>

            <div class="flex items-center justify-end gap-2 border-t border-default px-5 py-3">
              <UButton color="red" variant="ghost" icon="basil:cancel-outline" label="Cancel" class="cancel-hover" :disabled="saving" @click="showForm = false" />
              <UButton color="primary" :class="[{ 'record-save-hover': editingId }, !editingId ? 'channel-add-hover' : '']" :label="editingId ? 'Save changes' : 'Add record'" :loading="saving" :disabled="!editingId && hostnamePending" @click="save">
                <template #leading><SaveIcon v-if="editingId" /><ChannelAddIcon v-else /></template>
              </UButton>
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
                <dt class="text-muted">PC Hostname</dt>
                <dd class="truncate tabular-nums">{{ cell(deleteTarget, 'japan_hostname') || '-' }}</dd>
                <dt class="text-muted">Location</dt>
                <dd class="truncate">{{ cell(deleteTarget, 'lokasi') || '-' }}</dd>
              </dl>
            </div>

            <div class="flex items-center justify-end gap-2 bg-elevated/40 px-5 py-4">
              <UButton color="neutral" variant="ghost" label="Cancel" :disabled="deleting" @click="showDelete = false" />
              <UButton color="error" class="record-delete-hover trash-bin-hover" label="Delete record" :loading="deleting" @click="confirmDelete">
                <template #leading>
                  <TrashBinIcon class="size-5" data-slot="leadingIcon" />
                </template>
              </UButton>
            </div>
          </div>
        </template>
      </UModal>
    <!-- Pointer-anchored confirmation: fixed, so it floats above the table without moving a row. -->
    <div class="copy-pill-layer" aria-hidden="true">
      <span
        v-for="p in copyPills"
        :key="p.id"
        class="copy-pill"
        :style="{ left: `${p.x}px`, top: `${p.y}px` }"
      >
        <UIcon name="i-lucide-check" class="size-3" />
        <span>Copied!</span>
      </span>
    </div>
    <span class="sr-only" role="status" aria-live="polite">{{ copyAnnounce }}</span>
    </template>
  </UDashboardPanel>
</template>

<style scoped>
/* USelect's trigger lacks this page's scoped attribute; opt in only these filters. */
:global(.pl-filter-trigger:is(:hover, :focus-visible)) {
  background-color: color-mix(in oklab, var(--ui-primary) 5%, var(--ui-bg));
}
:global(.dark .pl-filter-trigger:is(:hover, :focus-visible)) {
  background-color: color-mix(in oklab, var(--ui-primary) 10%, var(--ui-bg));
}

/* Keep the selected option (including All) tinted when the pointer highlight moves. */
:global(.pl-filter-option-row:is([data-highlighted], [data-state=checked]):not([data-disabled])::before) {
  background: color-mix(in oklab, var(--ui-primary) 5%, transparent);
}
:global(.dark .pl-filter-option-row:is([data-highlighted], [data-state=checked]):not([data-disabled])::before) {
  /* The dark popup sits on a deeper surface than the table; keep the tint visible. */
  background: color-mix(in oklab, var(--ui-primary) 10%, transparent);
}

.pl-column-popover { position: relative; }
.pl-column-firework { position: absolute; z-index: 1; width: 0; height: 0; pointer-events: none; }
.pl-column-firework::before {
  content: ''; position: absolute; width: 18px; height: 18px; border: 2px solid var(--ui-primary);
  border-radius: 50%; animation: pl-firework-ring 650ms ease-out both;
}
.pl-column-firework i {
  position: absolute; width: 4px; height: 4px; margin: -2px;
  border-radius: 50%; background: var(--ui-primary); box-shadow: 0 0 4px var(--ui-primary);
  animation: pl-firework-ray 680ms cubic-bezier(.2,.7,.25,1) both;
}
@keyframes pl-firework-ring {
  0% { opacity: .85; transform: translate(-50%, -50%) scale(.55); }
  48% { opacity: .7; transform: translate(-50%, -50%) scale(1.4); }
  100% { opacity: 0; transform: translate(-50%, -50%) scale(2.3); }
}
@keyframes pl-firework-ray {
  0% { opacity: 1; transform: rotate(calc(var(--ray) * 45deg)) translateY(-3px) scale(.85); }
  50% { opacity: 1; transform: rotate(calc(var(--ray) * 45deg)) translateY(-16px); }
  100% { opacity: 0; transform: rotate(calc(var(--ray) * 45deg)) translateY(-24px) scale(.6); }
}
@media (prefers-reduced-motion: reduce), print {
  .pl-column-firework { display: none; }
}

.pc-copy-cell { text-align: inherit; font: inherit; }
.pl-table .pl-window-spacer td { padding: 0; border: 0; background: transparent; line-height: 0; font-size: 0; }
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
.pl-table tbody tr.pl-row-even td {
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

/* CCTV's Actions cell padding (measured 8px 4px, `px-1 py-2`) leaves enough room for the
   three 24px icon buttons at the action column's 6% width. It cannot be expressed as a utility here:
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


/* ---- "Copied!" pill: pointer-anchored confirmation for the click-to-copy cells ----
   `translate` does the centring and `transform` runs the animation, so the two never fight: Tailwind
   v4 emits centring as the standalone translate property, and animating transform composes with it
   instead of overwriting it. The pill is fixed to the viewport at the click coordinates, so it
   cannot shift a table row or be clipped by the table's scroll container. */
.copy-pill-layer { position: fixed; inset: 0; z-index: 60; pointer-events: none; }
.copy-pill {
  position: fixed;
  translate: -50% -50%;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 3px 9px 3px 7px;
  border-radius: 9999px;
  border: 1px solid color-mix(in oklab, var(--ui-primary) 45%, transparent);
  background: color-mix(in oklab, var(--ui-primary) 14%, var(--ui-bg));
  color: var(--ui-primary);
  font-size: 11px;
  font-weight: 600;
  line-height: 1.4;
  white-space: nowrap;
  box-shadow: 0 8px 20px -8px color-mix(in oklab, var(--ui-primary) 70%, transparent), 0 2px 6px rgba(0, 0, 0, 0.25);
  animation: copy-pill-life 1.15s cubic-bezier(0.22, 1, 0.36, 1) forwards;
}
.copy-pill::after {
  content: '';
  position: absolute;
  inset: -1px;
  border-radius: inherit;
  border: 1px solid var(--ui-primary);
  animation: copy-pill-ring 700ms ease-out forwards;
}
@keyframes copy-pill-life {
  0% { opacity: 0; transform: translateY(6px) scale(0.78); }
  16% { opacity: 1; transform: translateY(0) scale(1.06); }
  30% { opacity: 1; transform: translateY(0) scale(1); }
  68% { opacity: 1; transform: translateY(-8px) scale(1); }
  100% { opacity: 0; transform: translateY(-18px) scale(0.94); }
}
@keyframes copy-pill-ring {
  from { opacity: 0.55; transform: scale(1); }
  to { opacity: 0; transform: scale(1.55); }
}
@media (prefers-reduced-motion: reduce) {
  .copy-pill { animation: none; }
  .copy-pill::after { animation: none; opacity: 0; }
}

</style>

<!-- NON-SCOPED, and it has to be. UModal teleports its content to <body>, so a scoped rule - which
     compiles to .pl-record-modal[data-v-xxx] - can never match the dialog element, because it carries
     no scope id from this component. Same reason the CCTV and Users dialogs keep this CSS in a plain
     <style> block. -->
<style>
/* The page-title glyph is 28px; match only its wrapper to that size so the grid
   centres the SVG instead of offsetting it 2px right and down inside a 24px track. */
.pc-ledger-title-icon .pc-add-mark { width: 1.75rem; height: 1.75rem; }

/* Only the PC Ledger view action animates; its hitbox stays fixed at the edges. */
.pl-view-action { cursor: pointer; transition: background-color 180ms ease, box-shadow 180ms ease, color 180ms ease; }
.pl-view-action:is(:hover, :focus-visible) {
  background: color-mix(in oklab, var(--ui-primary) 13%, transparent);
  box-shadow: 0 0 0 1px color-mix(in oklab, var(--ui-primary) 28%, transparent), 0 0 14px color-mix(in oklab, var(--ui-primary) 15%, transparent);
}
.pl-view-eye-iris { transform-origin: 12px 12px; transition: transform 240ms cubic-bezier(.2,.8,.2,1); }
.pl-view-action:is(:hover, :focus-visible) .pl-view-eye-iris { transform: scale(1.28); }
.pl-view-action:is(:hover, :focus-visible) .pl-view-eye-outline { animation: pl-eye-trace 420ms ease-out both; }
@keyframes pl-eye-trace { from { stroke-dasharray: 60; stroke-dashoffset: 60; } to { stroke-dasharray: 60; stroke-dashoffset: 0; } }
.pl-view-modal[data-state='open'] { animation: pl-view-arrive 290ms cubic-bezier(.2,.8,.2,1) both; }
@keyframes pl-view-arrive { from { opacity: 0; transform: translateY(12px) scale(.985); } to { opacity: 1; transform: none; } }
/* One cycle: glance right, return, glance left, return, then close and open the lids.
   Only the glyph moves; the icon tile and modal header never change size. */
.pl-view-modal[data-state='open'] .pl-view-header-lids {
  transform-box: fill-box;
  transform-origin: center;
  animation: pl-header-blink 3.6s ease-in-out infinite;
}
.pl-view-modal[data-state='open'] .pl-view-header-pupil {
  animation: pl-header-glance 3.6s ease-in-out infinite;
}
@keyframes pl-header-glance {
  0%, 12%, 43%, 76%, 100% { transform: translateX(0); }
  20%, 34% { transform: translateX(2px); }
  52%, 67% { transform: translateX(-2px); }
}
@keyframes pl-header-blink {
  0%, 78%, 91%, 100% { transform: scaleY(1); }
  84% { transform: scaleY(.08); }
}
/* The solid 6px section marker stays put; only its out-of-flow ring expands. */
.pl-view-section-dot::after {
  content: '';
  position: absolute;
  inset: -2px;
  border: 1px solid var(--ui-primary);
  border-radius: 9999px;
  pointer-events: none;
  animation: pl-view-section-ping 2.4s ease-out infinite;
}
@keyframes pl-view-section-ping {
  0% { transform: scale(.6); opacity: .65; }
  50%, 100% { transform: scale(2.1); opacity: 0; }
}
.pl-view-modal .pl-view-footer-close:is(:hover, :focus-visible) {
  color: var(--ui-error);
  background-color: color-mix(in oklab, var(--ui-error) 13%, transparent);
}
@media (prefers-reduced-motion: reduce) {
  .pl-view-action, .pl-view-eye-iris { transition: none; }
  .pl-view-action:is(:hover, :focus-visible) .pl-view-eye-outline,
  .pl-view-modal[data-state='open'],
  .pl-view-modal[data-state='open'] .pl-view-header-eye :is(.pl-view-header-lids, .pl-view-header-pupil) { animation: none; }
  .pl-view-section-dot::after { animation: none; opacity: 0; }
}
/* A teleported suggestion becomes the modal's last child; suppress only the NEW form's
   accidental divide-y border so opening a dropdown cannot grow/move the dialog by 1px. */
.pl-record-modal[data-slot='content'] > .pc-new-record-content { border-bottom-width: 0; }

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
