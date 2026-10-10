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
 * THE TAB BAR IS THE TEMPLATE'S OWN PATTERN - `UDashboardPanel` + `UDashboardNavbar` and then a
 * `UDashboardToolbar` holding a horizontal `UNavigationMenu` with `highlight`, copied from the
 * dashboard template's settings page. HIRO pointed at exactly that structure: the element
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
const { isAdmin } = useAuth()

const TABS = [
  { key: 'import', label: 'Import PC Ledger', icon: 'i-lucide-file-up' },
  { key: 'gid', label: 'GID List', icon: 'i-lucide-list' }
] as const

const activeTab = computed(() => (route.query.tab === 'gid' ? 'gid' : 'import'))

const links = computed<NavigationMenuItem[][]>(() => [
  TABS.map((t) => ({
    label: t.label,
    icon: t.icon,
    to: { path: '/data-import', query: { tab: t.key } },
    // `exact` is what makes the two entries distinguishable at all: they share a path and differ
    // only by query, so a prefix match would light up both of them at once.
    exact: true
  }))
])

function selectTab(key: string) {
  if (key !== activeTab.value) router.push({ path: '/data-import', query: { tab: key } })
}

/* ------------------------------------------------------------------------------------------
   IMPORT PC LEDGER

   The workbook is the same one the PC Ledger page exports (IT FORM SG031): notes in rows 3-6,
   the header in row 10, one record per row from row 11. Rather than hard-coding row 10, the
   header row is FOUND - the row whose cells match the most known column labels - so a file that
   was edited by hand, or exported from a slightly different sheet, still lands.
   ------------------------------------------------------------------------------------------ */

/** Workbook label -> pc_ledger field key. `No` is deliberately absent: the API numbers it. */
const FIELD_BY_LABEL: Record<string, string> = {
  'staff name': 'staff_name',
  'email address': 'email',
  'email': 'email',
  'gid': 'gid',
  'japan hostname': 'japan_hostname',
  'japan host': 'japan_hostname',
  'computer model': 'computer_model',
  'model': 'computer_model',
  'computer s/n': 'computer_sn',
  's/n': 'computer_sn',
  'date': 'tanggal',
  'computer chassis': 'chassis',
  'chassis': 'chassis',
  'computer manufacturer': 'manufacturer',
  'vendor': 'manufacturer',
  'computer o/s name': 'os_name',
  'o/s name': 'os_name',
  'computer o/s architecture': 'os_arch',
  'o/s arch': 'os_arch',
  'location': 'lokasi',
  'remark1': 'remark2',
  'remark2': 'remark3',
  'remark 1': 'remark2',
  'remark 2': 'remark3',
  'department': 'departemen'
}

const PC_LEDGER_SLUG = 'pc_ledger'

type Parsed = {
  fileName: string
  sheetName: string
  headerRow: number
  columns: { index: number; key: string; label: string }[]
  rows: Record<string, any>[]
  skipped: number
}

const parsing = ref(false)
const parsed = ref<Parsed | null>(null)
const parseError = ref('')
const importing = ref(false)
const importResult = ref<{ ok: number; failed: number; firstError: string } | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)

function cellText(v: any): string {
  if (v === null || v === undefined) return ''
  if (typeof v === 'object') {
    // ExcelJS cells can carry rich text, a formula result or a hyperlink depending on the file.
    if (Array.isArray(v.richText)) return v.richText.map((r: any) => r.text ?? '').join('')
    if (v.text !== undefined) return String(v.text)
    if (v.result !== undefined) return String(v.result)
    if (v instanceof Date) return v.toISOString().slice(0, 10)
    return ''
  }
  if (v instanceof Date) return v.toISOString().slice(0, 10)
  return String(v)
}

async function onFile(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  parsing.value = true
  parsed.value = null
  parseError.value = ''
  importResult.value = null
  try {
    const wb = new ExcelJS.Workbook()
    await wb.xlsx.load(await file.arrayBuffer())
    const ws = wb.worksheets[0]
    if (!ws) throw new Error('The workbook has no sheet.')

    // Find the header row: the first 15 rows, scored by how many known labels it carries.
    let headerRow = 0
    let best: { index: number; key: string; label: string }[] = []
    for (let r = 1; r <= Math.min(15, ws.rowCount); r++) {
      const found: { index: number; key: string; label: string }[] = []
      ws.getRow(r).eachCell((cell, col) => {
        const label = cellText(cell.value).trim().toLowerCase()
        const key = FIELD_BY_LABEL[label]
        if (key) found.push({ index: col, key, label })
      })
      if (found.length > best.length) { best = found; headerRow = r }
    }
    if (best.length < 3) throw new Error('No header row recognised - expected the register columns (Staff Name, GID, Computer Model...).')

    const rows: Record<string, any>[] = []
    let skipped = 0
    for (let r = headerRow + 1; r <= ws.rowCount; r++) {
      const values: Record<string, any> = {}
      let filled = 0
      best.forEach((c) => {
        const raw = cellText(ws.getRow(r).getCell(c.index).value).trim()
        if (raw) { values[c.key] = raw; filled++ }
      })
      if (!filled) { skipped++; continue }
      rows.push(values)
    }
    parsed.value = {
      fileName: file.name,
      sheetName: ws.name,
      headerRow,
      columns: best,
      rows,
      skipped
    }
    if (rows.length) toast.add({ title: 'File read', description: `${rows.length} row(s) ready to import.`, icon: 'i-lucide-circle-check', color: 'success' })
  } catch (err: any) {
    parseError.value = err?.message || 'Could not read that file.'
  } finally {
    parsing.value = false
    if (fileInput.value) fileInput.value.value = ''
  }
}

const previewRows = computed(() => parsed.value?.rows.slice(0, 5) ?? [])

async function runImport() {
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

function resetParsed() {
  parsed.value = null
  parseError.value = ''
  importResult.value = null
}

/* ------------------------------------------------------------------------------------------
   GID LIST - the distinct GIDs currently held in the PC Ledger, with what they own.
   ------------------------------------------------------------------------------------------ */

const gidLoading = ref(false)
const gids = ref<{ gid: string; owners: string[]; pcs: number }[]>([])
const gidError = ref('')

async function loadGids() {
  gidLoading.value = true
  gidError.value = ''
  try {
    const entities = await apiListEntities(true)
    const entity = entities.find((e: any) => e.slug === PC_LEDGER_SLUG)
    if (!entity) throw new Error('The pc_ledger entity was not found on the API.')
    const page = await apiListRecords(entity.id, { page: 1, pageSize: 500 })
    const map = new Map<string, { owners: Set<string>; pcs: number }>()
    for (const r of page.items ?? []) {
      const v = (r as any).values ?? {}
      const gid = String(v.gid ?? '').trim()
      if (!gid) continue
      const cur = map.get(gid) ?? { owners: new Set<string>(), pcs: 0 }
      const owner = String(v.staff_name ?? '').trim()
      if (owner) cur.owners.add(owner)
      cur.pcs++
      map.set(gid, cur)
    }
    gids.value = [...map.entries()]
      .map(([gid, v]) => ({ gid, owners: [...v.owners].sort(), pcs: v.pcs }))
      .sort((a, b) => a.gid.localeCompare(b.gid, undefined, { numeric: true }))
  } catch (err: any) {
    gidError.value = err?.message || 'Could not load the GID list.'
  } finally {
    gidLoading.value = false
  }
}

onMounted(() => { if (activeTab.value === 'gid') loadGids() })
watch(activeTab, (t) => { if (t === 'gid' && !gids.value.length) loadGids() })
</script>

<template>
  <UDashboardPanel id="data-import" :ui="{ body: 'p-6' }">
    <template #header>
      <UDashboardNavbar title="Data Import" />

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
      <!-- ---------------- tab 1: import ---------------- -->
      <div v-if="activeTab === 'import'" key="import" class="anim-fade-up mx-auto w-full max-w-5xl">
        <div class="overflow-hidden rounded-xl border border-default bg-elevated">
          <div class="flex items-start gap-3 border-b border-default px-4 py-3">
            <span class="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary ring-1 ring-inset ring-primary/20">
              <UIcon name="i-lucide-file-up" class="size-5" />
            </span>
            <div class="min-w-0">
              <h2 class="text-sm font-semibold leading-tight">Import PC Ledger workbook</h2>
              <p class="text-xs text-muted">
                The file is read in the browser first and shown below - nothing is written until you press Import.
              </p>
            </div>
          </div>

          <div class="px-4 py-4">
            <div class="flex flex-wrap items-center gap-2">
              <input
                ref="fileInput"
                type="file"
                accept=".xlsx,.xlsm"
                class="hidden"
                @change="onFile"
              >
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
                @click="resetParsed"
              />
              <span v-if="parsed" class="text-xs text-muted">{{ parsed.fileName }}</span>
            </div>

            <UAlert
              v-if="parseError"
              color="error"
              variant="soft"
              icon="i-lucide-circle-alert"
              :title="parseError"
              class="mt-3"
            />

            <template v-if="parsed">
              <dl class="mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-xs">
                <dt class="text-muted">Sheet</dt>
                <dd class="font-medium">{{ parsed.sheetName }}</dd>
                <dt class="text-muted">Header row found</dt>
                <dd class="font-medium">Row {{ parsed.headerRow }}</dd>
                <dt class="text-muted">Columns recognised</dt>
                <dd class="font-medium">{{ parsed.columns.length }}</dd>
                <dt class="text-muted">Rows to import</dt>
                <dd class="font-medium">{{ parsed.rows.length }}</dd>
                <dt v-if="parsed.skipped" class="text-muted">Empty rows skipped</dt>
                <dd v-if="parsed.skipped" class="font-medium">{{ parsed.skipped }}</dd>
              </dl>

              <div class="mt-3 flex flex-wrap gap-1.5">
                <UBadge
                  v-for="c in parsed.columns"
                  :key="c.key"
                  color="neutral"
                  variant="soft"
                  size="sm"
                >
                  {{ c.label }}
                </UBadge>
              </div>

              <div class="mt-4 overflow-x-auto rounded-lg border border-default">
                <table class="w-full text-xs">
                  <thead>
                    <tr class="border-b border-default bg-default/60">
                      <th
                        v-for="c in parsed.columns"
                        :key="c.key"
                        class="whitespace-nowrap px-3 py-2 text-left font-semibold uppercase tracking-wide text-primary"
                      >
                        {{ c.label }}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="(r, i) in previewRows" :key="i" class="border-b border-default/60 last:border-0">
                      <td v-for="c in parsed.columns" :key="c.key" class="max-w-[16rem] truncate px-3 py-2">
                        {{ r[c.key] ?? '-' }}
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <p class="mt-1 text-[11px] text-muted">Preview: first {{ previewRows.length }} row(s).</p>

              <div class="mt-4 flex items-center gap-2">
                <UButton
                  color="primary"
                  icon="i-lucide-check"
                  :label="`Import ${parsed.rows.length} record(s)`"
                  :loading="importing"
                  :disabled="!parsed.rows.length"
                  @click="runImport"
                />
                <span class="text-[11px] text-muted">No is assigned by the system, so the No column is not imported.</span>
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
              Expected columns are the ones the register uses: Staff Name, Email Address, GID, JAPAN Hostname,
              Computer Model, Computer S/N, Date, Computer Chassis, Computer Manufacturer, Computer O/S Name,
              Computer O/S Architecture, Location, Remark1, Remark2.
            </p>
          </div>
        </div>
      </div>

      <!-- ---------------- tab 2: GID list ---------------- -->
      <div v-else key="gid" class="anim-fade-up mx-auto w-full max-w-5xl">
        <div class="overflow-hidden rounded-xl border border-default bg-elevated">
          <div class="flex items-center justify-between gap-3 border-b border-default px-4 py-3">
            <div class="flex items-center gap-3">
              <span class="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary ring-1 ring-inset ring-primary/20">
                <UIcon name="i-lucide-list" class="size-5" />
              </span>
              <div class="min-w-0">
                <h2 class="text-sm font-semibold leading-tight">GID list</h2>
                <p class="text-xs text-muted">Every GID currently recorded in the PC Ledger, and the machines under it.</p>
              </div>
            </div>
            <UButton
              color="neutral"
              variant="ghost"
              icon="i-lucide-refresh-cw"
              label="Refresh"
              :loading="gidLoading"
              @click="loadGids"
            />
          </div>

          <UAlert
            v-if="gidError"
            color="error"
            variant="soft"
            icon="i-lucide-circle-alert"
            :title="gidError"
            class="m-4"
          />

          <div class="max-h-[60vh] overflow-y-scroll">
            <table class="w-full table-fixed text-sm">
              <thead class="sticky top-0 z-10">
                <tr class="border-b border-default bg-elevated">
                  <th class="w-[16%] px-4 py-2 text-left text-xs font-semibold uppercase tracking-wide text-primary">GID</th>
                  <th class="px-4 py-2 text-left text-xs font-semibold uppercase tracking-wide text-primary">Owner(s)</th>
                  <th class="w-[12%] px-4 py-2 text-left text-xs font-semibold uppercase tracking-wide text-primary">PCs</th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="!gids.length && !gidLoading">
                  <td colspan="3" class="px-4 py-10 text-center text-xs text-muted">No GID recorded yet.</td>
                </tr>
                <tr
                  v-for="g in gids"
                  :key="g.gid"
                  class="border-b border-default/60 last:border-0 hover:bg-primary/5"
                >
                  <td class="truncate px-4 py-2 font-medium tabular-nums">{{ g.gid }}</td>
                  <td class="truncate px-4 py-2">{{ g.owners.join(', ') || '-' }}</td>
                  <td class="px-4 py-2 tabular-nums">{{ g.pcs }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="flex items-center justify-between gap-3 border-t border-default bg-default/30 px-4 py-2.5 text-xs text-muted">
            <span>{{ gids.length }} GID{{ gids.length === 1 ? '' : 's' }}</span>
            <span class="tabular-nums">{{ gids.reduce((a, g) => a + g.pcs, 0) }} PC(s) covered</span>
          </div>
        </div>
      </div>
    </template>
  </UDashboardPanel>
</template>
