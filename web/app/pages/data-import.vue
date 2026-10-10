<script setup lang="ts">
import type { NavigationMenuItem } from '@nuxt/ui'
import ExcelJS from 'exceljs'

/**
 * Data Import - admin-only surface for getting bulk data INTO the registers.
 *
 * WHY A DEDICATED PAGE with tabs, and not a button on the PC Ledger page: importing is a heavy,
 * occasional, admin task with its own failure modes, while the register itself is what everyone
 * uses every day. Keeping it out of the register means a failed import cannot leave that page in a
 * half-loaded state, and it gives the task room for a preview before anything is written.
 *
 * THE TAB BAR IS THE TEMPLATE'S OWN PATTERN - `PageHeader` (which carries the sidebar collapse in
 * the navbar's #leading slot, on every page of this app) followed by a `UDashboardToolbar` holding
 * a horizontal `UNavigationMenu` with `highlight`, copied from the dashboard template's settings
 * page. HIRO pointed at exactly that structure: the element
 * `div.shrink-0.flex.items-center.justify-between.border-b.border-default.px-4.sm:px-6.gap-1.5
 * .overflow-x-auto.min-h-[49px] > nav`. Using the vendor components means the bar height, border,
 * padding and active indicator are the same as every other page rather than a look-alike.
 *
 * TABS LIVE IN THE QUERY STRING (`/data-import?tab=gid`), which is how the menu can mark one item
 * active with `exact` matching without introducing child routes and a second panel.
 */

const route = useRoute()
const router = useRouter()
const toast = useToast()

const TABS = [
  { key: 'import', label: 'Import PC Ledger', icon: 'i-lucide-file-up' },
  { key: 'gid', label: 'GID List', icon: 'i-lucide-list' }
] as const

const activeTab = computed(() => (route.query.tab === 'gid' ? 'gid' : 'import'))

// The page's entrance animation lives on the panel BODY element, via :ui on UDashboardPanel below.
// That element is created once and is never re-created by the tab swap, so the fade can run only on
// the first paint. It used to sit on the two per-tab wrappers, which are swapped with v-if, so a tab
// that flipped just after mount could mount a fresh wrapper and replay the 340ms fade - HIRO saw the
// page flicker and grow/shrink when switching tabs, and the fade was measured running twice on some
// loads even after the wrapper was gated with a timer. An element that is never re-created cannot replay.

const links = computed<NavigationMenuItem[][]>(() => [
  TABS.map((t) => ({
    label: t.label,
    icon: t.icon,
    to: { path: '/data-import', query: { tab: t.key } },
    // `active` is set EXPLICITLY, and that is the whole point. The vendor marks an item by matching
    // its `to` against the route, and it compares the PATH only - the two tabs share a path and
    // differ by query, so BOTH were marked active and the accent line never moved (measured: both
    // items carried the primary colour before and after switching). Setting `active` from the query
    // takes the decision away from the route matcher entirely.
    active: activeTab.value === t.key,
    exact: true
  }))
])

/* ==========================================================================================
   SHARED: reading the workbook in the browser, before anything is written.
   ========================================================================================== */

function cellText(v: any): string {
  if (v === null || v === undefined) return ''
  if (typeof v === 'object') {
    // ExcelJS cells carry rich text, a formula result or a hyperlink depending on the file.
    if (Array.isArray(v.richText)) return v.richText.map((r: any) => r.text ?? '').join('')
    if (v.text !== undefined) return String(v.text)
    if (v.result !== undefined) return String(v.result)
    if (v instanceof Date) return v.toISOString().slice(0, 10)
    return ''
  }
  if (v instanceof Date) return v.toISOString().slice(0, 10)
  return String(v)
}

async function readWorkbook(file: File) {
  const wb = new ExcelJS.Workbook()
  await wb.xlsx.load(await file.arrayBuffer())
  const ws = wb.worksheets[0]
  if (!ws) throw new Error('The workbook has no sheet.')
  return ws
}

/* ==========================================================================================
   TAB 1 - IMPORT PC LEDGER

   The file is the register's own workbook (IT FORM SG031), and its layout is FIXED - HIRO: "data
   yang anda insert ke tabel halaman PC ledger dimulai dari row B11, urutan kolom nya sudah fix
   sama 15 kolom jadi tinggal insert saja". So there is no header sniffing: rows are read from 11
   and the fifteen columns B..P are mapped by position.

   B is the sheet's own No. It is read but NOT sent: the API's LogbookNumberService assigns the
   register's number, and `nomor` is required+unique, so sending the sheet's value could collide.
   ========================================================================================== */

/** Column B to P, in the sheet's fixed order. `key: null` = read the cell, do not store it. */
const PC_LEDGER_COLUMNS: { col: number; key: string | null; label: string }[] = [
  { col: 2, key: null, label: 'No' },
  { col: 3, key: 'staff_name', label: 'Staff Name' },
  { col: 4, key: 'email', label: 'Email Address' },
  { col: 5, key: 'gid', label: 'GID' },
  { col: 6, key: 'japan_hostname', label: 'JAPAN Hostname' },
  { col: 7, key: 'computer_model', label: 'Computer Model' },
  { col: 8, key: 'computer_sn', label: 'Computer S/N' },
  { col: 9, key: 'tanggal', label: 'Date' },
  { col: 10, key: 'chassis', label: 'Computer Chassis' },
  { col: 11, key: 'manufacturer', label: 'Computer Manufacturer' },
  { col: 12, key: 'os_name', label: 'Computer O/S Name' },
  { col: 13, key: 'os_arch', label: 'Computer O/S Architecture' },
  { col: 14, key: 'lokasi', label: 'Location' },
  { col: 15, key: 'remark2', label: 'Remark1' },
  { col: 16, key: 'remark3', label: 'Remark2' }
]
const PC_LEDGER_FIRST_ROW = 11
const PC_LEDGER_SLUG = 'pc_ledger'

type PcParsed = {
  fileName: string
  sheetName: string
  rows: Record<string, any>[]
  skipped: number
  lastRow: number
  datesSkipped: number
}

const parsing = ref(false)
const parsed = ref<PcParsed | null>(null)
const parseError = ref('')
const importing = ref(false)
const importResult = ref<{ ok: number; failed: number; firstError: string } | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)

/**
 * Dates in the register workbook are not born equal: some cells are real dates, some are Excel
 * serial numbers, and a few hold text that is not a date at all (the reference file has cells
 * reading "20"). Anything that cannot be turned into a date is DROPPED rather than sent, because
 * the API rejects an unparseable Date field and would fail the whole row - three of the reference
 * file's twenty-six rows were lost that way before this existed. The count is reported instead.
 */
function normaliseDate(value: any, raw: string): string | null {
  if (value instanceof Date) return value.toISOString().slice(0, 10)
  const n = Number(raw)
  if (Number.isFinite(n) && n > 20000 && n < 60000) {
    // Excel serial day count (1900 date system) -> Unix epoch.
    const d = new Date(Math.round((n - 25569) * 86400000))
    return isNaN(d.getTime()) ? null : d.toISOString().slice(0, 10)
  }
  const iso = raw.match(/^(\d{4})-(\d{2})-(\d{2})/)
  if (iso) return `${iso[1]}-${iso[2]}-${iso[3]}`
  const d = new Date(raw)
  return isNaN(d.getTime()) ? null : d.toISOString().slice(0, 10)
}

function onPcFile(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  parsing.value = true
  parseError.value = ''
  parsed.value = null
  importResult.value = null
  ;(async () => {
    try {
      const ws = await readWorkbook(file)
      const rows: Record<string, any>[] = []
      let skipped = 0
      let datesSkipped = 0
      for (let r = PC_LEDGER_FIRST_ROW; r <= ws.rowCount; r++) {
        const values: Record<string, any> = {}
        let filled = 0
        PC_LEDGER_COLUMNS.forEach((c) => {
          if (!c.key) return
          const cell = ws.getRow(r).getCell(c.col)
          const raw = cellText(cell.value).trim()
          if (!raw) return
          if (c.key === 'tanggal') {
            const date = normaliseDate(cell.value, raw)
            if (!date) { datesSkipped++; return }
            values[c.key] = date
            filled++
            return
          }
          values[c.key] = raw
          filled++
        })
        if (!filled) { skipped++; continue }
        rows.push(values)
      }
      parsed.value = { fileName: file.name, sheetName: ws.name, rows, skipped, lastRow: ws.rowCount, datesSkipped }
      if (rows.length) {
        toast.add({ title: 'File read', description: `${rows.length} row(s) ready to import.`, icon: 'i-lucide-circle-check', color: 'success' })
      }
    } catch (err: any) {
      parseError.value = err?.message || 'Could not read that file.'
    } finally {
      parsing.value = false
      if (fileInput.value) fileInput.value.value = ''
    }
  })()
}

const previewRows = computed(() => parsed.value?.rows.slice(0, 5) ?? [])

async function runPcImport() {
  if (!parsed.value) return
  importing.value = true
  importResult.value = null
  try {
    const entities = await apiListEntities(true)
    const entity = entities.find((e: any) => e.slug === PC_LEDGER_SLUG)
    if (!entity) throw new Error('The pc_ledger entity was not found on the API.')
    let ok = 0
    let failed = 0
    let firstError = ''
    for (const values of parsed.value.rows) {
      try {
        await apiCreateRecord(entity.id, values)
        ok++
      } catch (err: any) {
        failed++
        if (!firstError) firstError = err?.data?.message || err?.message || 'Rejected by the API.'
      }
    }
    importResult.value = { ok, failed, firstError }
    toast.add({
      title: failed ? 'Import finished with errors' : 'Import finished',
      description: `${ok} record(s) imported${failed ? `, ${failed} failed` : ''}.`,
      icon: failed ? 'i-lucide-triangle-alert' : 'i-lucide-circle-check',
      color: failed ? 'warning' : 'success'
    })
  } catch (err: any) {
    importResult.value = { ok: 0, failed: parsed.value.rows.length, firstError: err?.message || 'Import failed.' }
  } finally {
    importing.value = false
  }
}

/* ==========================================================================================
   TAB 2 - GID LIST

   Source file: the Global ID export (header in row 1: Global ID | Registration Date |
   Alphabet Name | Division Code | E-mail Address | ID Activation Date | ID Termination Date |
   Employee No | Title). HIRO: "data yang dipakai hanya kolom A, C, E, dan H" - so only four of
   the nine columns are read: A Global ID, C Alphabet Name, E E-mail Address, H Employee No.

   WHY IT IS STORED AT ALL: this list is the SOURCE for the smart suggestions on the PC Ledger's
   add-record form. The register's `gid` column is typed by hand today, so without a canonical list
   there is nothing to suggest from. It lives in its own entity (`gid_list`) rather than in a
   browser cache so every admin sees the same suggestions.
   ========================================================================================== */

const GID_LIST_SLUG = 'gid_list'
const GID_SHEET_COLUMNS: { col: number; key: string; label: string }[] = [
  { col: 1, key: 'gid', label: 'Global ID' },
  { col: 3, key: 'name', label: 'Alphabet Name' },
  { col: 5, key: 'email', label: 'E-mail Address' },
  { col: 8, key: 'employee_no', label: 'Employee No' }
]

type GidParsed = { fileName: string; sheetName: string; rows: Record<string, any>[]; skipped: number }

const gidParsing = ref(false)
const gidParsed = ref<GidParsed | null>(null)
const gidParseError = ref('')
const gidImporting = ref(false)
const gidImportResult = ref<{ ok: number; failed: number; skippedExisting: number; firstError: string } | null>(null)

const gidLoading = ref(false)
const gidList = ref<Record<string, any>[]>([])
const gidError = ref('')

async function loadGids() {
  gidLoading.value = true
  gidError.value = ''
  try {
    const entities = await apiListEntities(true)
    const entity = entities.find((e: any) => e.slug === GID_LIST_SLUG)
    if (!entity) { gidError.value = 'The gid_list entity was not found on the API.'; return }
    const page = await apiListRecords(entity.id, { page: 1, pageSize: 500 })
    gidList.value = ((page.items ?? []) as any[]).map((r) => r.values ?? {})
      .sort((a, b) => String(a.gid ?? '').localeCompare(String(b.gid ?? ''), undefined, { numeric: true }))
  } catch (err: any) {
    gidError.value = err?.message || 'Could not load the GID list.'
  } finally {
    gidLoading.value = false
  }
}

const gidFileInput = ref<HTMLInputElement | null>(null)
const gidPreview = computed(() => gidParsed.value?.rows.slice(0, 5) ?? [])

function onGidFile(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  gidParsing.value = true
  gidParseError.value = ''
  gidParsed.value = null
  gidImportResult.value = null
  ;(async () => {
    try {
      const ws = await readWorkbook(file)
      const rows: Record<string, any>[] = []
      let skipped = 0
      // Row 1 is the header.
      for (let r = 2; r <= ws.rowCount; r++) {
        const values: Record<string, any> = {}
        GID_SHEET_COLUMNS.forEach((c) => {
          const raw = cellText(ws.getRow(r).getCell(c.col).value).trim()
          if (raw) values[c.key] = raw
        })
        // A row without a Global ID cannot suggest anything, so it is not imported.
        if (!values.gid) { skipped++; continue }
        rows.push(values)
      }
      gidParsed.value = { fileName: file.name, sheetName: ws.name, rows, skipped }
      toast.add({ title: 'File read', description: `${rows.length} GID row(s) ready.`, icon: 'i-lucide-circle-check', color: 'success' })
    } catch (err: any) {
      gidParseError.value = err?.message || 'Could not read that file.'
    } finally {
      gidParsing.value = false
      if (gidFileInput.value) gidFileInput.value.value = ''
    }
  })()
}

async function runGidImport() {
  if (!gidParsed.value) return
  gidImporting.value = true
  gidImportResult.value = null
  try {
    const entities = await apiListEntities(true)
    const entity = entities.find((e: any) => e.slug === GID_LIST_SLUG)
    if (!entity) throw new Error('The gid_list entity was not found on the API.')
    // Existing GIDs are skipped rather than duplicated: this list gets re-imported whenever HR
    // sends a new export, so "import again" has to mean "add what is new".
    const existing = new Set(gidList.value.map((r) => String(r.gid ?? '').trim()))
    let ok = 0
    let failed = 0
    let skippedExisting = 0
    let firstError = ''
    for (const values of gidParsed.value.rows) {
      const gid = String(values.gid).trim()
      if (existing.has(gid)) { skippedExisting++; continue }
      try {
        await apiCreateRecord(entity.id, values)
        existing.add(gid)
        ok++
      } catch (err: any) {
        failed++
        if (!firstError) firstError = err?.data?.message || err?.message || 'Rejected by the API.'
      }
    }
    gidImportResult.value = { ok, failed, skippedExisting, firstError }
    toast.add({
      title: failed ? 'Import finished with errors' : 'Import finished',
      description: `${ok} added${skippedExisting ? `, ${skippedExisting} already present` : ''}${failed ? `, ${failed} failed` : ''}.`,
      icon: failed ? 'i-lucide-triangle-alert' : 'i-lucide-circle-check',
      color: failed ? 'warning' : 'success'
    })
    await loadGids()
  } catch (err: any) {
    gidImportResult.value = { ok: 0, failed: gidParsed.value.rows.length, skippedExisting: 0, firstError: err?.message || 'Import failed.' }
  } finally {
    gidImporting.value = false
  }
}

function resetGidParsed() {
  gidParsed.value = null
  gidParseError.value = ''
  gidImportResult.value = null
}

const gidSearch = ref('')
const visibleGids = computed(() => {
  const q = gidSearch.value.trim().toLowerCase()
  if (!q) return gidList.value
  return gidList.value.filter((r) =>
    [r.gid, r.name, r.email, r.employee_no].some((v) => String(v ?? '').toLowerCase().includes(q))
  )
})

onMounted(() => { if (activeTab.value === 'gid') loadGids() })
watch(activeTab, (t) => { if (t === 'gid' && !gidList.value.length) loadGids() })
</script>

<template>
  <UDashboardPanel id="data-import" :ui="{ body: 'p-6 anim-fade-up' }">
    <template #header>
      <PageHeader title="Data Import" />

      <!-- The template's own tab bar: `UDashboardToolbar` gives the border, the 49px height and the
           horizontal padding, and the `highlight` prop on the menu draws the active indicator. -->
      <UDashboardToolbar>
        <UNavigationMenu
          :items="links"
          highlight
          orientation="horizontal"
          class="-mx-1 flex-1"
        />
      </UDashboardToolbar>
    </template>

    <template #body>
      <!-- ---------------- tab 1: import PC Ledger ---------------- -->
      <div v-if="activeTab === 'import'" key="import" class="mx-auto w-full max-w-6xl">
        <div class="overflow-hidden rounded-xl border border-default bg-elevated">
          <div class="flex items-start gap-3 border-b border-default px-4 py-3">
            <span class="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary ring-1 ring-inset ring-primary/20">
              <UIcon name="i-lucide-file-up" class="size-5" />
            </span>
            <div class="min-w-0">
              <h2 class="text-sm font-semibold leading-tight">Import PC Ledger workbook</h2>
              <p class="text-xs text-muted">
                The register workbook, read from row 11. Columns B to P are taken in their fixed order; the
                sheet's own No is read but not imported, because the system assigns it.
              </p>
            </div>
          </div>

          <div class="px-4 py-4">
            <div class="flex flex-wrap items-center gap-2">
              <input ref="fileInput" type="file" accept=".xlsx,.xlsm" class="hidden" @change="onPcFile">
              <UButton
                icon="i-lucide-upload"
                :label="parsed ? 'Choose another file' : 'Choose Excel file'"
                :loading="parsing"
                @click="fileInput?.click()"
              />
              <UButton
                v-if="parsed"
                color="neutral"
                variant="ghost"
                icon="i-lucide-x"
                label="Clear"
                @click="parsed = null; importResult = null"
              />
              <span v-if="parsed" class="text-xs text-muted">{{ parsed.fileName }}</span>
            </div>

            <UAlert v-if="parseError" color="error" variant="soft" icon="i-lucide-circle-alert" :title="parseError" class="mt-3" />

            <template v-if="parsed">
              <dl class="mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-xs">
                <dt class="text-muted">Sheet</dt>
                <dd class="font-medium">{{ parsed.sheetName }}</dd>
                <dt class="text-muted">Data read from</dt>
                <dd class="font-medium">Row 11, columns B-P</dd>
                <dt class="text-muted">Rows to import</dt>
                <dd class="font-medium">{{ parsed.rows.length }} of {{ Math.max(0, parsed.lastRow - 10) }} row(s)</dd>
                <dt v-if="parsed.skipped" class="text-muted">Empty rows skipped</dt>
                <dd v-if="parsed.skipped" class="font-medium">{{ parsed.skipped }}</dd>
                <dt v-if="parsed.datesSkipped" class="text-muted">Unreadable dates dropped</dt>
                <dd v-if="parsed.datesSkipped" class="font-medium">{{ parsed.datesSkipped }}</dd>
              </dl>

              <div class="mt-4 overflow-x-auto rounded-lg border border-default">
                <table class="w-full text-xs">
                  <thead>
                    <tr class="border-b border-default bg-default/60">
                      <th
                        v-for="c in PC_LEDGER_COLUMNS"
                        :key="c.col"
                        class="whitespace-nowrap px-3 py-2 text-left font-semibold uppercase tracking-wide text-primary"
                      >
                        {{ c.label }}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="(r, i) in previewRows" :key="i" class="border-b border-default/60 last:border-0">
                      <td v-for="c in PC_LEDGER_COLUMNS" :key="c.col" class="max-w-[14rem] truncate px-3 py-2">
                        {{ c.key ? (r[c.key] ?? '-') : '-' }}
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <p class="mt-1 text-[11px] text-muted">
                Preview: first {{ previewRows.length }} row(s). The No column shows - because the system assigns it on import.
              </p>

              <div class="mt-4">
                <UButton
                  color="primary"
                  icon="i-lucide-check"
                  :label="`Import ${parsed.rows.length} record(s)`"
                  :loading="importing"
                  :disabled="!parsed.rows.length"
                  @click="runPcImport"
                />
              </div>

              <UAlert
                v-if="importResult"
                class="mt-3"
                :color="importResult.failed ? 'warning' : 'success'"
                variant="soft"
                :icon="importResult.failed ? 'i-lucide-triangle-alert' : 'i-lucide-circle-check'"
                :title="`${importResult.ok} imported, ${importResult.failed} failed`"
                :description="importResult.firstError || undefined"
              />
            </template>

            <p v-else class="mt-4 text-xs text-muted">
              Expected file: the register workbook, one record per row starting at row 11, columns B to P
              in the fixed order No, Staff Name, Email Address, GID, JAPAN Hostname, Computer Model,
              Computer S/N, Date, Computer Chassis, Computer Manufacturer, Computer O/S Name,
              Computer O/S Architecture, Location, Remark1, Remark2.
            </p>
          </div>
        </div>
      </div>

      <!-- ---------------- tab 2: GID list ---------------- -->
      <div v-else key="gid" class="mx-auto w-full max-w-6xl">
        <div class="mb-4 overflow-hidden rounded-xl border border-default bg-elevated">
          <div class="flex items-start gap-3 border-b border-default px-4 py-3">
            <span class="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary ring-1 ring-inset ring-primary/20">
              <UIcon name="i-lucide-upload" class="size-5" />
            </span>
            <div class="min-w-0">
              <h2 class="text-sm font-semibold leading-tight">Import GID list</h2>
              <p class="text-xs text-muted">
                The Global ID export. Only four of its columns are used: A Global ID, C Alphabet Name,
                E E-mail Address and H Employee No. GIDs already in the list are skipped, so importing a
                fresh export only adds what is new.
              </p>
            </div>
          </div>

          <div class="px-4 py-4">
            <div class="flex flex-wrap items-center gap-2">
              <input ref="gidFileInput" type="file" accept=".xlsx,.xlsm" class="hidden" @change="onGidFile">
              <UButton
                icon="i-lucide-upload"
                :label="gidParsed ? 'Choose another file' : 'Choose Excel file'"
                :loading="gidParsing"
                @click="gidFileInput?.click()"
              />
              <UButton
                v-if="gidParsed"
                color="neutral"
                variant="ghost"
                icon="i-lucide-x"
                label="Clear"
                @click="resetGidParsed"
              />
              <span v-if="gidParsed" class="text-xs text-muted">{{ gidParsed.fileName }}</span>
            </div>

            <UAlert v-if="gidParseError" color="error" variant="soft" icon="i-lucide-circle-alert" :title="gidParseError" class="mt-3" />

            <template v-if="gidParsed">
              <dl class="mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-xs">
                <dt class="text-muted">Sheet</dt>
                <dd class="font-medium">{{ gidParsed.sheetName }}</dd>
                <dt class="text-muted">Columns used</dt>
                <dd class="font-medium">A, C, E, H</dd>
                <dt class="text-muted">Rows with a Global ID</dt>
                <dd class="font-medium">{{ gidParsed.rows.length }}</dd>
                <dt v-if="gidParsed.skipped" class="text-muted">Rows skipped (no Global ID)</dt>
                <dd v-if="gidParsed.skipped" class="font-medium">{{ gidParsed.skipped }}</dd>
              </dl>

              <div class="mt-4 overflow-x-auto rounded-lg border border-default">
                <table class="w-full text-xs">
                  <thead>
                    <tr class="border-b border-default bg-default/60">
                      <th v-for="c in GID_SHEET_COLUMNS" :key="c.key" class="whitespace-nowrap px-3 py-2 text-left font-semibold uppercase tracking-wide text-primary">
                        {{ c.label }}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="(r, i) in gidPreview" :key="i" class="border-b border-default/60 last:border-0">
                      <td v-for="c in GID_SHEET_COLUMNS" :key="c.key" class="max-w-[18rem] truncate px-3 py-2">{{ r[c.key] ?? '-' }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <p class="mt-1 text-[11px] text-muted">Preview: first {{ gidPreview.length }} row(s).</p>

              <div class="mt-4">
                <UButton
                  color="primary"
                  icon="i-lucide-check"
                  :label="`Import ${gidParsed.rows.length} GID row(s)`"
                  :loading="gidImporting"
                  :disabled="!gidParsed.rows.length"
                  @click="runGidImport"
                />
              </div>

              <UAlert
                v-if="gidImportResult"
                class="mt-3"
                :color="gidImportResult.failed ? 'warning' : 'success'"
                variant="soft"
                :icon="gidImportResult.failed ? 'i-lucide-triangle-alert' : 'i-lucide-circle-check'"
                :title="`${gidImportResult.ok} added, ${gidImportResult.skippedExisting} already present, ${gidImportResult.failed} failed`"
                :description="gidImportResult.firstError || undefined"
              />
            </template>
          </div>
        </div>

        <div class="overflow-hidden rounded-xl border border-default bg-elevated">
          <div class="flex items-center justify-between gap-3 border-b border-default px-4 py-3">
            <div class="flex items-center gap-3">
              <span class="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary ring-1 ring-inset ring-primary/20">
                <UIcon name="i-lucide-list" class="size-5" />
              </span>
              <div class="min-w-0">
                <h2 class="text-sm font-semibold leading-tight">Stored GID list</h2>
                <p class="text-xs text-muted">The suggestions the PC Ledger's add-record form draws on.</p>
              </div>
            </div>
            <div class="flex items-center gap-2">
              <UInput
                v-model="gidSearch"
                icon="i-lucide-search"
                placeholder="Search GID, name, email..."
                class="w-64"
                :ui="{ base: 'h-9' }"
              />
              <UButton color="neutral" variant="ghost" icon="i-lucide-refresh-cw" label="Refresh" :loading="gidLoading" @click="loadGids" />
            </div>
          </div>

          <UAlert v-if="gidError" color="error" variant="soft" icon="i-lucide-circle-alert" :title="gidError" class="m-4" />

          <div class="max-h-[52vh] overflow-y-scroll">
            <table class="w-full table-fixed text-sm">
              <thead class="sticky top-0 z-10">
                <tr class="border-b border-default bg-elevated">
                  <th class="w-[16%] px-4 py-2 text-left text-xs font-semibold uppercase tracking-wide text-primary">Global ID</th>
                  <th class="px-4 py-2 text-left text-xs font-semibold uppercase tracking-wide text-primary">Name</th>
                  <th class="w-[30%] px-4 py-2 text-left text-xs font-semibold uppercase tracking-wide text-primary">E-mail</th>
                  <th class="w-[14%] px-4 py-2 text-left text-xs font-semibold uppercase tracking-wide text-primary">Employee No</th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="!visibleGids.length && !gidLoading">
                  <td colspan="4" class="px-4 py-10 text-center text-xs text-muted">
                    {{ gidList.length ? 'Nothing matches that search.' : 'No GID stored yet - import the Global ID export above.' }}
                  </td>
                </tr>
                <tr v-for="(g, i) in visibleGids" :key="i" class="border-b border-default/60 last:border-0 hover:bg-primary/5">
                  <td class="truncate px-4 py-2 font-medium">{{ g.gid }}</td>
                  <td class="truncate px-4 py-2">{{ g.name || '-' }}</td>
                  <td class="truncate px-4 py-2">{{ g.email || '-' }}</td>
                  <td class="truncate px-4 py-2 tabular-nums">{{ g.employee_no || '-' }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="flex items-center justify-between gap-3 border-t border-default bg-default/30 px-4 py-2.5 text-xs text-muted">
            <span>{{ visibleGids.length }} of {{ gidList.length }} GID{{ gidList.length === 1 ? '' : 's' }}</span>
            <span>Source for the smart suggestions on the PC Ledger form</span>
          </div>
        </div>
      </div>
    </template>
  </UDashboardPanel>
</template>
