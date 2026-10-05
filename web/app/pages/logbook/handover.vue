<script setup lang="ts">
import { apiListRecords, apiCreateRecord, apiUpdateRecord, apiDeleteRecord, apiGetEntity, apiListEntities } from '~/composables/useApi'
import { exportLogbookToExcel } from '~/composables/useExcelExport'

/**
 * Handover Log Book — /logbook/handover
 *
 * Records every IT part handed over to a user (HIRO, 2026-10-05).
 *
 * Data lives in the generic metadata-driven engine (entity slug handover_log_book), so
 * storage, validation, audit and the sequential NO all run through exactly the same code
 * path as the CCTV Log Book. Only the presentation differs, and it deliberately mirrors
 * cctvacc.vue: same panel shell, same toolbar, same filter popover and chips, same
 * table-fixed grid, same row/FLIP transitions, same reveal+dismiss modal motion, same
 * accent glow, same delete-confirmation dialog. Two logbooks that look and behave
 * differently would read as two different applications.
 *
 * Column order is EXACTLY what HIRO asked for:
 *   Taken Date, Part Name, Brand, QTY, Employee No, Name, Section, Signature, Remarks
 * The sheet is a register of IT ASSETS first and their recipients second, so the part
 * identity leads and the person follows.
 *
 * No route middleware: the other pages of this app have none either. Authorisation is
 * enforced by the API (JWT on every /api route).
 */
const HANDOVER_SLUG = 'handover_log_book'

interface FieldMetaLite { id: number; name: string; label: string; type: string; sortOrder: number }
interface Row { id: number; values: Record<string, any>; createdBy: string; createdAt: string }

const entity = ref<{ id: number; name: string } | null>(null)
const fields = ref<FieldMetaLite[]>([])
const rows = ref<Row[]>([])
const total = ref(0)
const loading = ref(true)
const saving = ref(false)
/** Nuxt UI's own toast, the same one every other page uses. */
const toast = useToast()
const search = ref('')
const showForm = ref(false)
const editing = ref<Row | null>(null)

/**
 * Column order and width, per HIRO's list. `taken` is the only date, so the date filter
 * on this page reads `tanggal_ambil` rather than CCTV's `tanggal` - the field names differ
 * between the two logbooks and sharing one page component would have meant aliasing one of
 * them for no benefit.
 */
const COLUMNS = [
  { key: 'tanggal_ambil', label: 'Taken Date', w: 'w-[11%]' },
  { key: 'nama_barang', label: 'Part Name', w: 'w-[18%]' },
  { key: 'merek', label: 'Brand', w: 'w-[11%]' },
  { key: 'qty', label: 'QTY', w: 'w-[6%]' },
  { key: 'no_pegawai', label: 'Employee No', w: 'w-[10%]' },
  { key: 'nama', label: 'Name', w: 'w-[15%]' },
  { key: 'departemen', label: 'Section', w: 'w-[11%]' },
  { key: 'tanda', label: 'Signature', w: 'w-[11%]', sign: true },
  { key: 'catatan', label: 'Remarks', w: 'w-[15%]' }
]

/**
 * `nomor` is stored and printed but hidden on screen and absent from the form, exactly as
 * on the CCTV sheet. The backend fills it on create (DynamicRecordService -> the
 * sequential-number provider), and on EDIT the key must be OMITTED entirely: the API's
 * UpdateAsync is merge-only, so an absent key leaves the stored value alone - whereas an
 * explicit null OVERWRITES it and trips the required+unique check, which is what used to
 * make Edit silently do nothing on the CCTV page.
 */
const HIDDEN_FIELDS = ['nomor'] as const

/** The signed-in user, used to prefill "Name" so nobody has to type their own name. */
const { user: me } = useAuth()

/** yyyy-mm-dd for today, in LOCAL time. */
function todayIso(): string {
  const d = new Date()
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

/** The display name of whoever is signed in, falling back to the username. */
function currentUserName(): string {
  const u = me.value as any
  return String(u?.fullName || u?.username || '')
}

/**
 * MANDATORY FIELDS: everything a handover record must carry to be meaningful. QTY is
 * required because a handover without a quantity is not a record of anything - but it is
 * NOT validated as "greater than zero" here, matching how the engine stores it: a Number
 * field, so the API rejects a non-numeric value on its own.
 *
 * `required` on <UFormField> is used purely for the red asterisk. UForm's own validation is
 * switched off below (`:validate-on="[]"`) so this page can raise ONE English warning naming
 * every missing field at once, with the empty inputs outlined in red.
 */
const REQUIRED = [
  { key: 'tanggal_ambil', label: 'Taken Date' },
  { key: 'nama_barang', label: 'Part Name' },
  { key: 'qty', label: 'QTY' },
  { key: 'nama', label: 'Name' },
  { key: 'tanda', label: 'Signature' }
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
const filters = ref({ from: '', to: '', section: '', pic: '' })
const EMPTY_FILTERS = { from: '', to: '', section: '', pic: '' }

function clearFilters() { filters.value = { ...EMPTY_FILTERS } }

/** Distinct values for the dropdowns, taken from the data itself so the lists are never stale. */
const sectionOptions = computed(() =>
  [...new Set(rows.value.map((r) => String(r.values.departemen ?? '').trim()).filter(Boolean))].sort()
)
/** The popover's second dropdown is labelled "Name" on this page - it filters the recipient. */
const nameOptions = computed(() =>
  [...new Set(rows.value.map((r) => String(r.values.nama ?? '').trim()).filter(Boolean))].sort()
)

/** The date a row falls on, as yyyy-mm-dd, compared in LOCAL time to match the date pickers. */
function rowDate(v: any): string {
  if (!v) return ''
  const d = new Date(v)
  if (Number.isNaN(d.getTime())) return String(v).slice(0, 10)
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

/**
 * Fields that are NOT part of a text search.
 *
 * THE SIGNATURE COLUMN MUST STAY OUT. Each stored signature is a base64 PNG data-URL of
 * roughly 2000 characters, and a predicate built on the raw JSON searches the base64
 * itself: any short term turns up inside random base64, which is exactly what made the
 * CCTV search feel arbitrary ("sya" matched 4 rows instead of 1).
 */
const NON_SEARCHABLE = new Set(['tanda'])

/** One lower-cased string per row, built from real field values only. */
function searchHaystack(values: Record<string, any>): string {
  let out = ''
  for (const k in values) {
    if (NON_SEARCHABLE.has(k)) continue
    const v = values[k]
    if (v === null || v === undefined) continue
    out += ' ' + String(v)
  }
  return out.toLowerCase()
}

/** Rows actually shown: the text search, then the filters. */
const visibleRows = computed(() => {
  const f = filters.value
  const q = search.value.trim().toLowerCase()
  return rows.value.filter((r) => {
    if (q && !searchHaystack(r.values).includes(q)) return false
    const d = rowDate(r.values.tanggal_ambil)
    if (f.from && (!d || d < f.from)) return false
    if (f.to && (!d || d > f.to)) return false
    if (f.section && String(r.values.departemen ?? '') !== f.section) return false
    if (f.pic && String(r.values.nama ?? '') !== f.pic) return false
    return true
  })
})

/** One removable chip per active filter, so the current view is never ambiguous. */
const activeChips = computed(() => {
  const f = filters.value
  const out: { key: string; label: string; text: string }[] = []
  if (f.from || f.to) {
    const label = f.from && f.to ? 'Taken Date' : (f.from ? 'From' : 'To')
    const text = f.from && f.to ? `${f.from} → ${f.to}` : (f.from || f.to)
    out.push({ key: 'date', label, text: String(text) })
  }
  if (f.section) out.push({ key: 'section', label: 'Section', text: f.section })
  if (f.pic) out.push({ key: 'name', label: 'Name', text: f.pic })
  return out
})

function removeChip(key: string) {
  const f = { ...filters.value }
  if (key === 'date') { f.from = ''; f.to = '' }
  if (key === 'section') f.section = ''
  if (key === 'name') f.pic = ''
  filters.value = f
}

/** Draft of the row currently being entered. */
const form = reactive<Record<string, any>>({})

function notify(msg: string, kind: 'success' | 'error' = 'success') {
  toast.add({ title: msg, color: kind })
}

function resetForm() {
  for (const c of COLUMNS) form[c.key] = c.sign ? null : ''
  for (const k of HIDDEN_FIELDS) form[k] = ''
  editing.value = null
  showErrors.value = false
}

async function loadEntity() {
  // Resolved by slug rather than hard-coded id, so a fresh database still lands correctly.
  const all = await apiListEntities(true)
  const meta = all.find((e: any) => e.slug === HANDOVER_SLUG)
  if (!meta) {
    loading.value = false
    notify('Handover Log Book entity not found in the database.', 'error')
    return false
  }
  const full = await apiGetEntity(meta.id)
  entity.value = { id: full.id, name: full.name }
  fields.value = [...(full.fields as FieldMetaLite[])].sort((a, b) => a.sortOrder - b.sortOrder)
  return true
}

async function loadRows() {
  if (!entity.value) return
  // Fetched ONCE: search and filters both run over this set in the browser, so there is no
  // per-keystroke round trip and no race between two out-of-order responses.
  // pageSize 500 is the API's hard ceiling (the service clamps to 1..500).
  const page = await apiListRecords(entity.value.id, { page: 1, pageSize: 500 })
  rows.value = page.items as unknown as Row[]
  total.value = page.total
}

async function openCreate() {
  resetForm()
  // Taken Date defaults to today, Name to the signed-in user and QTY to 1 - the common case
  // (handing one part to yourself right now) then needs no typing at all. Everything stays
  // editable: auto-filled is not the same as locked.
  form.tanggal_ambil = todayIso()
  form.nama = currentUserName()
  form.qty = '1'
  showForm.value = true
}

function openEdit(row: Row) {
  resetForm()
  editing.value = row
  for (const c of COLUMNS) form[c.key] = row.values[c.key] ?? (c.sign ? null : '')
  // The API returns dates as full ISO timestamps ("2026-10-05T00:00:00"), but the themed
  // picker takes a bare "yyyy-mm-dd" and renders EMPTY for anything else - which, on a
  // required field, then blocks the submit and makes Edit look like it does nothing.
  form.tanggal_ambil = String(form.tanggal_ambil ?? '').slice(0, 10)
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
      // QTY is a Number field server-side; sending '' would fail validation, so an empty
      // value becomes null rather than an empty string.
      values[c.key] = c.sign ? (v || null) : (v === '' ? null : v)
    }
    // Hidden-but-stored fields: sent as null on CREATE so the backend auto-numbers, and
    // OMITTED on EDIT - see HIDDEN_FIELDS for why an explicit null there breaks the update.
    if (editing.value) {
      for (const k of HIDDEN_FIELDS) delete values[k]
    } else {
      for (const k of HIDDEN_FIELDS) values[k] = null
    }
    if (editing.value) {
      await apiUpdateRecord(entity.value.id, editing.value.id, values)
      notify('Record updated')
    } else {
      await apiCreateRecord(entity.value.id, values)
      notify('Record created')
    }
    showForm.value = false
    resetForm()
    await loadRows()
  } catch (e: any) {
    notify(e?.data?.message || 'Could not save the record.', 'error')
  } finally {
    saving.value = false
  }
}

/**
 * Delete confirmation. A real UModal rather than a native confirm(), matching the CCTV
 * page: it repeats the row's identity so the person confirms the row they MEANT (the table
 * row is no longer visible behind the backdrop), and it carries the same staggered motion.
 */
const showDelete = ref(false)
const deleteTarget = ref<Row | null>(null)
const deleting = ref(false)

function askDelete(row: Row) {
  deleteTarget.value = row
  showDelete.value = true
}

/** The identity shown in the delete dialog - the part first, then who received it. */
const deleteDetails = computed(() => {
  const r = deleteTarget.value
  if (!r) return [] as { label: string, value: any }[]
  return [
    { label: 'Part Name', value: r.values.nama_barang },
    { label: 'Name', value: r.values.nama },
    { label: 'Employee No', value: r.values.no_pegawai },
    { label: 'Taken Date', value: fmtDate(r.values.tanggal_ambil) },
    { label: 'QTY', value: r.values.qty }
  ]
})

async function confirmDelete() {
  if (!entity.value || !deleteTarget.value) return
  deleting.value = true
  try {
    await apiDeleteRecord(entity.value.id, deleteTarget.value.id)
    notify('Record deleted')
    showDelete.value = false
    deleteTarget.value = null
    await loadRows()
  } catch (e: any) {
    notify(e?.data?.message || 'Could not delete the record.', 'error')
  } finally {
    deleting.value = false
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
 * the view and the file cannot disagree. NO is included even though it is hidden on screen:
 * a spreadsheet is a register, and a register without its sequence number is not one.
 */
const exporting = ref(false)

async function exportExcel() {
  if (!visibleRows.value.length) { notify('Nothing to export.', 'error'); return }
  exporting.value = true
  try {
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
      sheetTitle: 'Handover Log Book',
      fileName: `Handover-Log-${stamp}.xlsx`
    })
    notify(`Exported ${visibleRows.value.length} rows to Excel`)
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
    notify(e?.data?.message || 'Could not load the data.', 'error')
  } finally {
    loading.value = false
  }
}
await init()
</script>

<template>
  <!-- Per-instance :ui on the panel body, exactly as on the CCTV page: `scrollbar-gutter-stable`
       reserves the 15px so filtering can never make the grid jump sideways, and
       `overflow-y-scroll` keeps the track permanently rendered so it cannot blink.
       Scoped to THIS page on purpose - a global dashboardPanel theme override was tried on
       the CCTV fix and rejected, because it changed pages that never had the bug. -->
  <UDashboardPanel :ui="{ body: 'handover-panel-body overflow-y-scroll scrollbar-gutter-stable' }">
    <template #header>
      <!-- PageHeader carries the sidebar collapse control in the navbar's #leading slot,
           exactly as the Nuxt dashboard template does on every page. -->
      <PageHeader title="Handover" />
    </template>

    <template #body>
      <div class="anim-stagger space-y-4">
        <!-- Sheet header: title on the left, actions on the right. Same order as the CCTV
             toolbar (Search | Filter | Excel | Add Record) so the muscle memory carries over. -->
        <div class="rounded-lg border border-default bg-elevated p-4">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h1 class="text-lg font-bold tracking-wide">Handover Log Book</h1>
            </div>
            <div class="flex gap-2">
              <UInput v-model="search" icon="i-lucide-search" placeholder="Search..." class="w-48" />
              <LogbookFilterPopover
                v-model="filters"
                :sections="sectionOptions"
                :pics="nameOptions"
                person-label="Name"
                person-placeholder="All recipients"
                :match-count="visibleRows.length"
                :total-count="rows.length"
                @clear="clearFilters"
              />
              <UButton icon="i-lucide-file-spreadsheet" label="Excel" variant="soft"
                       :loading="exporting" @click="exportExcel" />
              <UButton icon="i-lucide-plus" label="Add Record" @click="openCreate" />
            </div>
          </div>
        </div>

        <!-- Active filters as removable chips. The row is ALWAYS present and only its height
             animates (0fr -> 1fr), so the table below is pushed down progressively instead of
             being shoved a whole line in one frame; the v-if sits on the INNER row so the
             "Filtered by" label and Clear all do not survive in the DOM at 0px height. -->
        <div class="chips-slot" :class="activeChips.length ? 'is-open' : ''">
          <div class="chips-clip">
            <div v-if="activeChips.length" class="flex flex-wrap items-center gap-1.5 px-0.5 pb-2 pt-1">
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

        <!-- Register table. `table-fixed` + a colgroup because percentage widths on <th> are
             only HINTS under the default auto layout - the browser hands out leftover space
             unevenly, which is why the CCTV signature columns matched at 1280px but drifted
             apart on the office 1920px screen. The signature column's FIXED chip width gives
             it a deterministic content box too. -->
        <div class="overflow-hidden rounded-xl border border-default bg-elevated">
          <div class="logbook-scroll max-h-[70vh] overflow-y-scroll overflow-x-auto print:scroll-area">
            <table class="w-full min-w-[1240px] table-fixed border-collapse text-sm">
              <!-- Exactly 10 <col> for 10 columns: the 9 in COLUMNS plus Actions. They sum to
                   more than 100% on purpose, so the browser scales them all by one factor -
                   which is what keeps the relationship between equal entries intact. -->
              <colgroup>
                <col style="width: 11%" />  <!-- Taken Date -->
                <col style="width: 18%" />  <!-- Part Name -->
                <col style="width: 11%" />  <!-- Brand -->
                <col style="width: 6%" />   <!-- QTY -->
                <col style="width: 10%" />  <!-- Employee No -->
                <col style="width: 15%" />  <!-- Name -->
                <col style="width: 11%" />  <!-- Section -->
                <col style="width: 11%" />  <!-- Signature -->
                <col style="width: 15%" />  <!-- Remarks -->
                <col style="width: 6%" />   <!-- Actions -->
              </colgroup>
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
                   the clean way to get a keyed, animatable row list without a wrapper div that
                   would break the table layout. -->
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
                        : 'Use “Add Record” to log the first handover.' }}
                    </p>
                  </td>
                </tr>
              </tbody>

              <!-- Row transitions: rows that survive a filter change slide to their new
                   position (FLIP move) instead of teleporting, and rows that newly match fade
                   in from slightly above. Filtered-OUT rows are removed instantly - see the
                   .row-* rules for why animating them in a table is more trouble than worth. -->
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
                    <!-- The stored PNG is black ink on a TRANSPARENT background, which vanishes
                         on the dark table. The INK is flipped instead: `dark:invert` turns black
                         pixels white and leaves the ALPHA channel alone, so the cell keeps the
                         theme's own background and the signature reads as white ink on dark.
                         Nothing stored is modified. The chip is a FIXED w-[108px], not min-w,
                         so this column has the same content width on every row. -->
                    <div v-if="c.sign && signSrc(r.values[c.key])"
                         class="flex w-[108px] items-center justify-center rounded-md border border-default/50 p-1">
                      <img :src="signSrc(r.values[c.key])!" alt="signature"
                           class="h-8 max-w-full object-contain dark:invert" />
                    </div>
                    <span v-else-if="c.sign" class="text-dimmed">—</span>
                    <template v-else-if="c.key === 'tanggal_ambil'">
                      <span class="tabular-nums whitespace-nowrap">{{ fmtDate(r.values[c.key]) }}</span>
                    </template>
                    <!-- QTY is centred and tabular so a column of digits lines up. -->
                    <template v-else-if="c.key === 'qty'">
                      <span class="tabular-nums text-center">{{ r.values[c.key] ?? '—' }}</span>
                    </template>
                    <template v-else>
                      <span class="block break-words">{{ r.values[c.key] ?? '—' }}</span>
                    </template>
                  </td>
                  <td class="border-b border-default/60 px-1 py-2 text-center whitespace-nowrap print:hidden">
                    <UButton icon="i-lucide-pencil" size="xs" variant="ghost" color="neutral"
                             @click="openEdit(r)" />
                    <UButton icon="i-lucide-trash-2" size="xs" variant="ghost" color="error"
                             @click="askDelete(r)" />
                  </td>
                </tr>
              </TransitionGroup>
            </table>
          </div>

          <!-- Left count follows the FILTERS so it agrees with "Showing X of Y" beside it. -->
          <div class="flex items-center justify-between gap-3 border-t border-default bg-default/30 px-4 py-2.5 text-xs text-muted print:hidden">
            <span>{{ visibleRows.length }} {{ visibleRows.length === 1 ? 'row' : 'rows' }}</span>
            <span class="tabular-nums">Showing {{ visibleRows.length }} of {{ total }}</span>
          </div>
        </div>

        <!-- Entry form lives in a UModal, matching the CCTV dialog. The Save/Cancel buttons
             stay INSIDE the <UForm> in the #body slot: UModal's #footer slot swallows @click on
             a submit button in this codebase, which is exactly why these forms cannot use it. -->
        <!-- `header` and `body` get `relative z-10` so they paint ABOVE the ::after accent glow
             defined in the non-scoped style block at the foot of this file. The glow is a
             pseudo-element rather than a div in the #content slot on purpose - that slot is
             documented in the CCTV file as having broken the form's submit path, and the content
             element is already `overflow: hidden`, which is exactly the clipping that makes the
             orb read as light bleeding in from the corner. -->
        <!-- THE HEADER IS REPLACED WHOLESALE, matching the CCTV dialog exactly (HIRO, 2026-10-05:
         "on new handover form adapt style from cctv form ... make the section title like form
         cctv access"). Two logbooks that look different read as two different applications, so
         this is a deliberate copy rather than a parallel design.

         REPLACING #header INSTEAD OF FILLING #title IS REQUIRED for the plate to span both
         lines. Modal.vue renders `<slot name="title">` INSIDE its own <DialogTitle>, which is a
         SIBLING of <DialogDescription> - so a plate placed there can only ever be as tall as
         the title line. That is the single fact that cost three measurement rounds on the CCTV
         page; it is written here so it does not have to be rediscovered here.

         The cost of taking the slot over is owning the close button, because #header wraps the
         wrapper, the #actions slot and the <DialogClose> together (Modal.vue lines 100-135).
         Both teardown paths were verified working after the copy on this page too.

         `min-h-0` cancels the vendor's `min-h-(--ui-header-height)` floor - the 4rem lock that
         also made PageHeader title-only on this project. Without it, the 32px of default
         padding eats a 64px box and leaves no room for a two-line block. `items-stretch`
         (NOT items-center) is what makes each item take the full line height; `items-center`
         measures the plate at 33px against a 41px text block.
         `self-stretch` needs a fixed WIDTH and NO height class at all - a `size-*` sets height
         and silently defeats it, and a `grid` parent establishes its own alignment context so
         align-self never reaches the row's cross axis. -->
    <UModal v-model:open="showForm"
            :ui="{
              content: 'sm:max-w-4xl handover-record-modal',
              header: 'relative z-10 flex items-stretch gap-3.5 p-4 min-h-0 sm:px-6',
              body: 'relative z-10 p-4 sm:p-5'
            }"
            :title="editing ? 'Edit Row' : 'New Handover'"
            :description="editing ? 'Update this handover entry.' : 'Record an IT part handed over to a user.'">
      <template #header="{ close }">
        <span
          class="handover-modal-icon flex w-10 shrink-0 items-center justify-center self-stretch
                 rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20 dark:bg-primary/15"
        >
          <!-- Package/box for a part being handed over; the pen variant when editing, matching
               the CCTV dialog's create-vs-edit icon swap. -->
          <UIcon :name="editing ? 'i-lucide-square-pen' : 'i-lucide-package'" class="size-5" />
        </span>

        <!-- Title and subtitle as ONE block: `leading-tight` on the title and a 0 gap before the
             description is what makes the pair read as a single two-line label rather than a
             caption floating under a heading. `min-w-0` lets the text truncate on a narrow
             dialog instead of pushing the close button off-screen. -->
        <span class="min-w-0 flex-1">
          <span class="block text-base font-semibold leading-tight text-default">
            {{ editing ? 'Edit Row' : 'New Handover' }}
          </span>
          <span class="mt-0.5 block text-sm leading-snug text-muted">
            {{ editing ? 'Update this handover entry.' : 'Record an IT part handed over to a user.' }}
          </span>
        </span>

        <!-- The vendor close button, rebuilt. `close()` comes from DialogRoot, so this is the
             same teardown path the vendor button uses, not a hand-rolled state change. -->
        <UButton
          icon="i-lucide-x"
          color="neutral"
          variant="ghost"
          aria-label="Close"
          class="shrink-0"
          @click="close()"
        />
      </template>
      <template #body>
      <!-- `:validate-on="[]"` switches OFF UForm's own validation, so this page can raise ONE
           warning listing every missing field instead of UForm short-circuiting the submit with
           per-field errors. `required` is kept only for the red asterisk. -->
      <UForm :state="form" :validate-on="[]" @submit="save">
        <!-- Sections and a 2-column grid (HIRO, 2026-10-05), copying the CCTV dialog's structure:
             small uppercase dimmed heading with a leading icon, the fields under it, then the
             next section. The section headings are the thing that makes a ten-field form read
             as a record being created rather than a form to fill in.

             TWO columns rather than three on purpose. Three columns on this sheet would leave a
             gap wherever an odd field count is followed by a wide one, and - more importantly -
             Remarks was asked for BESIDE Name, which only works cleanly at two: Name | Remarks
             fills the row exactly, and the pair reads as "who received it, and anything worth
             noting about it", which is how the record is actually used.

             The heading selectors below changed with the structure: the grid's direct children
             are now the three <section> elements rather than the fields, so `> *` alone would
             stagger the sections and nothing else - a silent partial failure, since the
             animation would still be visible. -->
        <div class="handover-record-grid space-y-4">
          <!-- ============ PART ============ -->
          <section>
            <p class="mb-2 flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-dimmed">
              <UIcon name="i-lucide-package" class="size-3.5" />
              Part
            </p>
            <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <!-- Taken Date is prefilled with today and stays editable. Same themed picker as the
                   filter, not the native control: the OS date input cannot be styled to match the
                   app and renders the US mm/dd/yyyy order on this machine, while the sheet is
                   written dd/mm/yyyy. v-model is a bare yyyy-mm-dd, which is exactly the form's
                   shape, and :name keeps it inside UForm's state.

                   HIRO's order (2026-10-05) reads across the row, not down the column:
                     Taken Date | Part Name
                     Brand     | QTY
                   which is also how the printed sheet reads - what it is, then how many. -->
              <UFormField label="Taken Date" name="tanggal_ambil" required>
                <DatePicker v-model="form.tanggal_ambil" name="tanggal_ambil" placeholder="Pick a date"
                            :invalid="fieldInvalid('tanggal_ambil')" />
              </UFormField>
              <UFormField label="Part Name" name="nama_barang" required>
                <UInput v-model="form.nama_barang" name="nama_barang" placeholder="Laptop / monitor / dongle" class="w-full"
                         :ui="fieldInvalid('nama_barang') ? { base: 'ring-2 ring-error' } : undefined" />
              </UFormField>
              <UFormField label="Brand" name="merek">
                <UInput v-model="form.merek" name="merek" placeholder="Dell / HP / Lenovo" class="w-full" />
              </UFormField>
              <UFormField label="QTY" name="qty" required>
                <UInput v-model="form.qty" name="qty" type="number" min="1" placeholder="1" class="w-full"
                         :ui="fieldInvalid('qty') ? { base: 'ring-2 ring-error' } : undefined" />
              </UFormField>
            </div>
          </section>

          <!-- ============ RECIPIENT ============ -->
          <!-- HIRO's order (2026-10-05), reading across each row:
               Employee No | Name
               Section    | Remarks
               The identity pair sits on the first row - the number and the person who owns the
               account belong together - and the org details with them. Name is prefilled from
               the session.

               Remarks is a free-text box rather than an input because condition notes run to a
               sentence or two: "screen has a hairline scratch, charger included" is exactly the
               kind of thing this column exists for, and a single-line input truncates precisely
               that. `w-full` is required as well as the grid cell - UTextarea sizes to its
               content otherwise and ignores the column. -->
          <section>
            <p class="mb-2 flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-dimmed">
              <UIcon name="i-lucide-user-round" class="size-3.5" />
              Recipient
            </p>
            <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <UFormField label="Employee No" name="no_pegawai">
                <UInput v-model="form.no_pegawai" name="no_pegawai" placeholder="940900" class="w-full" />
              </UFormField>
              <UFormField label="Name" name="nama" required>
                <UInput v-model="form.nama" name="nama" placeholder="Recipient's full name" class="w-full"
                         :ui="fieldInvalid('nama') ? { base: 'ring-2 ring-error' } : undefined" />
              </UFormField>
              <UFormField label="Section" name="departemen">
                <UInput v-model="form.departemen" name="departemen" placeholder="ISD / CAP" class="w-full" />
              </UFormField>
              <UFormField label="Remarks" name="catatan">
                <UTextarea v-model="form.catatan" name="catatan" :rows="1" class="w-full"
                          placeholder="Condition, accessories included" />
              </UFormField>
            </div>
          </section>

          <!-- ============ SIGNATURE ============ -->
          <!-- Titled "Signature" to match the CCTV dialog's third section.
               `full-width` is the opt-in prop on SignaturePad: without it the wrapper is
               `inline-block` and the canvas shrink-wraps to about 124px however much room the
               cell actually has.

               CENTRED IN THE FORM (HIRO, 2026-10-05: "make the signature section in the center
               of form"). Two separate things are centred, and they need different mechanisms:

                 - the SECTION HEADING is centred with `justify-center` on its own flex row.
                   That is why the heading's `items-center` stays and a `justify-center` is added
                   rather than the whole heading being centred some other way.
                 - the PAD is centred with `mx-auto` inside a `max-w-2xl` wrapper, so it is
                   centred RELATIVE TO THE DIALOG rather than relative to a grid column. Sizing it
                   to the column and centring with `justify-center` would have been the obvious
                   route and it does not work here: the pad is the only child of its section, so
                   there is no second column to centre it against - `justify-center` alone does
                   nothing to a single item in a full-width cell.

               `max-w-2xl` rather than full width is the deliberate part. A signature box stretched
               to 856px is not a signature box, it is a rule with a squiggle on it; ~670px matches
               the physical width a person actually signs on, and centring it reads as
               intentional. The height stays 130 rather than growing with the width, and the pad
               still lands on the same 2-column row width the CCTV dialog uses. -->
          <section>
            <p class="mb-2 flex items-center justify-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-dimmed">
              <UIcon name="i-lucide-pen-line" class="size-3.5" />
              Signature
            </p>
            <div class="mx-auto w-full max-w-2xl">
              <UFormField label="Recipient Sign" name="tanda" required>
                <SignaturePad v-model="form.tanda" :height="130" full-width
                              :invalid="fieldInvalid('tanda')" />
              </UFormField>
            </div>
          </section>
        </div>

              <div class="mt-4 flex items-center justify-between gap-2 border-t border-default pt-4">
                <!-- Footnote on the LEFT, actions on the right: the legend for the red
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

  <!-- DELETE CONFIRMATION - identical treatment to the CCTV page: the row's identity is
       repeated so the person confirms the row they MEANT, with the same staggered motion. -->
  <UModal
    v-model:open="showDelete"
    :ui="{
      content: 'sm:max-w-md',
      body: 'p-0',
      footer: 'p-0 border-t border-default/70'
    }"
  >
    <template #content>
      <div class="overflow-hidden rounded-xl">
        <div class="relative overflow-hidden px-6 pb-5 pt-6">
          <!-- Soft danger wash bleeding in from the top, clipped by this panel's own
               overflow-hidden. The same trick as the welcome banner's accent orb, sized for a
               dialog and keyed to `error` so it follows the theme. -->
          <div
            aria-hidden="true"
            class="handover-del-wash pointer-events-none absolute -right-16 -top-24 size-48 rounded-full bg-error/20 blur-3xl"
          />

          <div class="relative flex items-start gap-4">
            <span
              class="handover-del-icon grid size-11 shrink-0 place-items-center rounded-full
                     bg-error/10 ring-1 ring-inset ring-error/25 dark:bg-error/15"
            >
              <UIcon name="i-lucide-trash-2" class="size-5 text-error" />
            </span>

            <div class="min-w-0 flex-1">
              <h2 class="handover-del-rise-1 text-base font-semibold text-default">
                Delete this record?
              </h2>
              <p class="handover-del-rise-2 mt-1 text-sm text-muted">
                This permanently removes the entry from the logbook. This action cannot be undone.
              </p>
            </div>
          </div>

          <dl
            v-if="deleteTarget"
            class="handover-del-rise-3 relative mt-5 grid grid-cols-[7.5rem_1fr] items-baseline
                   gap-x-5 gap-y-3 rounded-xl bg-elevated/60 px-5 py-4 ring-1 ring-inset
                   ring-default"
          >
            <template v-for="d in deleteDetails" :key="d.label">
              <dt class="text-xs font-medium uppercase tracking-wide text-dimmed">
                {{ d.label }}
              </dt>
              <!-- line-clamp-2 rather than truncate, so a long part name wraps WITHIN this
                   column and the row keeps the same shape as the others. Full value on title. -->
              <dd
                :title="d.value || undefined"
                class="line-clamp-2 break-words text-sm font-medium text-default"
              >{{ d.value || '-' }}</dd>
            </template>
          </dl>
        </div>

        <div class="flex items-center justify-end gap-2 bg-elevated/40 px-6 py-4">
          <UButton
            label="Cancel"
            color="neutral"
            variant="ghost"
            :disabled="deleting"
            @click="showDelete = false"
          />
          <UButton
            label="Delete record"
            icon="i-lucide-trash-2"
            color="error"
            :loading="deleting"
            @click="confirmDelete"
          />
        </div>
      </div>
    </template>
  </UModal>
</template>

<!-- NOT scoped, and that is load-bearing rather than stylistic: UModal teleports to <body>,
     so a scoped rule could never reach the dialog's own content element. Every rule below is
     addressed through a marker class passed in via :ui / class, so none of it can leak onto
     another page's dialog. These are deliberately the same values as the CCTV page's, so the
     two logbooks feel like one application.

     NOTE: `.chips-slot`, `.row-*`, `.logbook-scroll` and the print rules are the SAME class
     names the CCTV page uses, with the same declarations. They are intentionally duplicated
     rather than shared: both pages keep their own copy so neither can be broken by an edit to
     the other, and the values are asserted identical by review. The page-specific rules
     (modal, grid, panel body, delete dialog) all carry a `handover-` prefix so they can only
     ever affect this page. -->
<style>
/* ---------------------------------------------------------------------------------------
   Collapsible filter-chip slot. Animating `grid-template-rows` 0fr -> 1fr is the only way to
   transition to an unknown content height in pure CSS: the track interpolates, so the table
   underneath is pushed down progressively instead of being shoved a whole line in one frame.
   `min-height: 0` on the clip is what makes the 0fr track actually collapse to nothing. */
.chips-slot {
  display: grid;
  grid-template-rows: 0fr;
  transition: grid-template-rows 220ms cubic-bezier(0.22, 1, 0.36, 1);
}
.chips-slot.is-open {
  grid-template-rows: 1fr;
}
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
   Row transitions (filter / search changes).
     - `transform` only, never a property that triggers layout. Animating width/height/table
       cells re-rasterises, which is the mechanism behind the sidebar "ghost text" bug.
     - NO leave transition. A <tr> animating out still occupies its slot until the transition
       ends, and taking it out of flow (position:absolute) mid-animation drops the borders
       and column alignment for the whole table. Removing it instantly while the survivors
       slide to their new positions reads cleanly and is far cheaper.
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

@media print {
  .row-enter-active,
  .row-move,
  .row-move-active {
    transition: none !important;
    opacity: 1 !important;
    transform: none !important;
  }
}

/* The dark-mode signature preview works by flipping the black ink to white with
   `filter: invert(1)`. On paper the background is white, so white ink would be INVISIBLE -
   and a blank signature column on a signed register is exactly the failure this register
   exists to prevent. Print must always use the original black ink. */
@media print {
  tbody img[alt="signature"] {
    filter: none !important;
  }
}

@media (prefers-reduced-motion: reduce) {
  .row-enter-active,
  .row-move,
  .row-move-active {
    transition: none !important;
    opacity: 1 !important;
    transform: none !important;
  }
}

/* A4 landscape: nine data columns plus Actions will not read in portrait. */
@media print {
  @page { size: A4 landscape; margin: 8mm; }
  body { background: #fff !important; }
  .print\:hidden { display: none !important; }

  /* The screen table lives inside a max-h scroll container with a sticky header; on paper
     both must go, or the sheet is clipped to one viewport-height and repeats the header. */
  .print\:scroll-area { max-height: none !important; overflow: visible !important; }
  .print\:sticky-head { position: static !important; }

  /* On screen the grid is bottom-rules only (border-b) for a cleaner look; the printed sheet
     needs a full grid so every cell is a distinct box. */
  table { min-width: 0 !important; width: 100% !important; }
  table, td, th { border-color: #000 !important; }
  th, td { border: 1px solid #000 !important; }
  tr { page-break-inside: avoid; }
}

/* ============================================================================================
   Smooth reveal for the New/Edit Handover dialog - the CCTV motion, verbatim.

   MEASURED FIRST on the CCTV dialog, because the obvious implementation is wrong here. The
   vendor centres the dialog with `left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2`, and
   Tailwind v4 emits those as the STANDALONE `translate` property (computed: `translate: -50%
   -50%` with `transform: none`). Animating `transform` therefore COMPOSES with `translate`
   instead of overwriting it, so the dialog stays centred on every frame. Had the centring
   been the older transform-based form, these keyframes would have flung it into the corner.

   TRANSLATE + OPACITY ONLY, never scale: a scale re-rasterises text mid-animation, which is
   the mechanism behind the sidebar ghost-text bug. The bounce comes from the easing curve.
   Position and fade are SEPARATE animations with different curves AND different durations,
   so the fade completes while the slide is still decelerating; both curves keep every y
   control point at or below 1, which makes the motion monotonic - it settles instead of
   wobbling past the target.

   The selector carries THREE components (specificity 0,3,0) so it beats the vendor utility
   `data-[state=open]:animate-[scale-in...]` at (0,2,0) WITHOUT disabling the vendor
   transition, which would also cost its focus-trap timing.
   ============================================================================================ */
@keyframes handover-modal-reveal-pos {
  from { transform: translateY(14px); }
  to   { transform: translateY(0); }
}

@keyframes handover-modal-reveal-fade {
  from { opacity: 0; }
  to   { opacity: 1; }
}

/* Exiting ACCELERATES away rather than easing: cubic-bezier(0.4, 0, 1, 1) has a rising slope,
   so the dialog leaves decisively instead of lingering on screen. */
@keyframes handover-modal-dismiss-pos {
  from { transform: translateY(0); }
  to   { transform: translateY(8px); }
}

@keyframes handover-modal-dismiss-fade {
  from { opacity: 1; }
  to   { opacity: 0; }
}

.handover-record-modal[data-slot='content'][data-state='open'] {
  animation:
    handover-modal-reveal-pos 440ms cubic-bezier(0.3, 0, 0.2, 1) both,
    handover-modal-reveal-fade 240ms cubic-bezier(0.4, 0, 0.2, 1) both;
}

.handover-record-modal[data-slot='content'][data-state='closed'] {
  animation:
    handover-modal-dismiss-pos 200ms cubic-bezier(0.4, 0, 1, 1) both,
    handover-modal-dismiss-fade 160ms cubic-bezier(0.4, 0, 1, 1) both;
}

/* ------------------------------------------------------------------------------------------
   Accent glow bleeding in from the top-right corner, matching both other dialogs.

   Measured against the WelcomeBanner reference: this is NOT a CSS gradient, it is a plain
   circle of the accent colour with an enormous blur, so the "gradient" is nothing but the
   blur falloff. `var(--ui-primary)` is a semantic token, so the glow follows whichever accent
   the user picked - switching to violet retints it with no extra code, and a hard-coded colour
   would have gone stale the moment the accent changed.

     - `::after` rather than a real element, because the #content slot is documented as having
       broken the form's submit path, and the content element is already `overflow: hidden`,
       which is exactly the clipping that makes the orb read as light coming in from the corner.
     - `pointer-events: none`, because the circle sits over the header and would otherwise
       swallow clicks on the close button.
     - z-0, with `header: 'relative z-10'` and `body: 'relative z-10'` in the :ui prop.
   ------------------------------------------------------------------------------------------ */
.handover-record-modal[data-slot='content']::after {
  content: '';
  position: absolute;
  top: -130px;
  right: -110px;
  width: 300px;
  height: 300px;
  border-radius: 9999px;
  background-color: var(--ui-primary);
  opacity: 0.13;
  filter: blur(70px);
  pointer-events: none;
  z-index: 0;
}

/* Light mode needs a lighter touch: a value that is barely perceptible on the dark navy
   surface turns into a visible stain on a near-white one. */
:root:not(.dark) .handover-record-modal[data-slot='content']::after {
  opacity: 0.09;
}

/* The icon plate in the dialog title settles a beat AFTER the dialog starts moving.
   Without a delay it popped into place at full opacity on frame 1 while the dialog was still
   sliding in, which read as a separate, later event rather than part of the same arrival.
   Opacity + a small translate only - never scale (a scaled glyph rasterises at the intermediate
   size and reads soft). The curve is monotonic so the plate lands exactly where it belongs. */
@keyframes handover-title-icon-in {
  from { opacity: 0; transform: translateY(-6px); }
  to   { opacity: 1; transform: translateY(0); }
}

.handover-modal-icon {
  animation: handover-title-icon-in 320ms cubic-bezier(0.22, 1, 0.36, 1) 40ms both;
}

/* Staggered reveal of the form fields, so they arrive in reading order instead of the whole
   form arriving as one slab. `both` keeps each field at its from-state while it waits its turn,
   which is what makes the stagger read as a sequence rather than a flash.
   The selector targets the SECTIONS and the fields inside them: the grid's direct children are
   the three <section> elements, so `> *` alone would stagger the sections and nothing else -
   a silent partial failure, because the animation would still be visible.
   One delay per SECTION, and the signature pad last, because it is the point of the form. */
@keyframes handover-row-reveal-pos {
  from { transform: translateY(8px); }
  to   { transform: translateY(0); }
}

@keyframes handover-row-reveal-fade {
  from { opacity: 0; }
  to   { opacity: 1; }
}

.handover-record-grid > *,
.handover-record-grid > section > div > * {
  animation:
    handover-row-reveal-pos 320ms cubic-bezier(0.3, 0, 0.2, 1) both,
    handover-row-reveal-fade 210ms cubic-bezier(0.4, 0, 0.35, 1) both;
}

.handover-record-grid > *:nth-child(1),
.handover-record-grid > *:nth-child(1) > div > * { animation-delay: 0ms; }
.handover-record-grid > *:nth-child(2),
.handover-record-grid > *:nth-child(2) > div > * { animation-delay: 70ms; }
.handover-record-grid > *:nth-child(3),
.handover-record-grid > *:nth-child(3) > div > * { animation-delay: 140ms; }

@media (prefers-reduced-motion: reduce) {
  .handover-record-modal[data-slot='content'][data-state],
  .handover-record-grid > *,
  .handover-record-grid > section > div > *,
  .handover-modal-icon {
    animation: none !important;
    opacity: 1 !important;
    transform: none !important;
  }
}

@media print {
  .handover-record-modal[data-slot='content'][data-state],
  .handover-record-grid > *,
  .handover-record-grid > section > div > *,
  .handover-modal-icon {
    animation: none !important;
  }
}

/* ============================================================================================
   Kill the scrollbar flicker when a filter changes the row count.

   The table lives in `max-h-[70vh]`. Whenever filtering shrinks the rows below that height the
   inner scrollbar disappears, the container becomes 15px WIDER, and because the columns are
   `table-fixed` with percentage widths they redistribute - so the whole grid slides sideways in
   a single frame. `scrollbar-gutter: stable` reserves the gutter permanently, and
   `overflow-y: scroll` (applied as a Tailwind utility in the class attribute, not as a
   declaration here - a class selector would tie with Tailwind's `.overflow-auto` on
   specificity and lose on source order) keeps the track permanently rendered so the thumb
   cannot blink in and out.
   ============================================================================================ */
.logbook-scroll {
  scrollbar-gutter: stable;
}

/* The thumb must stay VISIBLE, not merely present. 12px track with a 2px border gives an 8px
   thumb: quiet, but unmistakably a scrollbar. */
.logbook-scroll::-webkit-scrollbar {
  width: 12px;
  height: 12px;
}

/* background-COLOR, not the `background` shorthand: the shorthand resets background-clip back
   to its initial value and undoes the rounded-pill thumb below. */
.logbook-scroll::-webkit-scrollbar-track {
  background-color: color-mix(in oklab, var(--ui-text-dimmed) 10%, transparent);
}

.logbook-scroll::-webkit-scrollbar-thumb {
  background-color: var(--ui-text-dimmed);
  border-radius: 9999px;
  border: 2px solid transparent;
  background-clip: content-box;
}

.logbook-scroll::-webkit-scrollbar-thumb:hover {
  background-color: var(--ui-text-muted);
  background-clip: content-box;
}

/* Dark mode needs a firmer thumb and a slightly brighter rail than light mode, so the two are
   set separately instead of hoping one token works on both surfaces. */
.dark .logbook-scroll::-webkit-scrollbar-track {
  background-color: color-mix(in oklab, var(--ui-text-dimmed) 14%, transparent);
}

.dark .logbook-scroll::-webkit-scrollbar-thumb {
  background-color: var(--ui-text-dimmed);
}

.dark .logbook-scroll::-webkit-scrollbar-thumb:hover {
  background-color: var(--ui-text-highlighted);
}

/* Firefox honours the standard properties and ignores the ::-webkit rules entirely. */
.logbook-scroll {
  scrollbar-width: thin;
  scrollbar-color: var(--ui-text-dimmed) transparent;
}

.dark .logbook-scroll {
  scrollbar-color: var(--ui-text-muted) transparent;
}

/* -------------------------------------------------------------------------------------------
   HIDE the page scrollbar on the handover panel body - keep the scrolling.

   HIRO asked to hide it, NOT remove it. `overflow-y-scroll` stays exactly as it is: the panel
   body remains a real scrollable region and wheel, keyboard and trackpad all keep working.
   Only the scrollbar's APPEARANCE is suppressed, by giving it zero width - which also
   strengthens the flicker fix, because a zero-width scrollbar occupies no layout space at all,
   so the content width can never change.

   Scoped to .handover-panel-body, which exists only on this page's own UDashboardPanel.
   ------------------------------------------------------------------------------------------- */
.handover-panel-body {
  scrollbar-width: none;
  -ms-overflow-style: none;
}

.handover-panel-body::-webkit-scrollbar {
  width: 0;
  height: 0;
}

.handover-panel-body::-webkit-scrollbar-track {
  background: transparent;
}

.handover-panel-body::-webkit-scrollbar-thumb {
  background: transparent;
}

/* -------------------------------------------------------------------------------------------
   DELETE-CONFIRMATION MOTION

   Plain CSS keyframe animations rather than <Transition> wrappers, because the elements are
   always present once UModal mounts its content - a Vue transition needs a v-if/appear to fire,
   and adding v-ifs purely for timing would put the row details on a separate render path from
   the rest of the dialog.

   Every curve is MONOTONIC. The app-wide rule about avoiding scale is about TEXT: a scaled
   glyph rasterises at the intermediate size and reads soft, and a non-monotonic curve can drag
   the final frame away from the settled state. Neither applies to the 44px icon disc, so that
   one does scale - on a monotonic curve, so it still lands exactly where it belongs.

   `transform` here is safe even though the dialog is centred: Tailwind v4 emits the centring
   as the standalone `translate` property, not `transform`, so animating `transform` on a child
   cannot compose with - or fight - the parent's centring.
   ------------------------------------------------------------------------------------------- */
@keyframes handover-del-icon-in {
  from { opacity: 0; transform: scale(0.86); }
  to   { opacity: 1; transform: scale(1); }
}

@keyframes handover-del-rise {
  from { opacity: 0; transform: translateY(9px); }
  to   { opacity: 1; transform: translateY(0); }
}

/* Slow breath on the danger wash so the dialog reads as "destructive" before a word is read. */
@keyframes handover-del-wash-breathe {
  0%, 100% { opacity: 0.75; transform: scale(1); }
  50%      { opacity: 1;    transform: scale(1.08); }
}

.handover-del-icon {
  animation: handover-del-icon-in 300ms cubic-bezier(0.22, 1, 0.36, 1) both;
}

.handover-del-rise-1 { animation: handover-del-rise 360ms cubic-bezier(0.22, 1, 0.36, 1) 60ms both; }
.handover-del-rise-2 { animation: handover-del-rise 360ms cubic-bezier(0.22, 1, 0.36, 1) 110ms both; }
.handover-del-rise-3 { animation: handover-del-rise 420ms cubic-bezier(0.22, 1, 0.36, 1) 160ms both; }

.handover-del-wash {
  animation: handover-del-wash-breathe 5.5s cubic-bezier(0.4, 0, 0.6, 1) 420ms both;
}

@media (prefers-reduced-motion: reduce) {
  .handover-del-icon,
  .handover-del-rise-1,
  .handover-del-rise-2,
  .handover-del-rise-3,
  .handover-del-wash {
    animation: none !important;
  }
}
</style>