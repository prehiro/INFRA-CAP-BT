<script setup lang="ts">
import { apiListRecords, apiCreateRecord, apiUpdateRecord, apiDeleteRecord, apiGetEntity, apiListEntities } from '~/composables/useApi'

/**
 * CCTV Log Book — dedicated page that mirrors the paper form
 * "RECORDABLE MEDIA LOG BOOK / Name of System / Dept".
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
  { key: 'nomor', label: 'NO', w: 'w-[7%]' },
  { key: 'tanggal', label: 'Date', w: 'w-[8%]' },
  { key: 'departemen', label: 'Department', w: 'w-[9%]' },
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
const HIDDEN_FIELDS = ['waktu_diminta'] as const

const SIGN_FIELDS = ['tanda_pemohon', 'tanda_isd'] as const

/** Draft of the row currently being entered. */
const form = reactive<Record<string, any>>({})
const nextNo = ref('')
const formDate = computed(() => (form.tanggal ? String(form.tanggal).slice(0, 10) : ''))

function notify(msg: string, kind: 'success' | 'error' = 'success') {
  toast.value = { msg, kind }
  setTimeout(() => { toast.value = null }, 4000)
}

function resetForm() {
  for (const c of COLUMNS) form[c.key] = c.sign ? null : ''
  for (const k of HIDDEN_FIELDS) form[k] = ''
  editing.value = null
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
  const page = await apiListRecords(entity.value.id, {
    page: 1,
    pageSize: 200,
    search: search.value || undefined
  })
  rows.value = page.items as unknown as Row[]
  total.value = page.total
}

/** Preview the next sequential NO for the year of the row's date. */
async function fetchNextNo(date?: string) {
  try {
    const qs = date ? `?date=${encodeURIComponent(date)}` : ''
    const r = await $fetch<{ nomor: string }>(
      `${useRuntimeConfig().public.apiBase}/logbook/cctv/next-no${qs}`,
      { headers: authHeadersFor() }
    )
    nextNo.value = r.nomor
  } catch {
    // A failed preview must not block manual entry; the field stays editable.
    nextNo.value = ''
  }
}

function authHeadersFor(): Record<string, string> {
  const t = useToken().value
  return t ? { Authorization: `Bearer ${t}` } : {}
}

async function openCreate() {
  resetForm()
  form.tanggal = new Date().toISOString().slice(0, 10)
  await fetchNextNo(form.tanggal)
  form.nomor = nextNo.value
  showForm.value = true
}

function openEdit(row: Row) {
  resetForm()
  editing.value = row
  for (const c of COLUMNS) form[c.key] = row.values[c.key] ?? (c.sign ? null : '')
  // Carry hidden-but-stored fields through the edit untouched. The API's UpdateAsync is
  // merge-only (it only touches fields present in the payload), so omitting them would
  // already be safe — but round-tripping them explicitly means the intent is deliberate
  // and a future change to the backend cannot silently drop data.
  for (const k of HIDDEN_FIELDS) form[k] = row.values[k] ?? ''
  showForm.value = true
}

async function save() {
  if (!entity.value) return
  if (!form.tujuan?.trim()) { notify('Purpose / Details wajib diisi.', 'error'); return }
  if (!form.nama_pemohon?.trim()) { notify('PIC Name wajib diisi.', 'error'); return }

  saving.value = true
  try {
    const values: Record<string, any> = {}
    for (const c of COLUMNS) {
      const v = form[c.key]
      values[c.key] = c.sign ? (v || null) : (v === '' ? null : v)
    }
    // Hidden-but-stored fields are not shown in the form, but they must survive a save.
    for (const k of HIDDEN_FIELDS) {
      const v = form[k]
      values[k] = v === '' || v === undefined || v === null ? null : v
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

watch(formDate, d => { if (!editing.value) fetchNextNo(d) })
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
      <PageHeader title="CCTV Log Book" />
    </template>

    <template #body>
      <div class="space-y-4">
        <!-- Sheet header. "Name of System" and "Dept" were removed on 2026-10-02 at
             HIRO's request; the title and the actions are all that remain. -->
        <div class="rounded-lg border border-default bg-elevated p-4">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-lg font-bold tracking-wide">RECORDABLE MEDIA LOG BOOK</h1>
        </div>
        <div class="flex gap-2">
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search..." class="w-48" />
          <UButton icon="i-lucide-plus" label="Add Row" @click="openCreate" />
          <UButton icon="i-lucide-printer" label="Print" variant="soft" @click="printSheet" />
        </div>
      </div>
    </div>

    <div v-if="toast" class="rounded border px-3 py-2 text-sm"
         :class="toast.kind === 'error' ? 'border-error bg-error/10 text-error' : 'border-success bg-success/10 text-success'">
      {{ toast.msg }}
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
          <tbody>
            <tr v-if="loading">
              <td :colspan="COLUMNS.length + 1" class="px-4 py-10 text-center text-muted">
                <UIcon name="i-lucide-loader-circle" class="mx-auto mb-2 size-5 animate-spin" />
                <p class="text-sm">Loading...</p>
              </td>
            </tr>
            <tr v-else-if="!rows.length">
              <td :colspan="COLUMNS.length + 1" class="px-4 py-12 text-center">
                <UIcon name="i-lucide-inbox" class="mx-auto mb-2 size-7 text-dimmed" />
                <p class="text-sm font-medium">No rows yet</p>
                <p class="mt-0.5 text-xs text-muted">Use “Add Row” to record the first entry.</p>
              </td>
            </tr>
            <tr v-for="(r, i) in rows" :key="r.id"
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
                <template v-else-if="c.key === 'nomor'">
                  <span class="font-medium tabular-nums whitespace-nowrap">{{ r.values[c.key] ?? '—' }}</span>
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
          </tbody>
        </table>
      </div>

      <div class="flex items-center justify-between gap-3 border-t border-default bg-default/30 px-4 py-2.5 text-xs text-muted print:hidden">
        <span>{{ total }} {{ total === 1 ? 'row' : 'rows' }}</span>
        <span class="tabular-nums">Showing {{ rows.length }} of {{ total }}</span>
      </div>
    </div>

    <!-- Entry form: inline panel, not UModal. The #footer slot of UModal swallows
         @click on the submit button, which was verified in this codebase before. -->
    <div v-if="showForm" class="rounded-lg border border-default bg-elevated p-4">
      <div class="mb-3 flex items-center justify-between">
        <h2 class="font-semibold">{{ editing ? 'Edit Row' : 'New Row' }}</h2>
        <UButton icon="i-lucide-x" variant="ghost" size="sm" @click="showForm = false" />
      </div>

      <UForm :state="form" @submit="save">
        <div class="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
          <UFormField label="NO" name="nomor" required>
            <UInput v-model="form.nomor" name="nomor" placeholder="1/2026/001" />
          </UFormField>
          <UFormField label="Date" name="tanggal" required>
            <UInput v-model="form.tanggal" name="tanggal" type="date" />
          </UFormField>
          <UFormField label="Department" name="departemen">
            <UInput v-model="form.departemen" name="departemen" placeholder="ISD / CAP" />
          </UFormField>
          <UFormField label="Employee No" name="no_pegawai">
            <UInput v-model="form.no_pegawai" name="no_pegawai" />
          </UFormField>
          <UFormField label="PIC Name" name="nama_pemohon" required>
            <UInput v-model="form.nama_pemohon" name="nama_pemohon" />
          </UFormField>
          <UFormField label="Purpose / Details" name="tujuan" required class="md:col-span-2 xl:col-span-3">
            <UTextarea v-model="form.tujuan" name="tujuan" :rows="2"
                      placeholder="CCTV record at 25/11/25 03:00 - 03:30" />
          </UFormField>
          <UFormField label="Start Time" name="pic_mulai">
            <UInput v-model="form.pic_mulai" name="pic_mulai" placeholder="12/11/25 12:30" />
          </UFormField>
          <UFormField label="End Time" name="pic_selesai">
            <UInput v-model="form.pic_selesai" name="pic_selesai" placeholder="12/11/25 14:00" />
          </UFormField>

          <!-- Field order mirrors COLUMNS above: PIC Sign, then the two times, then
               PIC by ISD immediately before the ISD signature. -->
          <UFormField label="PIC Sign" name="tanda_pemohon">
            <SignaturePad v-model="form.tanda_pemohon" :height="80" />
          </UFormField>
          <UFormField label="PIC by ISD" name="pic_isd">
            <UInput v-model="form.pic_isd" name="pic_isd" />
          </UFormField>
          <UFormField label="ISD Sign" name="tanda_isd">
            <SignaturePad v-model="form.tanda_isd" :height="80" />
          </UFormField>
        </div>

        <div class="mt-4 flex gap-2">
          <UButton type="submit" :loading="saving" icon="i-lucide-check" label="Save" />
          <UButton type="button" variant="ghost" label="Cancel" @click="showForm = false" />
        </div>
      </UForm>
    </div>
      </div>
    </template>
  </UDashboardPanel>
</template>

<style>
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