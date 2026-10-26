<script setup lang="ts">
import { DEFAULT_NOA_WASTE, generateNoaPdf, noaMonth, validateNoaInput } from '~/utils/noaForm'
import type { NoaInput } from '~/utils/noaForm'
import templateUrl from '~/assets/forms/noa-r3-template.pdf?url'

const toast = useToast()
const form = reactive<NoaInput>({ changes: '', waste: DEFAULT_NOA_WASTE, poNo: '', what: '', why: '', where: '', when: '', who: '' })
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
      <form class="mx-auto flex w-full max-w-6xl flex-col gap-2" @submit.prevent="generate">
        <div class="mb-4 flex items-center gap-3">
          <span class="noa-title-icon grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">
            <RecordAddIcon name="i-lucide-file-text" random-halo />
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
                <UInput :id="`noa-${field.key}`" v-model="form[field.key]" class="min-w-0 w-full" :disabled="generating" />
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
          <UButton type="submit" icon="i-lucide-file-down" :loading="generating" :disabled="generating" class="record-save-hover shrink-0">Generate PDF</UButton>
        </div>
      </form>
    </template>
  </UDashboardPanel>
</template>

<style scoped>
/* Keep the issued-form title position and 24px gap even on short screens.
   Reclaim height inside NOA's right card, without affecting other pages. */
@media (max-height: 600px) {
  .noa-description-card :deep([data-slot="header"]),
  .noa-description-card :deep([data-slot="body"]) { padding-block: 12px; }
  .noa-description-fields > :not(:last-child) { margin-bottom: 4px; }
}
</style>
