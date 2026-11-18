<script setup lang="ts">
import type { UpsInput, UpsUnit } from '~/types'

/**
 * UPS register: brand, model, serial and where the unit stands.
 *
 * The sheet this register comes from ("reff/Screenshot 2026-11-09") also has a UPS Condition block -
 * Physical Condition and Lamp Indicator - and HIRO asked for the register WITHOUT it. Condition is
 * therefore not part of the model at all: no column, no field, no hidden input.
 */
const { isAdmin } = useAuth()
const toast = useToast()

const units = ref<UpsUnit[]>([])
const loading = ref(true)
const busy = ref(false)
const loadError = ref('')
const query = ref('')

const showEditor = ref(false)
const editingId = ref<number | null>(null)
const formError = ref('')
const draft = reactive<UpsInput>({ brand: '', model: '', serialNumber: '', location: '' })

const showDelete = ref(false)
const deleteTarget = ref<UpsUnit | null>(null)

const visible = computed(() => {
  const q = query.value.trim().toLowerCase()
  if (!q) return units.value
  return units.value.filter((u) =>
    [u.brand, u.model, u.serialNumber, u.location].some((v) => v.toLowerCase().includes(q))
  )
})

const serialCount = computed(() => new Set(units.value.map((u) => u.serialNumber)).size)

function message(e: unknown): string {
  const m = (e as { data?: { message?: string } })?.data?.message
  return m || (e as Error)?.message || 'Something went wrong.'
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    units.value = await apiListUps()
  } catch (e) {
    loadError.value = message(e)
  } finally {
    loading.value = false
  }
}

function openAdd() {
  if (!isAdmin.value) return
  editingId.value = null
  Object.assign(draft, { brand: '', model: '', serialNumber: '', location: '' })
  formError.value = ''
  showEditor.value = true
}

function openEdit(unit: UpsUnit) {
  if (!isAdmin.value) return
  editingId.value = unit.id
  Object.assign(draft, {
    brand: unit.brand,
    model: unit.model,
    serialNumber: unit.serialNumber,
    location: unit.location
  })
  formError.value = ''
  showEditor.value = true
}

async function save() {
  if (!isAdmin.value || busy.value) return
  if (!draft.brand.trim()) { formError.value = 'Brand is required.'; return }
  if (!draft.model.trim()) { formError.value = 'Model is required.'; return }
  if (!draft.serialNumber.trim()) { formError.value = 'Serial number is required.'; return }
  if (!draft.location.trim()) { formError.value = 'Location is required.'; return }

  busy.value = true
  formError.value = ''
  try {
    const payload: UpsInput = {
      brand: draft.brand.trim(),
      model: draft.model.trim(),
      serialNumber: draft.serialNumber.trim(),
      location: draft.location.trim()
    }
    if (editingId.value === null) {
      const saved = await apiCreateUps(payload)
      units.value.push(saved)
      toast.add({ title: 'UPS added', color: 'primary' })
    } else {
      const saved = await apiUpdateUps(editingId.value, payload)
      const idx = units.value.findIndex((u) => u.id === editingId.value)
      if (idx >= 0) units.value[idx] = saved
      toast.add({ title: 'UPS updated', color: 'primary' })
    }
    showEditor.value = false
  } catch (e) {
    // The API refuses a duplicate serial; show its sentence instead of inventing one.
    formError.value = message(e)
  } finally {
    busy.value = false
  }
}

function askDelete(unit: UpsUnit) {
  if (!isAdmin.value) return
  deleteTarget.value = unit
  showDelete.value = true
}

async function confirmDelete() {
  const unit = deleteTarget.value
  if (!unit || busy.value) return
  busy.value = true
  try {
    await apiDeleteUps(unit.id)
    units.value = units.value.filter((u) => u.id !== unit.id)
    showDelete.value = false
    deleteTarget.value = null
    toast.add({ title: 'UPS deleted', color: 'primary' })
  } catch (e) {
    toast.add({ title: 'Could not delete UPS', description: message(e), color: 'error' })
  } finally {
    busy.value = false
  }
}

onMounted(load)
</script>

<template>
  <UDashboardPanel id="device-ups" :ui="{ body: 'flex min-h-0 flex-col p-4 sm:p-6' }">
    <template #header><PageHeader title="UPS" /></template>
    <template #body>
      <div class="mx-auto flex min-h-0 w-full max-w-[1400px] flex-1 flex-col gap-4">

        <div class="flex flex-wrap items-center gap-x-4 gap-y-3 rounded-lg border border-default bg-elevated p-4">
          <div class="flex min-w-0 items-center gap-3">
            <span class="grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">
              <UIcon name="i-lucide-battery-charging" class="size-6" />
            </span>
            <div class="min-w-0">
              <h2 class="text-lg font-bold tracking-wide text-highlighted">UPS Register</h2>
              <p class="text-xs text-muted">
                {{ units.length }} unit{{ units.length === 1 ? '' : 's' }} · {{ serialCount }} serial number{{ serialCount === 1 ? '' : 's' }}
              </p>
            </div>
          </div>

          <div class="ml-auto flex flex-wrap items-center gap-2">
            <UInput v-model="query" icon="i-lucide-search" placeholder="Search brand, model, S/N, location" class="w-64" />
            <UButton v-if="isAdmin" icon="i-lucide-plus" :disabled="busy" @click="openAdd">Add UPS</UButton>
          </div>
        </div>

        <p v-if="loadError" role="alert" class="rounded-lg border border-error/30 bg-error/10 px-3 py-2 text-sm text-error">
          {{ loadError }}
        </p>

        <div class="min-h-0 flex-1 overflow-hidden rounded-xl border border-default bg-elevated">
          <div class="max-h-[calc(100vh-16rem)] overflow-auto">
            <table class="w-full min-w-[860px] border-collapse text-sm">
              <thead class="sticky top-0 z-10 bg-elevated">
                <tr class="border-b border-default text-[11px] uppercase tracking-wider text-muted">
                  <th class="w-16 px-3 py-2.5 text-left font-semibold">No</th>
                  <th class="px-3 py-2.5 text-left font-semibold">Brand</th>
                  <th class="px-3 py-2.5 text-left font-semibold">Model</th>
                  <th class="px-3 py-2.5 text-left font-semibold">SN</th>
                  <th class="px-3 py-2.5 text-left font-semibold">Location</th>
                  <th v-if="isAdmin" class="w-28 px-3 py-2.5 text-right font-semibold">Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(unit, index) in visible" :key="unit.id" class="border-b border-default/60 last:border-0 odd:bg-default/30 hover:bg-primary/5">
                  <td class="px-3 py-2 tabular-nums text-muted">{{ index + 1 }}</td>
                  <td class="px-3 py-2 font-medium text-highlighted">{{ unit.brand }}</td>
                  <td class="px-3 py-2 text-default">{{ unit.model }}</td>
                  <td class="px-3 py-2 font-mono text-xs text-default">{{ unit.serialNumber }}</td>
                  <td class="px-3 py-2 text-default">{{ unit.location }}</td>
                  <td v-if="isAdmin" class="px-3 py-2">
                    <div class="flex justify-end gap-1">
                      <UButton icon="i-lucide-pencil" color="neutral" variant="ghost" size="xs" :aria-label="`Edit ${unit.serialNumber}`" :disabled="busy" @click="openEdit(unit)" />
                      <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="xs" :aria-label="`Delete ${unit.serialNumber}`" :disabled="busy" @click="askDelete(unit)" />
                    </div>
                  </td>
                </tr>
                <tr v-if="!loading && !visible.length">
                  <td :colspan="isAdmin ? 6 : 5" class="px-3 py-10 text-center text-sm text-muted">
                    {{ query ? `No UPS matches "${query}".` : 'No UPS registered yet.' }}
                  </td>
                </tr>
                <tr v-if="loading">
                  <td :colspan="isAdmin ? 6 : 5" class="px-3 py-10 text-center text-sm text-muted">Loading UPS register…</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <UModal v-if="isAdmin" v-model:open="showEditor" :title="editingId === null ? 'Add UPS' : 'Edit UPS'" :description="editingId === null ? 'Register a UPS unit.' : 'Update this unit’s details.'" :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-lg network-modal-glow', body: 'p-4 sm:p-5' }">
        <template #body>
          <form class="space-y-4" @submit.prevent="save">
            <div class="grid gap-4 sm:grid-cols-2">
              <fieldset :disabled="busy">
                <legend class="mb-1.5 text-sm font-medium text-highlighted">Brand</legend>
                <UInput v-model="draft.brand" class="w-full" maxlength="60" placeholder="e.g. APC" :disabled="busy" />
              </fieldset>
              <fieldset :disabled="busy">
                <legend class="mb-1.5 text-sm font-medium text-highlighted">Model</legend>
                <UInput v-model="draft.model" class="w-full" maxlength="120" placeholder="e.g. Smart-UPS SRT 5000VA 230V" :disabled="busy" />
              </fieldset>
            </div>
            <fieldset :disabled="busy">
              <legend class="mb-1.5 text-sm font-medium text-highlighted">S/N</legend>
              <UInput v-model="draft.serialNumber" class="w-full" maxlength="60" placeholder="e.g. AS2143171426" :disabled="busy" />
            </fieldset>
            <fieldset :disabled="busy">
              <legend class="mb-1.5 text-sm font-medium text-highlighted">Location</legend>
              <UInput v-model="draft.location" class="w-full" maxlength="160" placeholder="e.g. CAP Server Room B2L2" :disabled="busy" />
            </fieldset>
            <p role="alert" class="min-h-5 text-sm text-error">{{ formError }}</p>
            <div class="flex justify-end gap-2 border-t border-default pt-4">
              <UButton type="button" color="red" variant="ghost" icon="basil:cancel-outline" class="cancel-hover" :disabled="busy" @click="showEditor = false">Cancel</UButton>
              <UButton type="submit" :icon="editingId === null ? 'i-lucide-plus' : 'i-lucide-pencil'" :loading="busy" :disabled="busy">{{ editingId === null ? 'Add UPS' : 'Save changes' }}</UButton>
            </div>
          </form>
        </template>
      </UModal>

      <UModal v-if="isAdmin" v-model:open="showDelete" title="Delete UPS" :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-md', body: 'p-5' }">
        <template #body>
          <p class="text-sm text-default">
            Remove <span class="font-semibold text-highlighted">{{ deleteTarget?.brand }} {{ deleteTarget?.model }}</span>
            (S/N {{ deleteTarget?.serialNumber }}) from the register? This cannot be undone.
          </p>
          <div class="mt-5 flex justify-end gap-2">
            <UButton color="neutral" variant="ghost" :disabled="busy" @click="showDelete = false">Cancel</UButton>
            <UButton color="error" icon="i-lucide-trash-2" :loading="busy" :disabled="busy" @click="confirmDelete">Delete</UButton>
          </div>
        </template>
      </UModal>
    </template>
  </UDashboardPanel>
</template>
