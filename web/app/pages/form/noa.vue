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
  <UDashboardPanel id="noa-form" :ui="{ body: 'p-4 sm:p-6' }">
    <template #header><PageHeader title="NOA" /></template>
    <template #body>
      <form class="mx-auto w-full max-w-6xl space-y-5" @submit.prevent="generate">
        <div class="flex flex-wrap items-center gap-3">
          <span class="noa-title-icon grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">
            <RecordAddIcon name="i-lucide-file-text" random-halo />
          </span>
          <div>
            <h2 class="text-xl font-semibold text-highlighted">Notification of Alteration</h2>
            <p class="mt-1 text-sm text-muted">EHS - E - A5 - 301B (R3) · PDF from the original NOA master</p>
          </div>
        </div>

        <div class="grid gap-5 xl:grid-cols-2 xl:items-start">
          <UCard :ui="{ header: 'px-4 py-4 sm:px-6', body: 'p-4 sm:p-6' }">
            <template #header><h3 class="font-semibold text-highlighted">Alteration details</h3></template>
            <div class="space-y-4">
              <div>
                <label class="mb-1.5 block text-sm font-medium" for="noa-month">For month of</label>
                <UInput id="noa-month" :model-value="currentMonth" disabled class="w-full" />
                <p class="mt-1 text-xs text-muted">Set automatically from the local date when you generate the PDF.</p>
              </div>
              <div>
                <label class="mb-1.5 block text-sm font-medium" for="noa-changes">Please specify the changes <span class="text-error">*</span></label>
                <UInput id="noa-changes" v-model="form.changes" class="w-full" placeholder="Describe the alteration" :disabled="generating" />
              </div>
              <div>
                <label class="mb-1.5 block text-sm font-medium" for="noa-waste">Waste</label>
                <UInput id="noa-waste" v-model="form.waste" class="w-full" :disabled="generating" />
                <p class="mt-1 text-xs text-muted">Prefilled from the master; you may edit or clear it.</p>
              </div>
              <div>
                <label class="mb-1.5 block text-sm font-medium" for="noa-po">PO NO <span class="text-error">*</span></label>
                <UInput id="noa-po" v-model="form.poNo" class="w-full" placeholder="Enter PO number" :disabled="generating" />
              </div>
            </div>
          </UCard>

          <UCard :ui="{ header: 'px-4 py-4 sm:px-6', body: 'p-4 sm:p-6' }">
            <template #header><h3 class="font-semibold text-highlighted">Description of changes (5W, 1H)</h3></template>
            <div class="space-y-4">
              <div v-for="field in descriptionFields" :key="field.key">
                <label class="mb-1.5 block text-sm font-medium" :for="`noa-${field.key}`">{{ field.label }} <span class="text-error">*</span></label>
                <UInput :id="`noa-${field.key}`" v-model="form[field.key]" class="w-full" :disabled="generating" />
              </div>
              <div class="rounded-lg border border-default bg-elevated/50 p-3">
                <p class="text-sm font-medium text-highlighted">6. How <span class="font-normal text-muted">(fixed in the master)</span></p>
                <p v-for="line in fixedHow" :key="line" class="mt-1 text-sm text-muted">{{ line }}</p>
              </div>
            </div>
          </UCard>
        </div>

        <div class="flex flex-wrap items-center justify-between gap-3">
          <p role="alert" class="min-h-5 text-sm text-error">{{ errorMessage }}</p>
          <UButton type="submit" icon="i-lucide-file-down" :loading="generating" :disabled="generating" class="record-save-hover">Generate PDF</UButton>
        </div>
      </form>
    </template>
  </UDashboardPanel>
</template>
