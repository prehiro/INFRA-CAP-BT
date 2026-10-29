<script setup lang="ts">
import { apiListEntities, apiListRecords } from '~/composables/useApi'
import type { Suggestion } from '~/components/SuggestInput.vue'
import { emptyIssuedAsset, validateIssuedInput } from '~/utils/issuedForm'
import templateUrl from '~/assets/forms/it-equipment-issue-return-v2.pdf?url'

// Three equipment rows are shown up front: a single row looked empty and every real issue
// usually covers more than one device. The cap stays at six.
//
// Each row carries a stable key so <TransitionGroup> can animate an insert or a removal
// instead of snapping. The key is ignored by the PDF mapper, which only reads ASSET_KEYS.
type EquipmentRow = ReturnType<typeof emptyIssuedAsset> & { key: string }
let rowSeq = 0
const makeRow = (): EquipmentRow => ({ ...emptyIssuedAsset(), key: `asset-${++rowSeq}` })
const form = reactive({ user: '', employeeNo: '', assets: [makeRow(), makeRow(), makeRow()] })
const candidates = ref<Record<string, any>[]>([])
const gidLoading = ref(false)
const gidError = ref('')
const hints = reactive({ user: '', employeeNo: '' })
const generating = ref(false)
const message = ref('')
const cols = [
  { key: 'brandModel', label: 'Brand & Model', placeholder: 'Brand & Model' },
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
  form.user = String(s.fill.name ?? '').trim().toUpperCase()
  form.employeeNo = String(s.fill.employee_no ?? '').trim().toUpperCase()
  hints.user = hints.employeeNo = 'Auto-filled from the GID List'
}
// Serial No. drives the PO box: the first character starts the department's PTCAP__ PO code,
// and emptying Serial No. puts the box back to its placeholder - unless the user has replaced
// it with a PO of their own, which must never be overwritten.
const AUTO_PO = 'PTCAP__'
function onCellInput(row: EquipmentRow, key: 'brandModel' | 'serialNo' | 'poNo' | 'remark', value: string) {
  const next = value.toUpperCase()
  const hadSerial = Boolean(row.serialNo.trim())
  row[key] = next
  if (key !== 'serialNo') return
  const hasSerial = Boolean(next.trim())
  if (!hadSerial && hasSerial && !row.poNo.trim()) row.poNo = AUTO_PO
  else if (hadSerial && !hasSerial && row.poNo === AUTO_PO) row.poNo = ''
}
// Chain between stacked boxes: a one-shot copy from the row above, not a permanent binding,
// so later edits to either row stay independent. HIRO asked for the same chain on PO and
// Remark as well, so the linkable columns are a list rather than one hard-coded key.
const LINKED_KEYS = ['brandModel', 'poNo', 'remark'] as const
type LinkedKey = (typeof LINKED_KEYS)[number]
const isLinkable = (key: string): key is LinkedKey => (LINKED_KEYS as readonly string[]).includes(key)

const justLinked = ref<string | null>(null)
let linkTimer: ReturnType<typeof setTimeout> | undefined
// Firework burst state: keyed per column+row, retriggered by a token so a second click replays.
const burstKey = ref<string | null>(null)
const burstId = ref(0)
const SPARKS = Array.from({ length: 14 }, (_, i) => ({
  i,
  angle: i * (360 / 14) + (i % 2 ? 7 : 0),
  distance: 16 + (i % 3) * 7,
  delay: i * 12,
  size: i % 3 === 0 ? 3 : 2
}))
function linkFromAbove(index: number, key: LinkedKey) {
  const above = form.assets[index - 1]
  if (!above || !form.assets[index]) return
  form.assets[index][key] = above[key]
  justLinked.value = `${key}-${index}`
  clearTimeout(linkTimer)
  linkTimer = setTimeout(() => { justLinked.value = null }, 700)
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return
  burstKey.value = `${key}-${index}`
  burstId.value += 1
  window.setTimeout(() => { if (burstKey.value === `${key}-${index}`) burstKey.value = null }, 800)
}
onBeforeUnmount(() => clearTimeout(linkTimer))
const toast = useToast()
// After a successful download the form goes back to its opening state - the same three empty
// rows the page starts with - so the next issue can be keyed in without clearing boxes by hand.
function resetForm() {
  form.user = ''
  form.employeeNo = ''
  hints.user = ''
  hints.employeeNo = ''
  form.assets.slice(0, 3).forEach(row => {
    row.brandModel = ''
    row.serialNo = ''
    row.poNo = ''
    row.remark = ''
  })
  if (form.assets.length > 3) form.assets.splice(3, form.assets.length - 3)
  while (form.assets.length < 3) form.assets.push(makeRow())
  message.value = ''
}
async function generate() {
  if (generating.value) return
  message.value = ''
  const error = validateIssuedInput(form)
  if (error) { message.value = error; return }
  const clickedAt = new Date()
  // Upper-case again at the edge: the PDF must be caps even if a value arrived from a
  // suggestion, a paste or a restored state that bypassed the input handler.
  const snapshot = JSON.parse(JSON.stringify(form))
  snapshot.user = String(snapshot.user).toUpperCase()
  snapshot.employeeNo = String(snapshot.employeeNo).toUpperCase()
  for (const row of snapshot.assets) {
    row.brandModel = String(row.brandModel).toUpperCase()
    row.serialNo = String(row.serialNo).toUpperCase()
    row.poNo = String(row.poNo).toUpperCase()
    row.remark = String(row.remark).toUpperCase()
  }
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
    toast.add({
      title: 'PDF generated',
      description: 'Open it in Adobe Acrobat Reader to review and digitally sign.',
      color: 'primary',
      icon: 'i-lucide-circle-check'
    })
    resetForm()
  } catch (e: any) { message.value = e.message || 'Unable to generate the PDF. Please try again.' }
  finally { generating.value = false }
}
onMounted(loadGid)
</script>

<template>
  <UDashboardPanel id="issued-form" :ui="{ body: 'p-4 sm:p-6' }">
    <template #header><PageHeader title="Issued Form" /></template>
    <template #body>
      <form class="issued-page-motion mx-auto w-full max-w-6xl space-y-6" @submit.prevent="generate">
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div class="flex min-w-0 items-center gap-3">
            <span class="issued-title-icon grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">
              <RecordAddIcon size="lg" random-halo>
                <!-- Same lucide:file-pen-line paths; thinner than the Iconify mask's fixed 2px stroke. -->
                <svg class="pc-add-tilt size-7" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" focusable="false">
                  <path d="M14.364 13.634a2 2 0 0 0-.506.854l-.837 2.87a.5.5 0 0 0 .62.62l2.87-.837a2 2 0 0 0 .854-.506l4.013-4.009a1 1 0 0 0-3.004-3.004zm.123-5.776A1 1 0 0 1 14 7V2" />
                  <path d="M20 19.645V20a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l2.516 2.516M8 18h1" />
                </svg>
              </RecordAddIcon>
            </span>
            <div class="min-w-0">
              <h2 class="text-xl font-semibold text-highlighted">IT Equipment Issue / Return Form</h2>
              <p class="mt-1 text-sm text-muted">IT FORM-BT015 · V2</p>
            </div>
          </div>
          <UBadge color="primary" variant="subtle">IT Issued form generator</UBadge>
        </div>
        <!-- Sections sit side by side on wide screens: the asset list needs the room while the
             user details are only two fields. Below xl they stack in the original order. -->
        <div class="grid gap-5 xl:grid-cols-[minmax(0,1.9fr)_minmax(0,1fr)] xl:items-start">
        <UCard :ui="{ header: 'px-4 py-4 sm:px-6', body: 'p-4 sm:p-6' }">
          <template #header>
            <div class="flex items-center justify-between gap-3">
              <div><h3 class="font-semibold text-highlighted">Section A — IT Asset List</h3></div>
              <UBadge color="neutral" variant="outline" class="whitespace-nowrap">{{ form.assets.length }} / 6</UBadge>
            </div>
          </template>
          <!-- One shared column title for the whole list: the labels belong to the list, not to each row,
               so a six-row form shows four headers once instead of four headers six times. -->
          <fieldset :disabled="generating" aria-label="IT asset list">
            <div class="hidden gap-2 px-1 pb-0.5 sm:grid sm:grid-cols-[1.75rem_repeat(4,minmax(0,1fr))_1.75rem]">
              <span class="text-xs font-medium text-highlighted dark:text-white">No.</span>
              <span v-for="col in cols" :key="col.key" class="text-xs font-medium text-highlighted dark:text-white">
                {{ col.label }}<span v-if="col.key === 'serialNo'" class="text-error" aria-hidden="true">*</span>
              </span>
              <span class="sr-only">Remove</span>
            </div>
            <TransitionGroup name="equip" tag="div" class="relative mt-2 space-y-2">
            <div
              v-for="(asset, index) in form.assets"
              :key="asset.key"
              class="relative grid grid-cols-1 gap-x-2 gap-y-1.5 rounded-md px-1 py-1.5 pr-9 transition-colors duration-150 hover:bg-elevated/40 focus-within:bg-elevated/40 sm:grid-cols-[1.75rem_repeat(4,minmax(0,1fr))_1.75rem] sm:items-center sm:pr-1"
            >
              <span class="grid size-5 shrink-0 place-items-center rounded-md bg-primary/10 text-[11px] font-semibold text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">{{ index + 1 }}</span>
              <label v-for="col in cols" :key="col.key" class="relative block min-w-0">
                <span class="mb-1 flex items-center gap-1.5 text-xs font-medium text-muted sm:hidden">
                  {{ col.label }}<span v-if="col.key === 'serialNo'" class="text-error" aria-hidden="true">*</span>
                  <!-- Mobile has no room for a chip in the gap, so the same action sits on the label line. -->
                  <button
                    v-if="isLinkable(col.key) && index > 0"
                    type="button"
                    class="-my-1 inline-flex size-5 items-center justify-center rounded-md text-muted transition hover:bg-elevated hover:text-primary"
                    :aria-label="`Copy ${col.label} from equipment ${index}`"
                    :title="`Same as equipment ${index}`"
                    @click="linkFromAbove(index, col.key)"
                  >
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" class="chain-link rotate-90 size-3.5" :class="justLinked === `${col.key}-${index}` ? 'text-primary' : ''" aria-hidden="true">
                      <path class="chain-half-left" d="M9 17H7A5 5 0 0 1 7 7h2" />
                      <path class="chain-half-right" d="M15 7h2a5 5 0 1 1 0 10h-2" />
                      <path class="chain-bar" d="M8 12h8" />
                    </svg>
                  </button>
                </span>
                <UInput :model-value="asset[col.key]" @update:model-value="(value: string) => onCellInput(asset, col.key, value)" :aria-label="`Equipment ${index + 1} ${col.label}`" :placeholder="col.placeholder" class="w-full" :maxlength="160" />
                <!-- Chain chip centred in the gap between this box and the same column one row above. -->
                <button
                  v-if="isLinkable(col.key) && index > 0"
                  type="button"
                  class="absolute -top-[19px] left-1/2 z-10 hidden size-[18px] -translate-x-1/2 items-center justify-center text-muted transition hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary sm:flex"
                  :aria-label="`Copy ${col.label} from equipment ${index}`"
                  :title="`Same as equipment ${index}`"
                  @click="linkFromAbove(index, col.key)"
                >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" class="chain-link rotate-90 size-3.5" :class="justLinked === `${col.key}-${index}` ? 'text-primary' : ''" aria-hidden="true">
                    <path class="chain-half-left" d="M9 17H7A5 5 0 0 1 7 7h2" />
                    <path class="chain-half-right" d="M15 7h2a5 5 0 1 1 0 10h-2" />
                    <path class="chain-bar" d="M8 12h8" />
                  </svg>
                  <!-- Firework splash, keyed so repeat clicks replay it from scratch. -->
                  <span v-if="burstKey === `${col.key}-${index}`" :key="burstId" class="pointer-events-none absolute inset-0" aria-hidden="true">
                    <span class="chain-burst-ring" />
                    <span
                      v-for="spark in SPARKS"
                      :key="spark.i"
                      class="chain-spark"
                      :style="{ '--angle': `${spark.angle}deg`, '--distance': `${spark.distance}px`, '--delay': `${spark.delay}ms`, '--size': `${spark.size}px` }"
                    />
                  </span>
                </button>
              </label>
              <UButton
                v-if="form.assets.length > 1"
                type="button"
                color="neutral"
                variant="ghost"
                size="xs"
                square
                class="trash-bin-hover absolute right-0 top-1 text-muted hover:text-error sm:static sm:justify-self-center"
                :aria-label="`Remove equipment ${index + 1}`"
                :title="`Remove equipment ${index + 1}`"
                @click="form.assets.splice(index, 1)"
              >
                <template #leading>
                  <TrashBinIcon class="size-4" data-slot="leadingIcon" />
                </template>
              </UButton>
            </div>
            </TransitionGroup>
          </fieldset>
          <div class="mt-2 flex justify-center px-1">
            <UButton
              type="button"
              variant="outline"
              color="neutral"
              :disabled="form.assets.length >= 6 || generating"
              class="gap-2 transition-colors duration-200 hover:border-primary/50 hover:bg-primary/5 hover:text-primary"
              @click="form.assets.push(makeRow())"
            >
              <span class="relative inline-flex items-center gap-1.5">
                <span class="relative inline-flex size-4 items-center justify-center">
                  <!-- ix:table-add-row-below, drawn inline. The ix Iconify collection does not
                       resolve at runtime in this offline build (provider 'none'), so the exact
                       glyph is embedded instead of depending on the icon bundle. -->
                  <svg viewBox="0 0 512 512" fill="currentColor" fill-rule="evenodd" clip-rule="evenodd" class="add-row-icon size-4" aria-hidden="true">
                    <path d="M277.333 277.333h-42.666v-64h-64v-42.666h64v-64h42.666v64h64v42.666h-64zm-192-64H128v-42.666H42.667V384h426.666V170.667H384v42.666h42.667v128H85.333z" />
                  </svg>
                  <span class="add-row-pulse" aria-hidden="true" />
                </span>
                Add equipment
              </span>
            </UButton>
          </div>
        </UCard>
        <UCard :ui="{ header: 'px-4 py-4 sm:px-6', body: 'p-4 sm:p-6' }">
          <template #header><h3 class="font-semibold text-highlighted">Section B — User Details</h3></template>
          <fieldset :disabled="generating" class="grid grid-cols-1 gap-4">
            <UFormField label="User" required>
              <SuggestInput :model-value="form.user" @update:model-value="(value: string) => { form.user = value.toUpperCase() }" v-model:source-hint="hints.user" :suggestions="suggestions.user" :context="[form.employeeNo]" header="GID List" placeholder="Search name or type manually" show-on-suggestions-update select-on-tab @fill="pickUser" />
            </UFormField>
            <UFormField label="Employee No" required>
              <SuggestInput :model-value="form.employeeNo" @update:model-value="(value: string) => { form.employeeNo = value.toUpperCase() }" v-model:source-hint="hints.employeeNo" :suggestions="suggestions.employeeNo" :context="[form.user]" header="GID List" placeholder="Search employee number" show-on-suggestions-update select-on-tab @fill="pickUser" />
            </UFormField>
          </fieldset>
          <div class="mt-2 min-h-6 text-xs text-muted" aria-live="polite">
            <span v-if="gidLoading">Loading GID suggestions…</span>
            <template v-else-if="gidError">{{ gidError }} <button type="button" class="text-primary underline" @click="loadGid">Retry</button></template>
            <span v-else>Select a suggestion to fill both fields. Manual entries are also supported.</span>
          </div>
        </UCard>
        </div>
        <div class="rounded-lg border border-default bg-elevated/40 p-4 text-sm text-muted">
          <p><span class="font-medium text-highlighted">Section C · Received By:</span> the name follows User; its date is set when you generate the PDF and remains editable.</p>
          <p class="mt-2">All other Section C fields and Section D retain the original template values. The eight digital signature fields remain available in the downloaded PDF.</p>
        </div>
        <div class="flex flex-wrap items-center justify-between gap-4 border-t border-default pt-5">
          <p class="text-xs text-muted">Download only · No record is saved to the database.</p>
          <UButton type="submit" icon="i-lucide-file-down" class="record-save-hover stable-hover-hitbox" :loading="generating" :disabled="generating">Generate PDF</UButton>
        </div>
        <p class="min-h-10 text-sm text-error" role="status">{{ message }}</p>
      </form>
    </template>
  </UDashboardPanel>
</template>

<style scoped>
/*
 * The chain is three paths, so its two halves can actually move: they draw toward each
 * other, hold for a beat, then let go with a small rebound while the centre bar squeezes.
 * That reads as a live link instead of the whole glyph squashing. Elements animate their own
 * translate/transform, so the svg's rotate-90 class is untouched. Vue renames scoped
 * keyframes with the component id, so the names stay local to this page.
 */
@keyframes chain-half-close-left {
  0%, 100% { translate: 0 0; }
  45%, 62% { translate: -1.3px 0; }
}
@keyframes chain-half-close-right {
  0%, 100% { translate: 0 0; }
  45%, 62% { translate: 1.3px 0; }
}
@keyframes chain-bar-squeeze {
  0%, 100% { transform: scaleX(1); opacity: 1; }
  45%, 62% { transform: scaleX(1.3); opacity: 0.85; }
}
/* A gentle whole-glyph swell plus a small twist makes the tug readable at 14px without
   distorting the shape. The base 90deg lives in the class so the glyph still stands vertical
   whenever the animation is switched off (reduced motion). */
@keyframes chain-link-swell {
  0%, 100% { scale: 1 1; rotate: 90deg; }
  45%, 62% { scale: 1.05 1.05; rotate: 97deg; }
}
.chain-link {
  animation: chain-link-swell 2.8s cubic-bezier(0.45, 0, 0.25, 1) infinite;
  transform-box: fill-box;
  transform-origin: center;
}
.chain-half-left,
.chain-half-right {
  animation: 2.8s cubic-bezier(0.45, 0, 0.25, 1) infinite;
}
.chain-half-left { animation-name: chain-half-close-left; }
.chain-half-right { animation-name: chain-half-close-right; }
.chain-bar {
  transform-box: fill-box;
  transform-origin: center;
  animation: chain-bar-squeeze 2.8s cubic-bezier(0.45, 0, 0.25, 1) infinite;
}
/*
 * Click splash: ten sparks fly out of the chip while one ring expands, all in the current
 * accent. Rendered only for 800ms by the burst state, so nothing is left in the DOM.
 */
.chain-burst-ring {
  position: absolute;
  inset: 0;
  border-radius: 9999px;
  border: 1px solid var(--ui-primary);
  animation: chain-burst-ring 520ms cubic-bezier(0.2, 0.7, 0.3, 1) forwards;
}
@keyframes chain-burst-ring {
  0% { transform: scale(0.55); opacity: 0.95; }
  100% { transform: scale(3.1); opacity: 0; }
}
.chain-spark {
  position: absolute;
  left: 50%;
  top: 50%;
  width: var(--size);
  height: var(--size);
  margin-left: calc(var(--size) / -2);
  margin-top: calc(var(--size) / -2);
  border-radius: 9999px;
  background: var(--ui-primary);
  opacity: 0;
  animation: chain-spark-fly 620ms cubic-bezier(0.15, 0.72, 0.3, 1) forwards;
  animation-delay: var(--delay);
}
@keyframes chain-spark-fly {
  0% { transform: rotate(var(--angle)) translateX(0) scale(1); opacity: 1; }
  70% { opacity: 0.9; }
  100% { transform: rotate(var(--angle)) translateX(var(--distance)) scale(0.2); opacity: 0; }
}
@media (prefers-reduced-motion: reduce) {
  .chain-link, .chain-half-left, .chain-half-right, .chain-bar { animation: none; }
  .chain-burst-ring, .chain-spark { display: none; }
}
/*
 * Add-equipment icon: the row is picked up, dropped in and settled, with a short accent
 * underline marking where it landed. The cycle is slow and off-beat so it reads as an
 * occasional hint rather than a blinking badge.
 */
.add-row-icon {
  transform-origin: center;
  animation: add-row-drop 4.2s cubic-bezier(0.34, 1.56, 0.64, 1) infinite;
}
@keyframes add-row-drop {
  0%, 60%, 100% { transform: translateY(0) scale(1); }
  66% { transform: translateY(-3px) scale(1.08); }
  76% { transform: translateY(1px) scale(0.95); }
  84% { transform: translateY(0) scale(1.03); }
  90% { transform: translateY(0) scale(1); }
}
.add-row-pulse {
  position: absolute;
  left: 50%;
  bottom: -3px;
  width: 12px;
  height: 2px;
  margin-left: -6px;
  border-radius: 9999px;
  background: var(--ui-primary);
  opacity: 0;
  animation: add-row-pulse 4.2s ease-out infinite;
}
@keyframes add-row-pulse {
  0%, 60%, 100% { opacity: 0; transform: scaleX(0.3); }
  68% { opacity: 0.95; transform: scaleX(1); }
  88% { opacity: 0; transform: scaleX(0.6); }
}
button:disabled .add-row-icon,
button:disabled .add-row-pulse { animation: none; }
button:disabled .add-row-pulse { opacity: 0; }
@media (prefers-reduced-motion: reduce) {
  .add-row-icon, .add-row-pulse { animation: none; }
  .add-row-pulse { display: none; }
}
/* Page-local entrance: native mount only, never keyed to inputs or reference-data loading.
   Backwards fill leaves no transformed containing block after the entrance finishes. */
.issued-page-motion > * {
  animation: issued-page-enter 540ms cubic-bezier(0.22, 1, 0.36, 1) backwards;
}
.issued-page-motion > :nth-child(2) { animation-delay: 60ms; }
.issued-page-motion > :nth-child(3) { animation-delay: 120ms; }
.issued-page-motion > :nth-child(4) { animation-delay: 180ms; }
.issued-page-motion > :nth-child(5) { animation-delay: 220ms; }
.issued-page-motion > :nth-child(6) { animation-delay: 250ms; }
@keyframes issued-page-enter {
  from { opacity: 0; transform: translateY(12px); }
  to { opacity: 1; transform: translateY(0); }
}
@media (prefers-reduced-motion: reduce), print {
  .issued-page-motion > * {
    animation: none !important;
    transform: none !important;
    opacity: 1;
  }
}
/*
 * Equipment rows: an added row unfolds from above while the rows below glide down, and a
 * removed row fades out while it is lifted out of the flow so the rest slide up instead of
 * the list snapping shut. Vue supplies the move class for the reflow.
 */
.equip-enter-active {
  transition: opacity 220ms ease-out, transform 280ms cubic-bezier(0.22, 1, 0.36, 1);
}
.equip-leave-active {
  position: absolute;
  left: 0;
  right: 0;
  transition: opacity 150ms ease-in, transform 150ms ease-in;
}
.equip-move {
  transition: transform 280ms cubic-bezier(0.22, 1, 0.36, 1);
}
.equip-enter-from {
  opacity: 0;
  transform: translateY(-10px) scale(0.98);
}
.equip-leave-to {
  opacity: 0;
  transform: translateY(-6px) scale(0.98);
}
@media (prefers-reduced-motion: reduce), print {
  .equip-enter-active,
  .equip-leave-active,
  .equip-move { transition: none; }
}
</style>
