<script setup lang="ts">
import { apiListRecords, apiCreateRecord, apiUpdateRecord, apiDeleteRecord, apiGetEntity, apiListEntities } from '~/composables/useApi'
import { exportLogbookToExcel } from '~/composables/useExcelExport'

/**
 * CCTV Access Log Book — dedicated page that mirrors the paper form
 * "CCTV Access Request Log" (renamed from "RECORDABLE MEDIA LOG BOOK" on 2026-10-02,
 * and set to title case the same day - the all-caps version read as shouting).
 * The "request" wording is deliberate: the sheet carries TWO signatures - the requester's and
 * ISD's - so it records an approved request to access footage, not a passive access log.
 *
 * Route is /logbook/cctvacc (renamed from /logbook/cctv on 2026-10-02 when the Log Book
 * menu group was introduced alongside Handover).
 *
 * Data lives in the generic metadata-driven engine (entity slug cctv_log_book), so
 * storage, validation and audit are the same code path as every other entity. Only the
 * presentation differs: the paper form is a 12-column landscape sheet, which does not
 * fit the generic table, so this page renders the columns in the printed order and
 * adds a print stylesheet that reproduces the sheet on A4 landscape.
 *
 * No route middleware: the other pages of this app have none either. Authorisation is
 * enforced by the API (JWT on every /api route), so a page that skips the middleware is
 * no less protected than data/[slug] or users.
 */
const CCTV_SLUG = 'cctv_log_book'

interface FieldMetaLite { id: number; name: string; label: string; type: string; sortOrder: number }
interface Row { id: number; values: Record<string, any>; createdBy: string; createdAt: string }

const entity = ref<{ id: number; name: string } | null>(null)
const fields = ref<FieldMetaLite[]>([])
const rows = ref<Row[]>([])
const total = ref(0)
const loading = ref(true)
const saving = ref(false)
const toast = ref<{ msg: string; kind: 'success' | 'error' } | null>(null)
const search = ref('')
const showForm = ref(false)
const editing = ref<Row | null>(null)

/** Column order and width of the printed sheet. */
const COLUMNS = [
  { key: 'tanggal', label: 'Date', w: 'w-[8%]' },
  { key: 'departemen', label: 'Section', w: 'w-[9%]' },
  { key: 'no_pegawai', label: 'Employee No', w: 'w-[9%]' },
  { key: 'nama_pemohon', label: 'PIC Name', w: 'w-[13%]' },
  { key: 'tujuan', label: 'Purpose / Details', w: 'w-[22%]' },
  { key: 'tanda_pemohon', label: 'PIC Sign', w: 'w-[8%]', sign: true },
  { key: 'pic_mulai', label: 'Start Time', w: 'w-[9%]' },
  { key: 'pic_selesai', label: 'End Time', w: 'w-[9%]' },
  // "PIC by ISD" moved here on 2026-10-02 at HIRO's request so it sits directly before
  // the ISD signature it belongs to.
  { key: 'pic_isd', label: 'PIC by ISD', w: 'w-[8%]' },
  { key: 'tanda_isd', label: 'ISD Sign', w: 'w-[8%]', sign: true }
] as const

/**
 * "Time Request" was dropped from this table AND from the entry form on 2026-10-02 at
 * HIRO's request. The DynamicField row for `waktu_diminta` still exists in the database
 * on purpose: removing it would drop cctv_log_book from 12 to 11 fields and break
 * test-logbook.ps1's "12 field terpasang" assertion. Keeping the field means the data
 * model is untouched and the column can come back later, while the UI stays consistent
 * (nothing is collected that is never displayed).
 */
const HIDDEN_FIELDS = ['waktu_diminta', 'nomor'] as const

const SIGN_FIELDS = ['tanda_pemohon', 'tanda_isd'] as const

/** The signed-in user, used to prefill "PIC by ISD" so nobody has to type their own name. */
const { user: me } = useAuth()

/** yyyy-mm-dd for today, in LOCAL time. */
function todayIso(): string {
  const d = new Date()
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

/** dd/MM/yy for today - the paper form's date shape. */
function todayDisplay(): string {
  const d = new Date()
  const p = (n: number) => String(n).padStart(2, '0')
  return `${p(d.getDate())}/${p(d.getMonth() + 1)}/${String(d.getFullYear()).slice(-2)}`
}

/** The display name of whoever is signed in, falling back to the username. */
function currentUserName(): string {
  const u = me.value as any
  return String(u?.fullName || u?.username || '')
}

/**
 * Build the stored Start/End value: the date is ALWAYS today, so only the time comes from
 * the picker. Stored as "dd/MM/yy HH:mm", matching the paper sheet. A blank picker yields
 * '' rather than a bare date, so an untouched field stays genuinely empty.
 */
function composeDateTime(time: unknown): string {
  const t = String(time ?? '').trim()
  if (!t) return ''
  return `${todayDisplay()} ${t}`
}

/**
 * MANDATORY FIELDS (HIRO, 2026-10-04): every field on the sheet is required - only the
 * hidden NO is optional, because the server generates it. The order here is the order the
 * warning message lists them in, i.e. the order they appear on the form.
 *
 * `required` on <UFormField> is used purely for the red asterisk - it renders
 * `after:content-['*']` on the label. It deliberately does NOT drive validation: UForm's
 * built-in submit validation is switched off below (`:validate-on="[]"`) so that this page
 * owns the whole flow and can show one English warning naming every missing field at once,
 * with the empty inputs outlined in red. Letting UForm validate would short-circuit the
 * submit handler and show its own per-field errors instead.
 */
const REQUIRED = [
  { key: 'tanggal', label: 'Date' },
  { key: 'departemen', label: 'Section' },
  { key: 'no_pegawai', label: 'Employee No' },
  { key: 'nama_pemohon', label: 'PIC Name' },
  { key: 'tujuan', label: 'Purpose / Details' },
  { key: 'pic_mulai', label: 'Start Time' },
  { key: 'pic_selesai', label: 'End Time' },
  { key: 'pic_isd', label: 'PIC by ISD' },
  { key: 'tanda_pemohon', label: 'PIC Sign' },
  { key: 'tanda_isd', label: 'ISD Sign' }
] as const

/** Only turn the red outline on after a failed save attempt, never while first typing. */
const showErrors = ref(false)

function isBlank(v: unknown): boolean {
  return v === '' || v === null || v === undefined
}

/** True when this field is mandatory, still empty, and the user has already tried to save. */
function fieldInvalid(key: string): boolean {
  return showErrors.value && REQUIRED.some((f) => f.key === key) && isBlank(form[key])
}

/* ---------------------------------------------------------------------------------------
   FILTERING
   Applied in the browser over the loaded rows, so it is instant and the table, the row
   counter and the Excel export can never disagree - they all read the same computed value.
   --------------------------------------------------------------------------------------- */
// "Unsigned only" was removed on 2026-10-04: both signatures are MANDATORY on the entry
// form, so a row without them can only be a row still being filled in - and those should not
// be searchable in the register at all.
const filters = ref({ from: '', to: '', section: '', pic: '' })
const EMPTY_FILTERS = { from: '', to: '', section: '', pic: '' }

function clearFilters() { filters.value = { ...EMPTY_FILTERS } }

/** Distinct values for the dropdowns, taken from the data itself so the lists are never stale. */
const sectionOptions = computed(() =>
  [...new Set(rows.value.map((r) => String(r.values.departemen ?? '').trim()).filter(Boolean))].sort()
)
const picOptions = computed(() =>
  [...new Set(rows.value.map((r) => String(r.values.nama_pemohon ?? '').trim()).filter(Boolean))].sort()
)

/** The date a row falls on, as yyyy-mm-dd, compared in LOCAL time to match the date pickers. */
function rowDate(v: any): string {
  if (!v) return ''
  const d = new Date(v)
  if (Number.isNaN(d.getTime())) return String(v).slice(0, 10)
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

/** Rows actually shown: the API's text search, then the filters. */
const visibleRows = computed(() => {
  const f = filters.value
  const q = search.value.trim().toLowerCase()
  return rows.value.filter((r) => {
    if (q && !JSON.stringify(r.values).toLowerCase().includes(q)) return false
    const d = rowDate(r.values.tanggal)
    if (f.from && (!d || d < f.from)) return false
    if (f.to && (!d || d > f.to)) return false
    if (f.section && String(r.values.departemen ?? '') !== f.section) return false
    if (f.pic && String(r.values.nama_pemohon ?? '') !== f.pic) return false
    return true
  })
})

/** One removable chip per active filter, so the current view is never ambiguous. */
const activeChips = computed(() => {
  const f = filters.value
  const out: { key: string; label: string; text: string }[] = []
  if (f.from || f.to) {
    const label = f.from && f.to ? 'Date' : (f.from ? 'From' : 'To')
    const text = f.from && f.to ? `${f.from} → ${f.to}` : (f.from || f.to)
    out.push({ key: 'date', label, text: String(text) })
  }
  if (f.section) out.push({ key: 'section', label: 'Section', text: f.section })
  if (f.pic) out.push({ key: 'pic', label: 'PIC Name', text: f.pic })
  return out
})

function removeChip(key: string) {
  const f = { ...filters.value }
  if (key === 'date') { f.from = ''; f.to = '' }
  if (key === 'section') f.section = ''
  if (key === 'pic') f.pic = ''
  filters.value = f
}

/** Draft of the row currently being entered. */
const form = reactive<Record<string, any>>({})

function notify(msg: string, kind: 'success' | 'error' = 'success') {
  toast.value = { msg, kind }
  setTimeout(() => { toast.value = null }, 4000)
}

function resetForm() {
  for (const c of COLUMNS) form[c.key] = c.sign ? null : ''
  for (const k of HIDDEN_FIELDS) form[k] = ''
  editing.value = null
  showErrors.value = false
}

async function loadEntity() {
  // The entity is seeded, but resolve it by slug rather than hard-coding an id so a
  // fresh database (or a renamed slug) still lands on the right target.
  const all = await apiListEntities(true)
  const meta = all.find((e: any) => e.slug === CCTV_SLUG)
  if (!meta) {
    loading.value = false
    notify('Entity CCTV Log Book belum ada di database.', 'error')
    return false
  }
  const full = await apiGetEntity(meta.id)
  entity.value = { id: full.id, name: full.name }
  fields.value = [...(full.fields as FieldMetaLite[])].sort((a, b) => a.sortOrder - b.sortOrder)
  return true
}

async function loadRows() {
  if (!entity.value) return
  // pageSize is deliberately generous because the FILTER runs on this set in the browser.
  // With a server-side date filter this would not matter, but it does today: a logbook larger
  // than this many rows would be silently truncated and the filter would look like it "lost"
  // entries. Raise this when the row count outgrows it.
  const page = await apiListRecords(entity.value.id, {
    page: 1,
    pageSize: 1000,
    search: search.value || undefined
  })
  rows.value = page.items as unknown as Row[]
  total.value = page.total
}

async function openCreate() {
  resetForm()
  // Date is fixed to today and the time fields are pre-seeded with the current clock, so the
  // common case (logging an access that is happening right now) needs no typing at all.
  // Everything stays editable - auto-filled is not the same as locked.
  form.tanggal = todayIso()
  const now = new Date()
  const hh = String(now.getHours()).padStart(2, '0')
  const mm = String(Math.floor(now.getMinutes() / 5) * 5).padStart(2, '0')
  form.pic_mulai = `${hh}:${mm}`
  form.pic_selesai = `${hh}:${mm}`
  form.pic_isd = currentUserName()
  showForm.value = true
}

function openEdit(row: Row) {
  resetForm()
  editing.value = row
  for (const c of COLUMNS) form[c.key] = row.values[c.key] ?? (c.sign ? null : '')
  // Nothing to load for hidden fields: they are not part of the form at all.

  // The API hands back dates as full ISO timestamps ("2026-10-03T00:00:00"), but
  // <input type="date"> only accepts a bare "yyyy-mm-dd" and silently renders EMPTY for
  // anything else. Because Date is required, that empty box then failed UForm validation and
  // blocked the submit entirely - so Edit appeared to do nothing at all, with no error shown.
  // Trimming to the date part is what makes Edit work; openCreate already did this by hand.
  form.tanggal = String(form.tanggal ?? '').slice(0, 10)

  // The pickers own the TIME only; the stored value carries the date too, so strip it off
  // before handing it to TimePicker, otherwise it would show the whole "03/10/26 09:00".
  for (const k of ['pic_mulai', 'pic_selesai']) {
    const m = String(form[k] ?? '').match(/(\d{1,2}):(\d{2})/)
    form[k] = m ? `${m[1]!.padStart(2, '0')}:${m[2]}` : ''
  }

  showForm.value = true
}

async function save() {
  if (!entity.value) return

  // One English warning naming every missing mandatory field, plus red outlines on each
  // empty input. Checked before anything else so a half-filled row never reaches the API.
  const missing = REQUIRED.filter((f) => isBlank(form[f.key]))
  showErrors.value = true
  if (missing.length) {
    notify(
      missing.length === 1
        ? `${missing[0]!.label} is required. Please complete the highlighted field.`
        : `These fields are required: ${missing.map((f) => f.label).join(', ')}. Please complete all highlighted fields.`,
      'error'
    )
    return
  }

  saving.value = true
  try {
    const values: Record<string, any> = {}
    for (const c of COLUMNS) {
      const v = form[c.key]
      // Start/End are stored with today's date glued onto the picked time.
      if (c.key === 'pic_mulai' || c.key === 'pic_selesai') values[c.key] = composeDateTime(v) || null
      else values[c.key] = c.sign ? (v || null) : (v === '' ? null : v)
    }
    // Hidden-but-stored fields are not shown in the form.
    //  - 'nomor' is filled by the backend on create (DynamicRecordService.FillAutoNumberAsync),
    //    so sending null here is what triggers the auto-number.
    //  - On EDIT we must OMIT the hidden fields entirely. The API's UpdateAsync is merge-only,
    //    so an absent key leaves the stored value untouched - but sending an explicit null
    //    OVERWRITES it, which trips the required+unique check on `nomor` and makes every edit
    //    fail with "Validation failed". This was the actual cause of Edit silently doing
    //    nothing while Create worked fine.
    if (editing.value) {
      for (const k of HIDDEN_FIELDS) delete values[k]
    } else {
      for (const k of HIDDEN_FIELDS) values[k] = null
    }
    if (editing.value) {
      await apiUpdateRecord(entity.value.id, editing.value.id, values)
      notify('Row updated.')
    } else {
      await apiCreateRecord(entity.value.id, values)
      notify('Row added.')
    }
    showForm.value = false
    resetForm()
    await loadRows()
  } catch (e: any) {
    notify(e?.data?.message || 'Gagal menyimpan.', 'error')
  } finally {
    saving.value = false
  }
}

async function remove(row: Row) {
  if (!entity.value) return
  const no = row.values.nomor ?? row.id
  if (!confirm(`Delete row NO ${no}?`)) return
  try {
    await apiDeleteRecord(entity.value.id, row.id)
    notify('Row deleted.')
    await loadRows()
  } catch (e: any) {
    notify(e?.data?.message || 'Gagal menghapus.', 'error')
  }
}

function fmtDate(v: any): string {
  if (!v) return ''
  const d = new Date(v)
  if (Number.isNaN(d.getTime())) return String(v)
  return d.toLocaleDateString('id-ID', { day: '2-digit', month: 'short', year: 'numeric' })
}

function signSrc(v: any): string | null {
  return typeof v === 'string' && v.startsWith('data:image/png;base64,') ? v : null
}

/**
 * Export to Excel. Exports exactly what is on screen - i.e. the current search/filter - so
 * what the user sees and what lands in the file cannot disagree.
 */
const exporting = ref(false)

async function exportExcel() {
  if (!visibleRows.value.length) { notify('Nothing to export.', 'error'); return }
  exporting.value = true
  try {
    // NO is included here even though it is hidden on screen: a spreadsheet is a register,
    // and a register without its sequence number is not one.
    const columns = [
      { key: 'nomor', label: 'NO' },
      ...COLUMNS.map((c) => ({ key: c.key as string, label: c.label as string, sign: !!c.sign }))
    ]
    const d = new Date()
    const p = (n: number) => String(n).padStart(2, '0')
    const stamp = `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}-${p(d.getHours())}${p(d.getMinutes())}`
    await exportLogbookToExcel({
      rows: visibleRows.value,
      columns,
      sheetTitle: 'CCTV Access Request Log',
      fileName: `CCTV-Access-Log-${stamp}.xlsx`
    })
    notify(`Exported ${visibleRows.value.length} rows to Excel.`)
  } catch (e: any) {
    notify(e?.message || 'Export failed.', 'error')
  } finally {
    exporting.value = false
  }
}

async function init() {
  loading.value = true
  try {
    if (await loadEntity()) await loadRows()
  } catch (e: any) {
    notify(e?.data?.message || 'Gagal memuat data.', 'error')
  } finally {
    loading.value = false
  }
}
await init()

// Re-preview the NO when the date changes, so numbering follows the year of the row.
/** Print uses the browser dialog; the @media print block below shapes the sheet. */
function printSheet() {
  if (typeof window !== 'undefined') window.print()
}

let searchTimer: any
watch(search, () => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => { loadRows().catch(() => {}) }, 300)
})
</script>

<template>
  <UDashboardPanel>
    <template #header>
      <!-- PageHeader carries the sidebar collapse control in the navbar's #leading slot,
           exactly as the Nuxt dashboard template does on every page. -->
      <PageHeader title="CCTV Access" />
    </template>

    <template #body>
      <div class="anim-stagger space-y-4">
        <!-- Sheet header. "Name of System" and "Dept" were removed on 2026-10-02 at
             HIRO's request; the title and the actions are all that remain. -->
        <div class="rounded-lg border border-default bg-elevated p-4">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-lg font-bold tracking-wide">CCTV Access Request Log</h1>
        </div>
        <div class="flex gap-2">
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search..." class="w-48" />
          <LogbookFilterPopover
            v-model="filters"
            :sections="sectionOptions"
            :pics="picOptions"
            :match-count="visibleRows.length"
            :total-count="rows.length"
            @clear="clearFilters"
          />
          <UButton icon="i-lucide-plus" label="Add Row" @click="openCreate" />
          <!-- Excel sits immediately left of Print: both are "get the data out of here"
               actions and share the `soft` variant so they read as a pair, with Print kept
               rightmost so it stays where the muscle memory is. Label shortened from
               "Export Excel" to "Excel" (HIRO) - the sheet icon plus the word Excel says it
               already, and the longer label pushed the toolbar wider than it needed to be. -->
          <UButton icon="i-lucide-file-spreadsheet" label="Excel" variant="soft"
                   :loading="exporting" @click="exportExcel" />
          <UButton icon="i-lucide-printer" label="Print" variant="soft" @click="printSheet" />
        </div>
      </div>
    </div>

    <!-- Save feedback is TELEPORTED to <body> on purpose. Rendered inline it sat inside
         #__nuxt, which carries `isolate` and therefore forms its own stacking context: no
         z-index on any descendant of it can ever paint above a UModal, because the modal is
         teleported to a LATER sibling of #__nuxt. That is why "Validation failed" was
         invisible behind the open modal. Teleporting to body makes the toast a sibling of the
         modal, and since it is appended later, z-[60] (over UModal's z-50) puts it on top.
         top-20 clears the 64px sticky navbar. -->
    <Teleport to="body">
      <div v-if="toast" class="pointer-events-none fixed left-1/2 top-20 z-[60] -translate-x-1/2 rounded-lg border px-4 py-2.5 text-sm shadow-lg backdrop-blur"
           :class="toast.kind === 'error' ? 'border-error bg-error/15 text-error' : 'border-success bg-success/15 text-success'">
        {{ toast.msg }}
      </div>
    </Teleport>

    <!-- Active filters, as removable chips. These exist so the current view is never
         ambiguous: without them a user can forget a filter is on, print the wrong thing or
         export the wrong thing and not notice until afterwards. -->
    <!-- COLLAPSIBLE SLOT. The chips used to be `v-if`'d straight into the flow, so the moment
         a chip appeared the table below was shoved down by a whole line-height in one frame -
         a jump. Now the row is always present and only its HEIGHT animates (0fr -> 1fr), so
         the table is pushed down progressively and lands exactly where it belongs. The chips
         themselves still keep their own fade-up.
         `min-height: 0` on the clip is what makes the 0fr track actually collapse to nothing
         rather than to the content's intrinsic height. -->
    <div class="chips-slot" :class="activeChips.length ? 'is-open' : ''">
     <div class="chips-clip">
      <div class="flex flex-wrap items-center gap-1.5 px-0.5 pb-2 pt-1">
      <span class="mr-0.5 text-xs font-medium text-muted">Filtered by</span>
      <button
        v-for="c in activeChips" :key="c.key"
        type="button"
        class="group inline-flex items-center gap-1 rounded-full border border-primary/40 bg-primary/10 py-1 pl-2.5 pr-1.5 text-xs text-primary transition-colors hover:bg-primary/20"
        :title="`Remove filter: ${c.label || c.text}`"
        @click="removeChip(c.key)"
      >
        <span v-if="c.label" class="font-semibold">{{ c.label }}:</span>
        <span>{{ c.text }}</span>
        <UIcon name="i-lucide-x" class="size-3 opacity-60 transition-opacity group-hover:opacity-100" />
      </button>
      <UButton size="xs" variant="ghost" color="error" label="Clear all" class="ml-1" @click="clearFilters" />
      </div>
     </div>
    </div>

    <!-- Logbook table.
         Professional look without losing the printed sheet: the grid uses `border-default`
         so the @media print block can force it to solid black, the header is sticky for
         long scrolls, and zebra striping + hover give row tracking on screen. Type is
         slightly larger than the old text-xs, and numeric-ish columns are tabular-nums
         so digits line up down the column. -->
    <div class="overflow-hidden rounded-xl border border-default bg-elevated">
      <div class="max-h-[70vh] overflow-auto print:scroll-area">
        <!-- min-w is sized so the whole logbook fits without horizontal scrolling on a
             typical 1280px screen (measured: 11 columns + Actions needed ~1180px).
             Horizontal scroll remains the graceful fallback on narrower screens rather
             than squashing the signature columns until they are unreadable. -->
        <table class="w-full min-w-[1180px] border-collapse text-sm">
          <thead class="print:sticky-head sticky top-0 z-10">
            <tr class="bg-default/60 backdrop-blur-sm">
              <th v-for="c in COLUMNS" :key="c.key"
                  scope="col"
                  class="border-b border-default px-3 py-2.5 text-left align-middle text-xs font-semibold uppercase tracking-wide text-muted"
                  :class="c.w">
                {{ c.label }}
              </th>
              <th scope="col"
                  class="w-[5%] border-b border-default px-2 py-2.5 text-center align-middle text-xs font-semibold uppercase tracking-wide text-muted print:hidden">
                Actions
              </th>
            </tr>
          </thead>
          <!-- Loading and empty live in their OWN <tbody>, so the data rows can sit inside a
               <TransitionGroup tag="tbody">. A table allows several tbody elements, which is
               the clean way to get a keyed, animatable row list without a wrapper that would
               break the table layout. -->
          <tbody v-if="loading">
            <tr>
              <td :colspan="COLUMNS.length + 1" class="px-4 py-10 text-center text-muted">
                <UIcon name="i-lucide-loader-circle" class="mx-auto mb-2 size-5 animate-spin" />
                <p class="text-sm">Loading...</p>
              </td>
            </tr>
          </tbody>
          <tbody v-else-if="!visibleRows.length">
            <tr>
              <td :colspan="COLUMNS.length + 1" class="px-4 py-12 text-center">
                <UIcon name="i-lucide-inbox" class="mx-auto mb-2 size-7 text-dimmed" />
                <p class="text-sm font-medium">
                  {{ activeChips.length || search ? 'No rows match your filter' : 'No rows yet' }}
                </p>
                <p class="mt-0.5 text-xs text-muted">
                  {{ activeChips.length || search
                    ? 'Adjust or clear the filters to see the rest.'
                    : 'Use “Add Row” to record the first entry.' }}
                </p>
              </td>
            </tr>
          </tbody>

          <!-- Row transitions. Rows that survive a filter change slide to their new position
               (FLIP move) instead of teleporting, and rows that newly match fade in from
               slightly above. Rows being filtered OUT are removed immediately - see the
               .row-* rules for why animating them in a table is more trouble than it is worth. -->
          <TransitionGroup
            v-else
            tag="tbody"
            name="row"
            enter-active-class="row-enter-active"
            enter-from-class="row-enter-from"
            move-class="row-move"
            move-active-class="row-move-active"
          >
            <tr v-for="(r, i) in visibleRows" :key="r.id"
                class="align-middle transition-colors hover:bg-primary/5"
                :class="i % 2 ? 'bg-default/20' : ''">
              <td v-for="c in COLUMNS" :key="c.key"
                  class="border-b border-default/60 px-3 py-2.5 align-middle">
                <img v-if="c.sign && signSrc(r.values[c.key])" :src="signSrc(r.values[c.key])!"
                     alt="signature" class="h-9 w-full object-contain" />
                <span v-else-if="c.sign" class="text-dimmed">—</span>
                <template v-else-if="c.key === 'tanggal'">
                  <span class="tabular-nums whitespace-nowrap">{{ fmtDate(r.values[c.key]) }}</span>
                </template>
                <template v-else>
                  <span class="block break-words">{{ r.values[c.key] ?? '—' }}</span>
                </template>
              </td>
              <td class="border-b border-default/60 px-1 py-2 text-center whitespace-nowrap print:hidden">
                <UButton icon="i-lucide-pencil" size="xs" variant="ghost" color="neutral"
                         @click="openEdit(r)" />
                <UButton icon="i-lucide-trash-2" size="xs" variant="ghost" color="error"
                         @click="remove(r)" />
                         </td>
                         </tr>
                         </TransitionGroup>
                         </table>
      </div>

      <div class="flex items-center justify-between gap-3 border-t border-default bg-default/30 px-4 py-2.5 text-xs text-muted print:hidden">
        <!-- Left count follows the FILTERS, so it agrees with the "Showing X of Y" beside it.
             Previously it printed the raw DB total, which read "1 row / Showing 0 of 1" and
             looked like a bug. -->
        <span>{{ visibleRows.length }} {{ visibleRows.length === 1 ? 'row' : 'rows' }}</span>
        <span class="tabular-nums">Showing {{ visibleRows.length }} of {{ total }}</span>
      </div>
    </div>

    <!-- Entry form lives in a UModal (converted from an inline panel on 2026-10-02).
         UModal brings its own smooth enter/leave transition plus backdrop blur and an
         ESC-to-close, all of which the inline panel could not do.
         DO NOT move the Save/Cancel buttons into UModal's #footer slot: that slot swallows
         @click on a submit button in this codebase, which is exactly why this form was an
         inline panel to begin with. They stay inside the <UForm> in the #body slot so the
         native form submit path stays intact. -->
    <UModal v-model:open="showForm" :ui="{ content: 'sm:max-w-4xl', body: 'p-4 sm:p-5' }"
            :title="editing ? 'Edit Row' : 'New Row'"
            :description="editing ? 'Update this CCTV access log entry.' : 'Record a new CCTV access request.'">
      <template #body>
      <!-- `:validate-on="[]"` switches OFF UForm's own validation. It is off on purpose:
           `required` on UFormField is kept only for the red asterisk, and this page does
           the checking itself so it can raise ONE English warning listing every missing
           field and outline them all in red. -->
      <UForm :state="form" :validate-on="[]" @submit="save">
        <!-- Five rows on a 3-column grid, no scroll area: the dialog is sized to fit its
             content (see the :ui below) and the two signature pads sit side by side in the
             last row, which is what keeps the whole form inside a 1080p window. -->
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <!-- Row 1: who/when/where. Date is prefilled with today and stays editable. -->
          <UFormField label="Date" name="tanggal" required>
            <UInput v-model="form.tanggal" name="tanggal" type="date" class="w-full"
                     :ui="fieldInvalid('tanggal') ? { base: 'ring-2 ring-error' } : undefined" />
          </UFormField>
          <UFormField label="Section" name="departemen" required>
            <UInput v-model="form.departemen" name="departemen" placeholder="ISD / CAP"
                     class="w-full" :ui="fieldInvalid('departemen') ? { base: 'ring-2 ring-error' } : undefined" />
          </UFormField>
          <UFormField label="Employee No" name="no_pegawai" required>
            <UInput v-model="form.no_pegawai" name="no_pegawai" placeholder="940900"
                     class="w-full" :ui="fieldInvalid('no_pegawai') ? { base: 'ring-2 ring-error' } : undefined" />
          </UFormField>

          <!-- Row 2: the requester and the searched window. Pickers take the TIME only;
               today's date is glued on at save time, so nobody types a date. -->
          <UFormField label="PIC Name" name="nama_pemohon" required>
            <UInput v-model="form.nama_pemohon" name="nama_pemohon" placeholder="Name of the requester" class="w-full"
                     :ui="fieldInvalid('nama_pemohon') ? { base: 'ring-2 ring-error' } : undefined" />
          </UFormField>
          <UFormField label="Start Time" name="pic_mulai" required>
            <TimePicker v-model="form.pic_mulai" name="pic_mulai" :invalid="fieldInvalid('pic_mulai')" />
          </UFormField>
          <UFormField label="End Time" name="pic_selesai" required>
            <TimePicker v-model="form.pic_selesai" name="pic_selesai" :invalid="fieldInvalid('pic_selesai')" />
          </UFormField>

          <!-- Row 3: purpose spans the full width. w-full is required as well as the
               col-span: UTextarea sizes to its content otherwise and ignores the span. -->
          <UFormField label="Purpose / Details" name="tujuan" required class="sm:col-span-2 lg:col-span-3">
            <UTextarea v-model="form.tujuan" name="tujuan" :rows="2" class="w-full"
                      placeholder="What is the footage being retrieved for?"
                      :ui="fieldInvalid('tujuan') ? { base: 'ring-2 ring-error' } : undefined" />
          </UFormField>

          <!-- Row 4: the ISD officer prefilled from the session, then the two signature pads
               side by side. Giving both pads one grid column each is what keeps them
               adjacent - a col-span on the second one pushed it onto a row of its own. -->
          <UFormField label="PIC by ISD" name="pic_isd" required>
            <UInput v-model="form.pic_isd" name="pic_isd" class="w-full"
                     :ui="fieldInvalid('pic_isd') ? { base: 'ring-2 ring-error' } : undefined" />
          </UFormField>
          <UFormField label="PIC Sign" name="tanda_pemohon" required>
            <SignaturePad v-model="form.tanda_pemohon" :height="64" :invalid="fieldInvalid('tanda_pemohon')" />
          </UFormField>
          <UFormField label="ISD Sign" name="tanda_isd" required>
            <SignaturePad v-model="form.tanda_isd" :height="64" :invalid="fieldInvalid('tanda_isd')" />
          </UFormField>
        </div>

        <div class="mt-4 flex items-center justify-between gap-2 border-t border-default pt-4">
          <!-- Footnote on the LEFT, action buttons on the right: the legend for the red
               asterisks, which is where a reader looks first when a save is rejected. -->
          <p class="text-xs text-muted">
            <span class="font-semibold text-error">*</span> Mandatory — all fields must be completed.
          </p>
          <div class="flex items-center gap-2">
          <UButton type="button" variant="ghost" label="Cancel" @click="showForm = false" />
          <UButton type="submit" :loading="saving" icon="i-lucide-check"
                   :label="editing ? 'Save Changes' : 'Save Row'" />
          </div>
        </div>
      </UForm>
      </template>
    </UModal>
      </div>
    </template>
  </UDashboardPanel>
</template>

<style>
/* ---------------------------------------------------------------------------------------
   Collapsible filter-chip slot.

   Animating `grid-template-rows` from 0fr to 1fr is the only way to transition to an
   unknown content height in pure CSS: the track interpolates, and the table underneath is
   pushed down progressively instead of being shoved a whole line in one frame. A plain
   `v-if` gave an instant jump; a max-height would need a hard-coded number that breaks the
   moment a second chip wraps to a new line. */
.chips-slot {
  display: grid;
  grid-template-rows: 0fr;
  transition: grid-template-rows 220ms cubic-bezier(0.22, 1, 0.36, 1);
}
.chips-slot.is-open {
  grid-template-rows: 1fr;
}
/* min-height:0 is required, otherwise the 0fr track still reserves the content's
   intrinsic height and the row never collapses. */
.chips-clip {
  overflow: hidden;
  min-height: 0;
}

@media print, (prefers-reduced-motion: reduce) {
  .chips-slot {
    transition: none !important;
  }
}

/* ---------------------------------------------------------------------------------------
   Row transitions for the logbook (filter / search changes).

   Design notes:
   - `transform` only, never a property that triggers layout. Animating width/height/table
     cells re-rasterises and is exactly what caused the sidebar "ghost text" bug, so the same
     rule applies here.
   - NO leave transition. A <tr> that animates out still occupies its slot until the
     transition ends, and taking it out of flow (position:absolute) on a table row drops the
     borders and column alignment for the whole table mid-animation. Removing it instantly
     while the surviving rows slide to their new positions reads cleanly and is far cheaper.
   - 220ms with an ease-out curve: long enough to see the motion, short enough that the table
     never feels like it is lagging behind the click.
   --------------------------------------------------------------------------------------- */
.row-enter-active {
  transition: opacity 220ms cubic-bezier(0.22, 1, 0.36, 1), transform 220ms cubic-bezier(0.22, 1, 0.36, 1);
}
.row-enter-from {
  opacity: 0;
  transform: translateY(-8px);
}
/* FLIP: Vue measures the row before and after, so only the transform needs animating. */
.row-move-active {
  transition: transform 220ms cubic-bezier(0.22, 1, 0.36, 1);
}
.row-move {
  transition: transform 220ms cubic-bezier(0.22, 1, 0.36, 1);
}

/* Printing must never capture a half-finished animation. */
@media print {
  .row-enter-active,
  .row-move,
  .row-move-active {
    transition: none !important;
    opacity: 1 !important;
    transform: none !important;
  }
}

/* Respect the OS "reduce motion" setting outright, matching the rule already used by the
   .anim-* utilities in main.css. */
@media (prefers-reduced-motion: reduce) {
  .row-enter-active,
  .row-move,
  .row-move-active {
    transition: none !important;
    opacity: 1 !important;
    transform: none !important;
  }
}

/* A4 landscape sheet: the form is 11 columns wide, portrait would be unreadable. */
@media print {
  @page { size: A4 landscape; margin: 8mm; }
  body { background: #fff !important; }
  .print\:hidden { display: none !important; }

  /* The screen table lives inside a max-h scroll container with a sticky header; on
     paper both must go, or the sheet is clipped to one viewport-height and repeats the
     header on every page. */
  .print\:scroll-area { max-height: none !important; overflow: visible !important; }
  .print\:sticky-head { position: static !important; }

  /* On screen the grid is bottom-rules only (border-b) for a cleaner look; the printed
     sheet needs a full grid so every cell is a distinct box. */
  table { min-width: 0 !important; width: 100% !important; }
  table, td, th { border-color: #000 !important; }
  th, td { border: 1px solid #000 !important; }
  tr { page-break-inside: avoid; }
}
</style>