<script setup lang="ts">
import { apiListEntities, apiListRecords } from '~/composables/useApi'
import type { Suggestion } from '~/components/SuggestInput.vue'
import { emptyIssuedAsset, validateIssuedInput } from '~/utils/issuedForm'
import templateUrl from '~/assets/forms/it-equipment-issue-return-v2.pdf?url'

const form = reactive({ user: '', employeeNo: '', assets: [emptyIssuedAsset()] })
const candidates = ref<Record<string, any>[]>([])
const gidLoading = ref(false)
const gidError = ref('')
const hints = reactive({ user: '', employeeNo: '' })
const generating = ref(false)
const message = ref('')
const success = ref(false)
const cols = [
  { key: 'brandModel', label: 'Brand & Model', placeholder: 'e.g. Panasonic Toughbook' },
  { key: 'serialNo', label: 'Serial No.', placeholder: 'Serial number' },
  { key: 'poNo', label: 'PO No. (If Any)', placeholder: 'Optional' },
  { key: 'remark', label: 'FA No. / Remark (If Any)', placeholder: 'Optional' }
] as const

async function loadGid() {
  gidLoading.value = true; gidError.value = ''
  try {
    const entities = await apiListEntities(true)
    const gid = entities.find((e: any) => e.slug === 'gid_list')
    if (!gid) throw new Error('GID List is unavailable.')
    const loaded = new Map<number, Record<string, any>>()
    for (let page = 1; ; page++) {
      const result = await apiListRecords(gid.id, { page, pageSize: 500 })
      const before = loaded.size
      for (const row of result.items ?? []) loaded.set(row.id, row.values ?? {})
      if (loaded.size >= result.total) break
      if (loaded.size === before) throw new Error('GID List could not be loaded completely.')
    }
    candidates.value = [...loaded.values()]
  } catch {
    candidates.value = []
    gidError.value = 'GID suggestions unavailable. You can enter the details manually or retry.'
  } finally { gidLoading.value = false }
}
const suggestions = computed(() => {
  const make = (key: 'name' | 'employee_no'): Suggestion[] => candidates.value.map(r => ({
    value: String(r[key] ?? '').trim(), label: String(r.name ?? '').trim(),
    meta: [r.employee_no, r.email, r.gid].filter(Boolean).join(' | '),
    keys: [r.name, r.employee_no, r.email, r.gid].map(v => String(v ?? '')),
    fill: r, hint: 'Auto-filled from the GID List'
  })).filter(s => s.value)
  return { user: make('name'), employeeNo: make('employee_no') }
})
function pickUser(s: Suggestion) {
  if (!s.fill) return
  // Replace BOTH values, including empty ones: never retain another employee's identifier.
  form.user = String(s.fill.name ?? '').trim()
  form.employeeNo = String(s.fill.employee_no ?? '').trim()
  hints.user = hints.employeeNo = 'Auto-filled from the GID List'
}
// Chain between stacked Brand & Model boxes: a one-shot copy from the row above,
// not a permanent binding, so later edits to either row stay independent.
const justLinked = ref<number | null>(null)
let linkTimer: ReturnType<typeof setTimeout> | undefined
function linkBrandFromAbove(index: number) {
  const above = form.assets[index - 1]
  if (!above || !form.assets[index]) return
  form.assets[index].brandModel = above.brandModel
  justLinked.value = index
  clearTimeout(linkTimer)
  linkTimer = setTimeout(() => { justLinked.value = null }, 700)
}
onBeforeUnmount(() => clearTimeout(linkTimer))
async function generate() {
  if (generating.value) return
  message.value = ''; success.value = false
  const error = validateIssuedInput(form)
  if (error) { message.value = error; return }
  const clickedAt = new Date()
  const snapshot = JSON.parse(JSON.stringify(form))
  generating.value = true
  try {
    const [{ generateIssuedPdf }, response] = await Promise.all([import('~/utils/issuedForm'), fetch(templateUrl)])
    if (!response.ok) throw new Error('The original PDF template could not be loaded.')
    const bytes = await generateIssuedPdf(new Uint8Array(await response.arrayBuffer()), snapshot, clickedAt)
    const url = URL.createObjectURL(new Blob([new Uint8Array(bytes)], { type: 'application/pdf' }))
    const a = document.createElement('a')
    const safeNo = snapshot.employeeNo.replace(/[^a-zA-Z0-9_-]/g, '_').slice(0,40)
    a.href = url; a.download = `IT-Equipment-Issued-${safeNo}.pdf`
    document.body.appendChild(a); a.click(); a.remove()
    window.setTimeout(() => URL.revokeObjectURL(url), 60000)
    success.value = true
    message.value = 'PDF generated. Open it in Adobe Acrobat Reader to review and digitally sign.'
  } catch (e: any) { message.value = e.message || 'Unable to generate the PDF. Please try again.' }
  finally { generating.value = false }
}
onMounted(loadGid)
</script>

<template>
  <UDashboardPanel id="issued-form" :ui="{ body: 'p-4 sm:p-6' }">
    <template #header><PageHeader title="Issued Form" /></template>
    <template #body>
      <form class="mx-auto w-full max-w-6xl space-y-6" @submit.prevent="generate">
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h2 class="text-xl font-semibold text-highlighted">IT Equipment Issue / Return Form</h2>
            <p class="mt-1 text-sm text-muted">IT FORM-BT015 · V2 · Fill the original PDF without removing its signature fields.</p>
          </div>
          <UBadge color="primary" variant="subtle">Interactive PDF</UBadge>
        </div>
        <UCard :ui="{ header: 'px-4 py-4 sm:px-6', body: 'p-4 sm:p-6' }">
          <template #header>
            <div class="flex items-center justify-between gap-3">
              <div><h3 class="font-semibold text-highlighted">Section A — IT Asset List</h3><p class="mt-1 text-xs text-muted">Up to six equipment rows, matching the original form.</p></div>
              <UBadge color="neutral" variant="outline" class="whitespace-nowrap">{{ form.assets.length }} / 6</UBadge>
            </div>
          </template>
          <!-- One shared column title for the whole list: the labels belong to the list, not to each row,
               so a six-row form shows four headers once instead of four headers six times. -->
          <fieldset :disabled="generating" aria-label="IT asset list" class="space-y-2">
            <div class="hidden gap-2 px-1 pb-0.5 sm:grid sm:grid-cols-[1.75rem_repeat(4,minmax(0,1fr))_1.75rem]">
              <span class="text-xs font-medium text-muted">No.</span>
              <span v-for="col in cols" :key="col.key" class="text-xs font-medium text-muted">
                {{ col.label }}<span v-if="col.key === 'brandModel'" class="text-error" aria-hidden="true">*</span>
              </span>
              <span class="sr-only">Remove</span>
            </div>
            <div
              v-for="(asset, index) in form.assets"
              :key="index"
              class="relative grid grid-cols-1 gap-x-2 gap-y-1.5 rounded-md px-1 py-1.5 pr-9 transition-colors duration-150 hover:bg-elevated/40 focus-within:bg-elevated/40 sm:grid-cols-[1.75rem_repeat(4,minmax(0,1fr))_1.75rem] sm:items-center sm:pr-1"
            >
              <span class="grid size-5 shrink-0 place-items-center rounded-md bg-primary/10 text-[11px] font-semibold text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">{{ index + 1 }}</span>
              <label v-for="col in cols" :key="col.key" class="relative block min-w-0">
                <span class="mb-1 flex items-center gap-1.5 text-xs font-medium text-muted sm:hidden">
                  {{ col.label }}<span v-if="col.key === 'brandModel'" class="text-error" aria-hidden="true">*</span>
                  <!-- Mobile has no room for a chip in the gap, so the same action sits on the label line. -->
                  <button
                    v-if="col.key === 'brandModel' && index > 0"
                    type="button"
                    class="-my-1 inline-flex size-5 items-center justify-center rounded-md text-muted transition hover:bg-elevated hover:text-primary"
                    :aria-label="`Copy Brand & Model from equipment ${index}`"
                    :title="`Same as equipment ${index}`"
                    @click="linkBrandFromAbove(index)"
                  >
                    <!-- Inline chain glyph: the bundled icon set has no link-2, and this must work offline. -->
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" class="size-3.5" :class="justLinked === index ? 'scale-110 text-primary' : ''" aria-hidden="true">
                      <path d="M9 17H7A5 5 0 0 1 7 7h2" /><path d="M15 7h2a5 5 0 1 1 0 10h-2" /><path d="M8 12h8" />
                    </svg>
                  </button>
                </span>
                <UInput v-model="asset[col.key]" :aria-label="`Equipment ${index + 1} ${col.label}`" :placeholder="col.placeholder" class="w-full" :maxlength="160" />
                <!-- Vertical chain centred in the gap between this Brand & Model box and the one above it. -->
                <button
                  v-if="col.key === 'brandModel' && index > 0"
                  type="button"
                  class="absolute -top-[19px] left-0 z-10 hidden size-[18px] items-center justify-center rounded-full border border-default bg-default text-muted shadow-sm transition hover:border-primary/50 hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary sm:flex"
                  :aria-label="`Copy Brand & Model from equipment ${index}`"
                  :title="`Same as equipment ${index}`"
                  @click="linkBrandFromAbove(index)"
                >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" class="size-3 rotate-90" :class="justLinked === index ? 'scale-110 text-primary' : ''" aria-hidden="true">
                    <path d="M9 17H7A5 5 0 0 1 7 7h2" /><path d="M15 7h2a5 5 0 1 1 0 10h-2" /><path d="M8 12h8" />
                  </svg>
                </button>
              </label>
              <UButton
                v-if="form.assets.length > 1"
                type="button"
                color="neutral"
                variant="ghost"
                size="xs"
                square
                icon="i-lucide-trash-2"
                class="absolute right-0 top-1 text-muted hover:text-error sm:static sm:justify-self-center"
                :aria-label="`Remove equipment ${index + 1}`"
                :title="`Remove equipment ${index + 1}`"
                @click="form.assets.splice(index, 1)"
              />
            </div>
          </fieldset>
          <div class="mt-2 px-1">
            <UButton type="button" icon="i-lucide-plus" variant="outline" color="neutral" :disabled="form.assets.length >= 6 || generating" @click="form.assets.push(emptyIssuedAsset())">Add equipment</UButton>
          </div>
        </UCard>
        <UCard :ui="{ header: 'px-4 py-4 sm:px-6', body: 'p-4 sm:p-6' }">
          <template #header><h3 class="font-semibold text-highlighted">Section B — User Details</h3></template>
          <fieldset :disabled="generating" class="grid grid-cols-1 gap-5 md:grid-cols-2">
            <UFormField label="User" required>
              <SuggestInput v-model="form.user" v-model:source-hint="hints.user" :suggestions="suggestions.user" :context="[form.employeeNo]" header="GID List" placeholder="Search name or type manually" show-on-suggestions-update select-on-tab @fill="pickUser" />
            </UFormField>
            <UFormField label="Employee No" required>
              <SuggestInput v-model="form.employeeNo" v-model:source-hint="hints.employeeNo" :suggestions="suggestions.employeeNo" :context="[form.user]" header="GID List" placeholder="Search employee number" show-on-suggestions-update select-on-tab @fill="pickUser" />
            </UFormField>
          </fieldset>
          <div class="mt-2 min-h-6 text-xs text-muted" aria-live="polite">
            <span v-if="gidLoading">Loading GID suggestions…</span>
            <template v-else-if="gidError">{{ gidError }} <button type="button" class="text-primary underline" @click="loadGid">Retry</button></template>
            <span v-else>Select a suggestion to fill both fields. Manual entries are also supported.</span>
          </div>
        </UCard>
        <div class="rounded-lg border border-default bg-elevated/40 p-4 text-sm text-muted">
          <p><span class="font-medium text-highlighted">Section C · Received By:</span> the name follows User; its date is set when you generate the PDF and remains editable.</p>
          <p class="mt-2">All other Section C fields and Section D retain the original template values. The eight digital signature fields remain available in the downloaded PDF.</p>
        </div>
        <div class="flex flex-wrap items-center justify-between gap-4 border-t border-default pt-5">
          <p class="text-xs text-muted">Download only · No record is saved to the database.</p>
          <UButton type="submit" icon="i-lucide-file-down" :loading="generating" :disabled="generating">Generate PDF</UButton>
        </div>
        <p class="min-h-10 text-sm" :class="success ? 'text-success' : 'text-error'" role="status">{{ message }}</p>
      </form>
    </template>
  </UDashboardPanel>
</template>
