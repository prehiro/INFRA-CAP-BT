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
  <UDashboardPanel :ui="{ body: 'p-6' }">
    <template #header><PageHeader title="UPS" /></template>

    <template #body>
      <div class="space-y-6">
        <!-- Register header: same shape as the other Device Ledger sheets (title mark left, actions right). -->
        <div class="anim-fade-up rounded-lg border border-default bg-elevated p-4">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div class="flex min-w-0 items-center gap-3">
              <span class="grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">
                <RecordAddIcon size="lg" random-halo>
                  <UpsIcon class="pc-add-tilt size-8" />
                </RecordAddIcon>
              </span>
              <div class="min-w-0">
                <h1 class="text-lg font-bold tracking-wide">UPS Register</h1>
                <p class="text-xs text-muted">
                  Uninterruptible power supplies
                  <span v-if="!loading"> · {{ units.length }} unit{{ units.length === 1 ? '' : 's' }}</span>
                </p>
              </div>
            </div>

            <div class="flex flex-wrap items-center gap-2">
              <UInput v-model="query" icon="i-lucide-search" placeholder="Search brand, model, S/N, location..." class="w-72" :ui="{ base: 'h-9' }">
                <template v-if="query" #trailing>
                  <UButton color="neutral" variant="link" icon="i-lucide-x" size="xs" aria-label="Clear search" @click="query = ''" />
                </template>
              </UInput>

              <UButton
                v-if="isAdmin"
                color="primary"
                label="Add UPS" class="record-add-hover stable-hover-hitbox"
                @click="openAdd"
              >
                <template #leading><ChannelAddIcon /></template>
              </UButton>
            </div>
          </div>
        </div>

        <UAlert v-if="loadError" color="error" variant="soft" icon="i-lucide-triangle-alert" :title="loadError" />

        <div class="anim-fade-up overflow-hidden rounded-xl border border-default bg-elevated">
          <div class="max-h-[70vh] overflow-y-auto overflow-x-auto">
            <table class="w-full min-w-[880px] border-collapse text-sm">
              <thead class="sticky top-0 z-10 bg-elevated">
                <tr class="border-b border-default text-[11px] uppercase tracking-wider text-muted">
                  <th class="w-16 px-3 py-2.5 text-left font-semibold">No</th>
                  <th class="px-3 py-2.5 text-left font-semibold">Brand</th>
                  <th class="px-3 py-2.5 text-left font-semibold">Model</th>
                  <th class="px-3 py-2.5 text-left font-semibold">SN</th>
                  <th class="px-3 py-2.5 text-left font-semibold">Location</th>
                  <th v-if="isAdmin" class="w-24 px-3 py-2.5 text-right font-semibold">Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="(unit, index) in visible"
                  :key="unit.id"
                  class="border-b border-default/60 transition-colors last:border-0 hover:bg-primary/5"
                >
                  <td class="px-3 py-2.5 tabular-nums text-muted">{{ index + 1 }}</td>
                  <td class="px-3 py-2.5 font-medium text-highlighted">{{ unit.brand }}</td>
                  <td class="px-3 py-2.5 text-default">{{ unit.model }}</td>
                  <td class="px-3 py-2.5 font-mono text-xs text-default">{{ unit.serialNumber }}</td>
                  <td class="px-3 py-2.5 text-default">{{ unit.location }}</td>
                  <td v-if="isAdmin" class="px-3 py-1.5">
                    <div class="flex justify-end gap-1">
                      <!-- Native xs ghost buttons, the same construction (and icon animations) as the PC
                           Ledger row actions: three reactive vendor buttons per row is wasted work. -->
                      <button
                        type="button"
                        data-slot="base"
                        class="inline-flex items-center rounded-md p-1 text-xs font-medium text-default transition-colors hover:bg-elevated active:bg-elevated outline-inverted/25 focus-visible:outline-3 disabled:cursor-not-allowed disabled:opacity-75 pencil-hover"
                        :aria-label="`Edit UPS ${unit.serialNumber}`"
                        title="Edit"
                        :disabled="busy"
                        @click="openEdit(unit)"
                      >
                        <PencilIcon class="size-4" data-slot="leadingIcon" />
                      </button>
                      <button
                        type="button"
                        data-slot="base"
                        class="inline-flex items-center rounded-md p-1 text-xs font-medium text-error transition-colors hover:bg-error/10 active:bg-error/10 outline-error/25 focus-visible:outline-3 disabled:cursor-not-allowed disabled:opacity-75 trash-bin-hover"
                        :aria-label="`Delete UPS ${unit.serialNumber}`"
                        title="Delete"
                        :disabled="busy"
                        @click="askDelete(unit)"
                      >
                        <TrashBinIcon class="size-4" data-slot="leadingIcon" />
                      </button>
                    </div>
                  </td>
                </tr>

                <tr v-if="loading">
                  <td :colspan="isAdmin ? 6 : 5" class="px-3 py-12 text-center text-sm text-muted">Loading UPS register…</td>
                </tr>
                <tr v-else-if="!visible.length">
                  <td :colspan="isAdmin ? 6 : 5" class="px-3 py-12 text-center text-sm text-muted">
                    {{ query ? `No UPS matches "${query}".` : 'No UPS registered yet.' }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div v-if="!loading && visible.length" class="flex items-center justify-between gap-3 border-t border-default bg-default/30 px-4 py-2.5 text-xs text-muted">
            <span>{{ visible.length }} {{ visible.length === 1 ? 'unit' : 'units' }}</span>
            <span class="tabular-nums">Showing {{ visible.length }} of {{ units.length }}</span>
          </div>
        </div>
      </div>

      <!-- ---------------- add / edit ---------------- -->
      <UModal v-if="isAdmin" v-model:open="showEditor" :dismissible="!busy" :close="!busy" :ui="{ content: 'w-full max-w-2xl network-modal-glow' }">
        <template #content>
          <div class="flex flex-col">
            <div class="flex items-center gap-3 border-b border-default px-5 py-4">
              <span class="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary ring-1 ring-inset ring-primary/20">
                <RecordAddIcon :name="editingId !== null ? 'i-lucide-pencil' : 'channel-add'" />
              </span>
              <div class="min-w-0 flex-1">
                <h2 class="text-sm font-semibold leading-tight">{{ editingId !== null ? 'Edit UPS' : 'New UPS' }}</h2>
                <p class="text-xs text-muted">
                  {{ editingId !== null ? 'Changes are written to the register immediately.' : 'Add a UPS unit to the register.' }}
                </p>
              </div>
              <button
                type="button"
                class="grid size-8 shrink-0 place-items-center rounded-lg text-muted transition-colors hover:bg-error/10 hover:text-error focus-visible:outline-2 focus-visible:outline-error disabled:cursor-not-allowed disabled:opacity-50"
                aria-label="Close UPS form"
                :disabled="busy"
                @click="showEditor = false"
              >
                <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M18 6 6 18M6 6l12 12" /></svg>
              </button>
            </div>

            <form @submit.prevent="save">
              <div class="space-y-4 px-5 pb-4 pt-4">
                <p class="flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wider text-muted">
                  <UIcon name="i-lucide-battery-charging" class="size-3.5" />
                  Unit details
                </p>
                <div class="grid gap-3 sm:grid-cols-2">
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

                <!-- Reserved, so a validation message never makes the dialog grow. -->
                <p role="alert" class="min-h-5 text-xs text-error">{{ formError }}</p>
              </div>

              <div class="flex items-center justify-end gap-2 border-t border-default px-5 py-3">
                <UButton color="red" variant="ghost" icon="basil:cancel-outline" label="Cancel" class="cancel-hover" :disabled="busy" @click="showEditor = false" />
                <UButton
                  color="primary"
                  :class="editingId !== null ? 'record-save-hover' : 'channel-add-hover'"
                  :label="editingId !== null ? 'Save changes' : 'Add UPS'"
                  :loading="busy"
                  @click="save"
                >
                  <template #leading>
                    <SaveIcon v-if="editingId !== null" />
                    <ChannelAddIcon v-else />
                  </template>
                </UButton>
              </div>
            </form>
          </div>
        </template>
      </UModal>

      <!-- ---------------- delete ---------------- -->
      <UModal v-if="isAdmin" v-model:open="showDelete" :dismissible="!busy" :close="!busy" :ui="{ content: 'max-w-md' }">
        <template #content>
          <!-- Same construction as the other Device Ledger delete dialogs: a clipping wrapper, a danger
               wash bleeding in from the corner, the row identity on a raised box above it, footer band. -->
          <div class="overflow-hidden rounded-xl">
            <div class="relative overflow-hidden px-5 pb-5 pt-5">
              <div aria-hidden="true" class="pointer-events-none absolute -right-16 -top-24 size-48 rounded-full bg-error/20 blur-3xl" />
              <div class="relative flex items-start gap-3">
                <span class="grid size-11 shrink-0 place-items-center rounded-full bg-error/10 text-error ring-1 ring-inset ring-error/25">
                  <UIcon name="i-lucide-trash-2" class="size-5" />
                </span>
                <div class="min-w-0">
                  <h2 class="text-sm font-semibold">Delete this UPS?</h2>
                  <p class="mt-1 text-xs text-muted">
                    The unit disappears from the register. This cannot be undone from the UI.
                  </p>
                </div>
              </div>

              <dl v-if="deleteTarget" class="relative mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 rounded-lg bg-elevated/60 p-3 text-xs ring-1 ring-inset ring-default">
                <dt class="text-muted">Brand</dt>
                <dd class="truncate font-medium">{{ deleteTarget.brand }}</dd>
                <dt class="text-muted">Model</dt>
                <dd class="truncate">{{ deleteTarget.model }}</dd>
                <dt class="text-muted">S/N</dt>
                <dd class="truncate font-mono tabular-nums">{{ deleteTarget.serialNumber }}</dd>
                <dt class="text-muted">Location</dt>
                <dd class="truncate">{{ deleteTarget.location }}</dd>
              </dl>
            </div>

            <div class="flex items-center justify-end gap-2 bg-elevated/40 px-5 py-4">
              <UButton color="neutral" variant="ghost" label="Cancel" :disabled="busy" @click="showDelete = false" />
              <UButton color="error" class="record-delete-hover trash-bin-hover" label="Delete unit" :loading="busy" @click="confirmDelete">
                <template #leading>
                  <TrashBinIcon class="size-5" data-slot="leadingIcon" />
                </template>
              </UButton>
            </div>
          </div>
        </template>
      </UModal>
    </template>
  </UDashboardPanel>
</template>
