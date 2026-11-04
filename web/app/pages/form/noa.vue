<script setup lang="ts">
import { DEFAULT_NOA_INPUT, generateNoaPdf, noaMonth, validateNoaInput } from '~/utils/noaForm'
import { chooseNoaRangeDay, displayNoaDate, formatNoaDateRange, noaMonthGrid, parseNoaDateRange } from '~/utils/noaDateRange'
import type { NoaInput } from '~/utils/noaForm'
import templateUrl from '~/assets/forms/noa-r3-template.pdf?url'

const toast = useToast()
const form = reactive<NoaInput>({ ...DEFAULT_NOA_INPUT })
const whenOpen = ref(false)
const whenFrom = ref('')
const whenTo = ref('')
const whenStep = ref<'from' | 'to'>('from')
const today = new Date()
const whenView = ref({ year: today.getFullYear(), month: today.getMonth() })
const weekdays = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su']
const whenPanels = computed(() => [0, 1].map(offset => {
  const date = new Date(whenView.value.year, whenView.value.month + offset, 1)
  return {
    key: `${date.getFullYear()}-${date.getMonth()}`,
    title: new Intl.DateTimeFormat('id-ID', { month: 'long', year: 'numeric' }).format(date),
    days: noaMonthGrid(date.getFullYear(), date.getMonth(), whenFrom.value, whenTo.value)
  }
}))
const whenPreview = computed(() => formatNoaDateRange(whenFrom.value, whenTo.value))
watch(whenOpen, (open) => {
  if (!open) return
  const range = parseNoaDateRange(form.when)
  whenFrom.value = range?.from ?? ''
  whenTo.value = range?.to ?? ''
  whenStep.value = 'from'
  const base = range?.from ?? ''
  const month = base ? new Date(Number(base.slice(0, 4)), Number(base.slice(5, 7)) - 1, 1) : new Date()
  whenView.value = { year: month.getFullYear(), month: month.getMonth() }
})
function focusWhen(which: 'from' | 'to') {
  whenStep.value = which
  const selected = which === 'from' ? whenFrom.value : whenTo.value
  if (!selected) return
  whenView.value = { year: Number(selected.slice(0, 4)), month: Number(selected.slice(5, 7)) - 1 }
}
function shiftWhenMonth(delta: number) {
  const month = new Date(whenView.value.year, whenView.value.month + delta, 1)
  whenView.value = { year: month.getFullYear(), month: month.getMonth() }
}
function pickWhen(value: string) {
  const selection = chooseNoaRangeDay({ from: whenFrom.value, to: whenTo.value }, whenStep.value, value)
  whenFrom.value = selection.range.from
  whenTo.value = selection.range.to
  whenStep.value = selection.nextStep
}
function applyWhen() {
  if (!whenPreview.value) return
  form.when = whenPreview.value
  whenOpen.value = false
}
function clearWhen() {
  form.when = ''
  whenOpen.value = false
}
const generating = ref(false)
const errorMessage = ref('')
const currentMonth = ref(noaMonth(new Date()))
const descriptionFields = [
  { key: 'what', label: '1. What' }, { key: 'why', label: '2. Why' },
  { key: 'where', label: '3. Where' }, { key: 'when', label: '4. When' },
  { key: 'who', label: '5. Who' }
] as const
const fixedHow = [
  'Prepare tools And material', 'Follow safety rule', 'Check work area',
  'Install additional LAN', 'General cleaning work location after finish'
]

async function generate() {
  if (generating.value) return
  errorMessage.value = ''
  const validation = validateNoaInput(form)
  if (validation) { errorMessage.value = validation; return }
  const generatedAt = new Date()
  currentMonth.value = noaMonth(generatedAt)
  generating.value = true
  try {
    const response = await fetch(templateUrl)
    if (!response.ok) throw new Error('The NOA PDF master could not be loaded.')
    const bytes = await generateNoaPdf(new Uint8Array(await response.arrayBuffer()), { ...form }, generatedAt)
    const url = URL.createObjectURL(new Blob([new Uint8Array(bytes)], { type: 'application/pdf' }))
    const a = document.createElement('a')
    a.href = url
    a.download = `NOA-${String(generatedAt.getFullYear())}-${String(generatedAt.getMonth() + 1).padStart(2, '0')}.pdf`
    document.body.appendChild(a)
    a.click()
    a.remove()
    window.setTimeout(() => URL.revokeObjectURL(url), 60000)
    toast.add({ title: 'NOA PDF generated', description: 'The original one-page NOA layout is preserved.', color: 'primary', icon: 'i-lucide-file-check-2' })
  } catch (error: any) {
    errorMessage.value = error?.message || 'Unable to generate the NOA PDF.'
  } finally { generating.value = false }
}
</script>

<template>
  <!-- Issued Form uses 24px side gutters and max-w-6xl. Below xl its taller form
       has a 15px scroll track, so reserve that width here without forcing NOA to scroll. -->
  <UDashboardPanel id="noa-form" :ui="{ body: 'p-3 sm:pl-6 sm:pr-[39px] sm:pt-6 xl:pb-4 xl:pr-6' }">
    <template #header><PageHeader title="NOA" /></template>
    <template #body>
      <form class="noa-page-motion mx-auto flex w-full max-w-6xl flex-col gap-2" @submit.prevent="generate">
        <div class="mb-4 flex items-center gap-3">
          <span class="noa-title-icon grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">
            <RecordAddIcon size="lg" random-halo>
              <!-- Same lucide:file-text paths; thinner than the Iconify mask's fixed 2px stroke. -->
              <svg class="pc-add-tilt size-7" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" focusable="false">
                <path d="M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z" />
                <path d="M14 2v5a1 1 0 0 0 1 1h5M10 9H8m8 4H8m8 4H8" />
              </svg>
            </RecordAddIcon>
          </span>
          <div class="min-w-0">
            <h2 class="text-xl font-semibold text-highlighted">Notification of Alteration</h2>
            <p class="mt-1 text-sm text-muted">EHS - E - A5 - 301B (R3) · Original one-page master</p>
          </div>
        </div>

        <div class="grid gap-3 lg:grid-cols-2 lg:items-stretch">
          <UCard :ui="{ header: 'px-4 py-3', body: 'px-4 py-3' }">
            <template #header><h3 class="text-sm font-semibold text-highlighted">Alteration details</h3></template>
            <div class="space-y-2">
              <div class="grid grid-cols-[8rem_minmax(0,1fr)] items-center gap-2">
                <label class="text-sm font-medium" for="noa-month">For month of</label>
                <UInput id="noa-month" :model-value="currentMonth" disabled class="min-w-0 w-full" />
              </div>
              <div class="grid grid-cols-[8rem_minmax(0,1fr)] items-center gap-2">
                <label class="text-sm font-medium leading-tight" for="noa-changes">Please specify the changes <span class="text-error">*</span></label>
                <UInput id="noa-changes" v-model="form.changes" class="min-w-0 w-full" placeholder="Describe the alteration" :disabled="generating" />
              </div>
              <div class="grid grid-cols-[8rem_minmax(0,1fr)] items-center gap-2">
                <label class="text-sm font-medium" for="noa-waste">Waste</label>
                <UInput id="noa-waste" v-model="form.waste" class="min-w-0 w-full" :disabled="generating" />
              </div>
              <div class="grid grid-cols-[8rem_minmax(0,1fr)] items-center gap-2">
                <label class="text-sm font-medium" for="noa-po">PO NO <span class="text-error">*</span></label>
                <UInput id="noa-po" v-model="form.poNo" class="min-w-0 w-full" placeholder="Enter PO number" :disabled="generating" />
              </div>
            </div>
            <p class="mt-3 border-t border-default pt-3 text-xs text-muted">Month is set at generation · Waste can be edited or cleared</p>
          </UCard>

          <UCard class="noa-description-card" :ui="{ header: 'px-4 py-3', body: 'px-4 py-3' }">
            <template #header><h3 class="text-sm font-semibold text-highlighted">Description of changes <span class="font-normal text-muted">(5W, 1H)</span></h3></template>
            <div class="noa-description-fields space-y-2">
              <div v-for="field in descriptionFields" :key="field.key" class="grid grid-cols-[5rem_minmax(0,1fr)] items-center gap-2">
                <label class="text-sm font-medium" :for="`noa-${field.key}`">{{ field.label }} <span class="text-error">*</span></label>
                <UInput v-if="field.key !== 'when'" :id="`noa-${field.key}`" v-model="form[field.key]" class="min-w-0 w-full" :disabled="generating" />
                <UInput v-else :id="`noa-${field.key}`" v-model="form.when" class="min-w-0 w-full"
                  placeholder="23 Okt 2026 – 31 Des 2026" :disabled="generating">
                  <template #trailing>
                    <UPopover v-model:open="whenOpen" :content="{ align: 'end' }"
                      :ui="{ content: 'max-h-[var(--reka-popper-available-height)] overflow-y-auto' }">
                      <UButton type="button" size="xs" variant="ghost" color="neutral"
                        class="noa-range-trigger" aria-label="Select date range" :disabled="generating">
                        <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                          stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">
                          <rect width="18" height="18" x="3" y="3" rx="2" />
                          <path d="M16 2v3M3 9h18M8 2v3m9 8h-6m2 4H7m0-4h.01M17 17h.01" />
                        </svg>
                      </UButton>
                      <template #content>
                        <div class="w-[min(19rem,calc(100vw-2rem))] p-3 sm:w-[35rem]">
                          <div class="mb-3">
                            <p class="text-sm font-semibold text-highlighted">Work schedule</p>
                            <p class="text-xs text-muted">Choose a start date, then an end date.</p>
                          </div>
                          <div class="mb-3 grid grid-cols-2 gap-2">
                            <button type="button" class="noa-range-choice min-w-0 rounded-lg border px-3 py-2 text-left transition-colors"
                              :class="whenStep === 'from' ? 'border-primary/60 bg-primary/10' : 'border-default hover:bg-elevated'"
                              :aria-pressed="whenStep === 'from'" @click="focusWhen('from')">
                              <span class="block text-[11px] text-muted">Start date</span>
                              <span class="block truncate text-sm font-medium">{{ displayNoaDate(whenFrom) || 'Select date' }}</span>
                            </button>
                            <button type="button" class="noa-range-choice min-w-0 rounded-lg border px-3 py-2 text-left transition-colors"
                              :class="whenStep === 'to' ? 'border-primary/60 bg-primary/10' : 'border-default hover:bg-elevated'"
                              :aria-pressed="whenStep === 'to'" @click="focusWhen('to')">
                              <span class="block text-[11px] text-muted">End date</span>
                              <span class="block truncate text-sm font-medium">{{ displayNoaDate(whenTo) || 'Select date' }}</span>
                            </button>
                          </div>
                          <div class="mb-2 flex items-center justify-between">
                            <UButton type="button" size="xs" variant="ghost" color="neutral" class="noa-range-nav noa-range-prev" aria-label="Previous month" @click="shiftWhenMonth(-1)">
                              <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m15 18-6-6 6-6" /></svg>
                            </UButton>
                            <span class="text-xs text-muted">{{ whenStep === 'from' ? 'Select start date' : 'Select end date' }}</span>
                            <UButton type="button" size="xs" variant="ghost" color="neutral" class="noa-range-nav noa-range-next" aria-label="Next month" @click="shiftWhenMonth(1)">
                              <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m9 18 6-6-6-6" /></svg>
                            </UButton>
                          </div>
                          <div class="grid gap-3 sm:grid-cols-2">
                            <div v-for="(panel, index) in whenPanels" :key="panel.key" :class="index === 1 ? 'hidden sm:block' : ''">
                              <p class="mb-2 text-center text-sm font-semibold capitalize text-highlighted">{{ panel.title }}</p>
                              <div class="mb-1 grid grid-cols-7 gap-0.5">
                                <span v-for="weekday in weekdays" :key="weekday" class="text-center text-[10px] font-medium text-muted">{{ weekday }}</span>
                              </div>
                              <div class="grid grid-cols-7 gap-0.5">
                                <button v-for="day in panel.days" :key="day.iso" type="button"
                                  class="noa-range-day h-8 rounded-md text-xs tabular-nums transition-colors hover:bg-elevated focus-visible:outline-2 focus-visible:outline-primary"
                                  :class="[
                                    day.inMonth ? 'text-default' : 'text-muted/40',
                                    day.between ? 'bg-primary/10 text-primary' : '',
                                    day.isStart || day.isEnd ? 'bg-primary font-semibold text-primary-contrast hover:bg-primary' : ''
                                  ]"
                                  :aria-label="displayNoaDate(day.iso) || day.iso" :aria-pressed="day.isStart || day.isEnd"
                                  @click="pickWhen(day.iso)">{{ day.day }}</button>
                              </div>
                            </div>
                          </div>
                          <div class="sticky bottom-0 z-10 -mx-3 mt-3 flex items-center justify-between gap-2 border-t border-default bg-default px-3 pb-1 pt-3">
                            <UButton type="button" size="xs" variant="ghost" color="error" label="Clear"
                              class="trash-bin-hover" @click="clearWhen">
                              <template #leading><TrashBinIcon class="size-4" data-slot="leadingIcon" /></template>
                            </UButton>
                            <div class="flex items-center gap-2">
                              <UButton type="button" size="xs" variant="ghost" color="neutral" label="Cancel"
                                class="cancel-hover" @click="whenOpen = false">
                                <template #leading>
                                  <svg class="size-4" data-slot="leadingIcon" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                                    stroke-width="2" stroke-linecap="round" aria-hidden="true" focusable="false">
                                    <path d="M18 6 6 18M6 6l12 12" />
                                  </svg>
                                </template>
                              </UButton>
                              <UButton type="button" size="xs" label="Apply" :disabled="!whenPreview"
                                class="record-save-hover stable-hover-hitbox" @click="applyWhen">
                                <template #leading>
                                  <svg class="size-4" data-slot="leadingIcon" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                                    stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">
                                    <path d="m20 6-11 11-5-5" />
                                  </svg>
                                </template>
                              </UButton>
                            </div>
                          </div>
                        </div>
                      </template>
                    </UPopover>
                  </template>
                </UInput>
              </div>
              <div class="rounded-lg border border-default bg-elevated/50 px-3 py-2">
                <p class="text-xs font-semibold text-highlighted">6. How <span class="font-normal text-muted">· Fixed from the master</span></p>
                <p class="mt-1 text-xs leading-4 text-muted">{{ fixedHow.join(' · ') }}</p>
              </div>
            </div>
          </UCard>
        </div>

        <div class="flex min-h-8 items-center justify-between gap-3">
          <p role="alert" class="min-w-0 text-sm text-error">{{ errorMessage }}</p>
          <UButton type="submit" icon="i-lucide-file-down" :loading="generating" :disabled="generating" class="record-save-hover stable-hover-hitbox shrink-0">Generate PDF</UButton>
        </div>
      </form>
    </template>
  </UDashboardPanel>
</template>

<style scoped>
/* Reveal only the three stable form sections on route entry, like Issued Form. */
.noa-page-motion > * {
  animation: noa-page-enter 520ms cubic-bezier(0.22, 1, 0.36, 1) backwards;
}
.noa-page-motion > :nth-child(2) { animation-delay: 70ms; }
.noa-page-motion > :nth-child(3) { animation-delay: 140ms; }
@keyframes noa-page-enter {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: none; }
}
@media (prefers-reduced-motion: reduce), print {
  .noa-page-motion > * { animation: none !important; opacity: 1; transform: none !important; }
}

/* Keep the issued-form title position and 24px gap even on short screens.
   Reclaim height inside NOA's right card, without affecting other pages. */
@media (max-height: 600px) {
  .noa-description-card :deep([data-slot="header"]),
  .noa-description-card :deep([data-slot="body"]) { padding-block: 12px; }
  .noa-description-fields > :not(:last-child) { margin-bottom: 4px; }
}

/* Only the NOA range picker moves; the form and button hitboxes stay put. */
.noa-range-trigger :deep(svg),
.noa-range-nav :deep(svg) { transition: transform 180ms ease-out; }
.noa-range-choice,
.noa-range-day { transition: transform 180ms ease-out, background-color 150ms ease, color 150ms ease, border-color 150ms ease; }

@media (hover: hover) and (pointer: fine) {
  .noa-range-trigger:not(:disabled):hover :deep(svg) { transform: scale(1.12) rotate(-7deg); }
  .noa-range-choice:hover,
  .noa-range-day:hover { transform: translateY(-2px); }
  .noa-range-prev:not(:disabled):hover :deep(svg) { transform: translateX(-2px); }
  .noa-range-next:not(:disabled):hover :deep(svg) { transform: translateX(2px); }
}
.noa-range-trigger:not(:disabled):focus-visible :deep(svg) { transform: scale(1.12) rotate(-7deg); }
.noa-range-choice:focus-visible,
.noa-range-day:focus-visible { transform: translateY(-2px); }
.noa-range-prev:not(:disabled):focus-visible :deep(svg) { transform: translateX(-2px); }
.noa-range-next:not(:disabled):focus-visible :deep(svg) { transform: translateX(2px); }
.noa-range-choice:active,
.noa-range-day:active { transform: translateY(0) scale(.96); }

@media (prefers-reduced-motion: reduce), print {
  .noa-range-trigger :deep(svg),
  .noa-range-nav :deep(svg),
  .noa-range-choice,
  .noa-range-day { transition: none !important; transform: none !important; }
}
</style>
