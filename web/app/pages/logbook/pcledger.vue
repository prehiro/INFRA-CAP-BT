<script setup lang="ts">
/**
 * PC Ledger — the app's replacement for the manual Excel sheet (IT FORM SG031, sheet "Ledger").
 *
 * COLUMN SET AND ORDER ARE NOT A DESIGN CHOICE. They are the reference workbook's 16 columns,
 * in the reference's order, because the whole point of the page is that its export can be
 * handed back to the department as the workbook they already use. The entity in the API
 * (`pc_ledger`, id 10) was created to mirror it field for field.
 *
 * The one addition is `department`: the workbook carries a single "Department:" cell above the
 * table, which cannot describe a per-row value, so it lives on the record instead — it filters
 * on screen and is written back into that cell on export.
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
type Column = { key: string; label: string; w: number; align?: 'center' }

const COLUMNS: Column[] = [
  { key: 'nomor', label: 'No', w: 5, align: 'center' },
  { key: 'staff_name', label: 'Staff Name', w: 20 },
  { key: 'email', label: 'Email Address', w: 26 },
  { key: 'gid', label: 'GID', w: 12 },
  { key: 'japan_hostname', label: 'JAPAN Hostname', w: 16 },
  { key: 'computer_model', label: 'Computer Model', w: 16 },
  { key: 'computer_sn', label: 'Computer S/N', w: 14 },
  { key: 'tanggal', label: 'Date', w: 12, align: 'center' },
  { key: 'chassis', label: 'Computer Chassis', w: 15 },
  { key: 'manufacturer', label: 'Computer Manufacturer', w: 19 },
  { key: 'os_name', label: 'Computer O/S Name', w: 20 },
  { key: 'os_arch', label: 'Computer O/S Architecture', w: 17 },
  { key: 'lokasi', label: 'Location', w: 28 },
  { key: 'remark2', label: 'Remark2', w: 26 },
  { key: 'remark3', label: 'Remark3', w: 18 }
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
const STORAGE_KEY = 'infra-cap.pcledger.columns'
const visibleKeys = ref<string[]>(COLUMNS.map((c) => c.key))

const visibleColumns = computed(() => COLUMNS.filter((c) => visibleKeys.value.includes(c.key)))

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
    // A corrupt value must never brick the page - fall back to showing every column.
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
 * The first two VISIBLE columns stay pinned while the rest scroll sideways under them.
 * A class rather than `nth-child`, and the offset is computed rather than hard-coded: hiding the
 * No column moves Staff Name into the first slot, and with a fixed `left: 5rem` on the second
 * slot it would have been pushed 5rem into the table and left a visible hole.
 */
function pinClass(i: number) {
  if (i === 0) return 'pl-pin pl-pin-first'
  if (i === 1) return 'pl-pin pl-pin-second'
  return ''
}

function pinStyle(i: number) {
  if (i !== 1) return undefined
  const first = visibleColumns.value[0]
  return first ? { left: `${first.w}rem` } : undefined
}

/** Kept in sync with COLUMNS by key; the modal groups the fields into three sections. */
const FORM_SECTIONS = [
  {
    title: 'Identity',
    icon: 'i-lucide-user-round',
    fields: ['staff_name', 'email', 'gid', 'departemen']
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
  remark2: 'Remark2',
  remark3: 'Remark3',
  departemen: 'Department'
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
const filterDept = ref(ALL)
const filterChassis = ref(ALL)
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
  if (filterDept.value !== ALL) out = out.filter((r) => cell(r, 'departemen') === filterDept.value)
  if (filterChassis.value !== ALL) out = out.filter((r) => cell(r, 'chassis') === filterChassis.value)

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

const departments = computed(() =>
  [...new Set(rows.value.map((r) => cell(r, 'departemen')).filter(Boolean))].sort()
)
const chassisTypes = computed(() =>
  [...new Set(rows.value.map((r) => cell(r, 'chassis')).filter(Boolean))].sort()
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
  form.departemen = departments.value[0] ?? 'Capacitor'
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
    const suffix = (searching.value || filterDept.value !== ALL || filterChassis.value !== ALL) ? '_filtered' : ''
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
                v-model="filterDept"
                :items="[{ label: 'All departments', value: ALL }, ...departments.map(d => ({ label: d, value: d }))]"
                class="w-44"
                :ui="{ base: 'h-9' }"
              />

              <USelect
                v-model="filterChassis"
                :items="[{ label: 'All chassis', value: ALL }, ...chassisTypes.map(d => ({ label: d, value: d }))]"
                class="w-40"
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
        <UCard class="anim-fade-up" :ui="{ body: 'p-0 sm:p-0' }">
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
            <table class="pl-table">
              <colgroup>
                <col v-for="c in visibleColumns" :key="c.key" :style="{ width: c.w + 'rem' }" />
                <col style="width: 5rem" />
              </colgroup>
              <thead>
                <tr>
                  <th
                    v-for="(c, ci) in visibleColumns"
                    :key="c.key"
                    :style="pinStyle(ci)"
                    :class="[
                      c.align === 'center' ? 'text-center' : 'text-left',
                      sortKey === c.key ? 'is-sorted' : '',
                      pinClass(ci)
                    ]"
                    @click="toggleSort(c.key)"
                  >
                    <span class="inline-flex items-center gap-1">
                      {{ c.label }}
                      <UIcon
                        v-if="sortKey === c.key"
                        :name="sortDir === 'asc' ? 'i-lucide-arrow-up' : 'i-lucide-arrow-down'"
                        class="size-3"
                      />
                    </span>
                  </th>
                  <th class="text-right">Actions</th>
                </tr>
              </thead>
              <!-- Row transitions, the same mechanism the CCTV register uses: rows that survive a
                   filter change slide to their new position (FLIP move) instead of teleporting,
                   and rows that newly match fade in from slightly above. -->
              <TransitionGroup
                tag="tbody"
                enter-active-class="row-enter-active"
                enter-from-class="row-enter-from"
                leave-active-class="row-leave-active"
                leave-to-class="row-leave-to"
                move-class="row-move"
                move-active-class="row-move-active"
              >
                <tr v-for="row in visibleRows" :key="row.id">
                  <td
                    v-for="(c, ci) in visibleColumns"
                    :key="c.key"
                    :style="pinStyle(ci)"
                    :class="[
                      c.align === 'center' ? 'text-center' : '',
                      c.key === 'nomor' ? 'font-medium' : '',
                      'tabular-nums',
                      pinClass(ci)
                    ]"
                    :title="cell(row, c.key)"
                  >
                    <span class="pl-ellipsis">{{ c.key === 'tanggal' ? fmtDate(row.values?.[c.key]) : cell(row, c.key) }}</span>
                  </td>
                  <td class="text-right">
                    <div class="inline-flex items-center gap-1">
                      <UButton color="neutral" variant="ghost" icon="i-lucide-pencil" size="xs" aria-label="Edit" @click="openEdit(row)" />
                      <UButton color="error" variant="ghost" icon="i-lucide-trash-2" size="xs" aria-label="Delete" @click="askDelete(row)" />
                    </div>
                  </td>
                </tr>
              </TransitionGroup>
            </table>
          </div>

          <template v-if="!loading && rows.length" #footer>
            <!-- Matched to the CCTV register's footer deliberately rather than approximately:
                 same classes (border-t, bg-default/30, px-4 py-2.5), and the LEFT count follows
                 the FILTERS so it can never disagree with the "Showing X of Y" next to it. The
                 CCTV page once printed the raw total there and read "1 row / Showing 0 of 1",
                 which looks like a bug. -->
            <div class="flex items-center justify-between gap-3 border-t border-default bg-default/30 px-4 py-2.5 text-xs text-muted">
              <span>{{ visibleRows.length }} {{ visibleRows.length === 1 ? 'record' : 'records' }}</span>
              <span class="tabular-nums">Showing {{ visibleRows.length }} of {{ rows.length }}</span>
            </div>
          </template>
        </UCard>
      </div>

      <!-- ---------------- add / edit ---------------- -->
      <UModal v-model:open="showForm" :ui="{ content: 'max-w-4xl' }">
        <template #content>
          <div class="flex max-h-[85vh] flex-col">
            <div class="flex items-center gap-3 border-b border-default px-5 py-4">
              <span class="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary ring-1 ring-inset ring-primary/20">
                <UIcon :name="editingId ? 'i-lucide-pencil' : 'i-lucide-plus'" class="size-5" />
              </span>
              <div class="min-w-0">
                <h2 class="text-sm font-semibold leading-tight">{{ editingId ? 'Edit PC record' : 'New PC record' }}</h2>
                <p class="text-xs text-muted">
                  {{ editingId ? 'Changes are written to the ledger immediately.' : 'No is assigned automatically by the system.' }}
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
                      :placeholder="key === 'departemen' ? 'Capacitor' : ''"
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
          <div class="p-5">
            <div class="flex items-start gap-3">
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
            <dl v-if="deleteTarget" class="mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 rounded-lg border border-default bg-elevated p-3 text-xs">
              <dt class="text-muted">No</dt>
              <dd class="truncate font-medium">{{ cell(deleteTarget, 'nomor') }}</dd>
              <dt class="text-muted">Staff Name</dt>
              <dd class="truncate">{{ cell(deleteTarget, 'staff_name') || '-' }}</dd>
              <dt class="text-muted">JAPAN Hostname</dt>
              <dd class="truncate tabular-nums">{{ cell(deleteTarget, 'japan_hostname') || '-' }}</dd>
              <dt class="text-muted">Location</dt>
              <dd class="truncate">{{ cell(deleteTarget, 'lokasi') || '-' }}</dd>
            </dl>

            <div class="mt-5 flex items-center justify-end gap-2">
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
  max-height: 70vh;
  overflow: auto;
  scrollbar-gutter: stable;
}

.pl-table {
  /* fixed, not auto: with auto layout the browser treats a declared width as a hint and
     redistributes the leftover unevenly, so two columns that declare the same width end up
     different at a wider viewport. */
  table-layout: fixed;
  min-width: max-content;
  width: 100%;
  border-collapse: separate;
  border-spacing: 0;
  font-size: 0.8125rem;
}

.pl-table th {
  position: sticky;
  top: 0;
  z-index: 2;
  background: var(--ui-bg-elevated);
  border-bottom: 1px solid var(--ui-border);
  padding: 0.5rem 0.75rem;
  font-size: 0.6875rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--ui-text-muted);
  white-space: nowrap;
  cursor: pointer;
  user-select: none;
}

.pl-table th.is-sorted {
  color: var(--ui-primary);
}

.pl-table td {
  padding: 0.5rem 0.75rem;
  border-bottom: 1px solid var(--ui-border);
  color: var(--ui-text);
  /* Equal-width digits in the app's own font, so codes and numbers line up column-wise. */
  font-variant-numeric: tabular-nums;
}

.pl-table tbody tr:hover td {
  background: color-mix(in oklab, var(--ui-primary) 6%, transparent);
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
  background: var(--ui-bg-elevated);
}

.pl-pin-first {
  left: 0;
}

.pl-pin-second {
  box-shadow: 1px 0 0 var(--ui-border);
}

.pl-table th.pl-pin {
  z-index: 4;
}

/* A pinned cell must stay OPAQUE. A translucent hover would let the columns scrolling underneath
   show through it, which is the classic broken sticky-column look - so the hover tint is mixed
   into the opaque surface rather than layered over it. */
.pl-table tbody tr:hover td.pl-pin {
  background: color-mix(in oklab, var(--ui-primary) 6%, var(--ui-bg-elevated));
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

   A LEAVE ANIMATION IS ADDED HERE, unlike CCTV. That page removes filtered-out rows instantly by
   choice, and its comment explains why a <tr> cannot be faded out without either holding the
   table's height open or pulling the row out of flow. With 26 rows on screen and a filter that
   keeps eight, the instant version is a hard cut, so this page pays the price: the leaving row
   fades AND gives its padding back while it goes, which hands most of its height to the rows
   below before it is dropped. */
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

/* FLIP: Vue measures the row before and after, so only the transform needs animating.
   THE CURVE OVERSHOOTS ON PURPOSE - a small bounce as the rows land on each other after a
   filter, which is what HIRO asked for ("add bounce effect when column collide after filter").
   cubic-bezier(0.34, 1.56, 0.64, 1) is the classic ease-out-back: the row travels a little past
   its final line and settles back into it.
   WHY THIS IS SAFE despite the app's monotonic-curve rule, which exists because a non-monotonic
   curve can leave an element off its settled value: a CSS transition ALWAYS ends exactly on its
   target, and the target here is `transform: none`, so the row lands precisely on its grid line.
   The overshoot only exists mid-flight. The rule was also written for scaled TEXT (a glyph
   rasterises soft at an in-between size); this moves rows, it does not scale anything.
   340ms rather than the CCTV register's 220ms: on a 43px row pitch a shorter overshoot is not
   perceptible, it just reads as a slightly late landing. */
.row-move-active,
.row-move {
  transition: transform 340ms cubic-bezier(0.34, 1.56, 0.64, 1);
}

/* Leaving rows fade AND their cell padding collapses.

   MEASURED, and it corrects an earlier claim of mine: the padding collapse works (a leaving row
   went 43px -> 27px, so 16px of its height goes back to the rows below it while they are still
   being removed), but collapsing the CONTENT box did NOT. A `max-height: 0` on the cell contents
   was in this file and had no effect at all - the row still measured 27px at removal - so those
   rules are deleted rather than left behind pretending to do something.

   The residual jump is real and is exactly what the CCTV register's comment warns about: with 26
   rows on screen and a filter that keeps eight, eighteen rows are dropped in one patch, and the
   rows below take one large step (~477px measured) as they go. Fixing that properly means taking
   the leaving rows OUT OF FLOW (absolute positioning plus a spacer), which trades a jump for a
   table that cannot keep its own column widths. Flagged to HIRO with the numbers instead of
   papered over. */
.row-leave-active {
  transition: opacity 260ms cubic-bezier(0.4, 0, 1, 1);
}

.row-leave-active td {
  padding-top: 0 !important;
  padding-bottom: 0 !important;
  transition: padding 240ms cubic-bezier(0.22, 1, 0.36, 1);
}

.row-leave-to {
  opacity: 0;
}

.pl-ellipsis {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (prefers-reduced-motion: reduce) {
  /* Cancel outright rather than shorten - a partial fade is still motion, which is exactly what
     the OS setting asks us to avoid. Same rule as main.css and as the CCTV register. */
  .row-enter-active,
  .row-leave-active,
  .row-move,
  .row-move-active {
    transition: none !important;
    opacity: 1 !important;
    transform: none !important;
  }
}
</style>
