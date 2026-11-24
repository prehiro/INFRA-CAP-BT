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
const draft = reactive<UpsInput>({ brand: '', model: '', serialNumber: '', location: '', assetType: ASSET_FIXED })

/**
 * The two asset classifications, in the order the form offers them. Kept as constants so the radio
 * group, the empty-draft default and the details dialog cannot drift apart on the spelling.
 */
const ASSET_FIXED = 'Fixed Asset'
const ASSET_NON_FIXED = 'Non Fixed Asset'
const ASSET_TYPES = [
  { value: ASSET_FIXED, label: 'Fixed Asset', hint: 'Capitalised and tracked in the asset register', icon: 'i-lucide-landmark' },
  { value: ASSET_NON_FIXED, label: 'Non Fixed Asset', hint: 'Expensed or consumed, not capitalised', icon: 'i-lucide-receipt' }
] as const

const showDelete = ref(false)
const deleteTarget = ref<UpsUnit | null>(null)

const showView = ref(false)
const viewTarget = ref<UpsUnit | null>(null)

/** "18 Nov 2026, 14:00" - the register's registration stamp, read-only in the details dialog. */
function formatStamp(value: string): string {
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return '-'
  return d.toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

const visible = computed(() => {
  const q = query.value.trim().toLowerCase()
  if (!q) return units.value
  return units.value.filter((u) =>
    [u.brand, u.model, u.serialNumber, u.location, u.assetType].some((v) => (v || '').toLowerCase().includes(q))
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
  Object.assign(draft, { brand: '', model: '', serialNumber: '', location: '', assetType: ASSET_FIXED })
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
    location: unit.location,
    // Rows saved before the column existed come back empty; treat those as the default rather than
    // leaving the radio group with nothing selected.
    assetType: unit.assetType || ASSET_FIXED
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
      location: draft.location.trim(),
      assetType: draft.assetType
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

function openView(unit: UpsUnit) {
  viewTarget.value = unit
  showView.value = true
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
              <!-- title-boot: shared boot sequence (plate spring + ring ping + scanline) and the
                   .pl-title-glyph wrapper that carries the ignition, so the icon below keeps its own
                   3.6s pc-add-tilt sway. Defined once in assets/css/main.css. -->
              <span class="title-boot grid size-12 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/20" aria-hidden="true">
                <RecordAddIcon size="lg" random-halo>
                  <span class="pl-title-glyph">
                    <UpsIcon class="pc-add-tilt size-8" />
                  </span>
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
                  <!-- Sized, not wrapping: the badge is the widest thing in the row and a two-line
                       "Non Fixed Asset" would make every row taller to fit one value. -->
                  <th class="w-32 px-3 py-2.5 text-left font-semibold">Asset type</th>
                  <th class="w-28 px-3 py-2.5 text-right font-semibold">Actions</th>
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
                  <!-- Same badge as the details dialog, so the register and the dialog cannot drift on
                       colour: primary for capitalised units, amber for the expensed ones. Rows saved
                       before the column existed render as Fixed Asset, which is what the migration
                       backfilled them to. -->
                  <td class="px-3 py-2.5">
                    <span class="inline-flex items-center rounded-md px-1.5 py-0.5 text-[11px] font-medium ring-1 ring-inset"
                      :class="unit.assetType === ASSET_NON_FIXED ? 'bg-amber-400/10 text-amber-500 ring-amber-400/25' : 'bg-primary/10 text-primary ring-primary/25'">
                      {{ unit.assetType || ASSET_FIXED }}
                    </span>
                  </td>
                  <td class="px-3 py-1.5">
                    <div class="flex justify-end gap-1">
                      <!-- Native xs ghost buttons, the same construction (and icon animations) as the PC
                           Ledger row actions: three reactive vendor buttons per row is wasted work. -->
                      <button
                        type="button"
                        data-slot="base"
                        class="inline-flex items-center rounded-md p-1 text-xs font-medium text-primary outline-primary/30 transition-colors hover:bg-primary/10 active:bg-primary/10 focus-visible:outline-3 ups-eye-hover"
                        :aria-label="`View details for UPS ${unit.serialNumber}`"
                        title="View details"
                        @click="openView(unit)"
                      >
                        <span class="ups-eye">
                          <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                            <path class="ups-eye-outline" d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z" />
                            <circle class="ups-eye-iris" cx="12" cy="12" r="2.5" />
                          </svg>
                        </span>
                      </button>
                      <button v-if="isAdmin"
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
                        v-if="isAdmin"
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
                  <td :colspan="7" class="px-3 py-12 text-center text-sm text-muted">Loading UPS register…</td>
                </tr>
                <tr v-else-if="!visible.length">
                  <td :colspan="7" class="px-3 py-12 text-center text-sm text-muted">
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
              <div class="space-y-5 px-5 pb-4 pt-4">
                <!-- Classification leads the form: it decides how the unit is carried, and each card
                     says what the choice MEANS rather than leaving two bare labels to guess between.
                     Same two-card radio idiom as the Access Point type picker, so it reads as the app's
                     own rather than a new invention. Real radio inputs (sr-only) inside their labels keep
                     arrow-key navigation and the focus ring free. -->
                <fieldset :disabled="busy">
                  <legend class="mb-2 flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wider text-muted">
                    <UIcon name="i-lucide-tag" class="size-3.5" />
                    Asset classification
                  </legend>
                  <div class="grid gap-2 sm:grid-cols-2">
                    <label
                      v-for="opt in ASSET_TYPES"
                      :key="opt.value"
                      class="flex min-w-0 items-start gap-3 rounded-xl border p-3 transition-colors focus-within:ring-2 focus-within:ring-primary/40"
                      :class="[
                        draft.assetType === opt.value ? 'border-primary/60 bg-primary/10' : 'border-default bg-elevated/40 hover:bg-elevated',
                        busy ? 'cursor-not-allowed opacity-60' : 'cursor-pointer'
                      ]"
                    >
                      <input v-model="draft.assetType" type="radio" name="ups-asset-type" :value="opt.value" class="sr-only" />
                      <span
                        class="mt-0.5 grid size-8 shrink-0 place-items-center rounded-lg ring-1 ring-inset transition-colors"
                        :class="draft.assetType === opt.value ? 'bg-primary/15 text-primary ring-primary/30' : 'bg-default/60 text-muted ring-default'"
                      >
                        <UIcon :name="opt.icon" class="size-4" />
                      </span>
                      <span class="min-w-0">
                        <span class="block text-sm font-semibold leading-tight" :class="draft.assetType === opt.value ? 'text-primary' : 'text-highlighted'">{{ opt.label }}</span>
                        <span class="mt-0.5 block text-xs leading-snug text-muted">{{ opt.hint }}</span>
                      </span>
                    </label>
                  </div>
                </fieldset>

                <p class="flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wider text-muted">
                  <UIcon name="i-lucide-battery-charging" class="size-3.5" />
                  Unit details
                </p>
                <!-- 2x2 rather than two full-width rows: S/N and Location are short values, so pairing
                     them keeps the dialog two rows of inputs instead of four. -->
                <div class="grid gap-3 sm:grid-cols-2">
                  <fieldset :disabled="busy">
                    <legend class="mb-1.5 text-sm font-medium text-highlighted">Brand</legend>
                    <UInput v-model="draft.brand" class="w-full" maxlength="60" placeholder="e.g. APC" :disabled="busy" />
                  </fieldset>
                  <fieldset :disabled="busy">
                    <legend class="mb-1.5 text-sm font-medium text-highlighted">Model</legend>
                    <UInput v-model="draft.model" class="w-full" maxlength="120" placeholder="e.g. Smart-UPS SRT 5000VA 230V" :disabled="busy" />
                  </fieldset>
                  <fieldset :disabled="busy">
                    <legend class="mb-1.5 text-sm font-medium text-highlighted">S/N</legend>
                    <UInput v-model="draft.serialNumber" class="w-full" maxlength="60" placeholder="e.g. AS2143171426" :disabled="busy" />
                  </fieldset>
                  <fieldset :disabled="busy">
                    <legend class="mb-1.5 text-sm font-medium text-highlighted">Location</legend>
                    <UInput v-model="draft.location" class="w-full" maxlength="160" placeholder="e.g. CAP Server Room B2L2" :disabled="busy" />
                  </fieldset>
                </div>

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

      <!-- ---------------- read-only details ---------------- -->
      <UModal v-model:open="showView" :ui="{ content: 'w-full max-w-lg network-modal-glow' }">
        <template #content>
          <div v-if="viewTarget" class="flex flex-col">
            <div class="flex items-center gap-3 border-b border-default px-5 py-4">
              <span class="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary ring-1 ring-inset ring-primary/20">
                <UpsIcon class="size-5" />
              </span>
              <div class="min-w-0 flex-1">
                <h2 class="text-sm font-semibold leading-tight">{{ viewTarget.brand }} {{ viewTarget.model }}</h2>
                <p class="text-xs text-muted">S/N {{ viewTarget.serialNumber }} · UPS details</p>
              </div>
              <button
                type="button"
                class="grid size-8 shrink-0 place-items-center rounded-lg text-muted transition-colors hover:bg-error/10 hover:text-error focus-visible:outline-2 focus-visible:outline-error"
                aria-label="Close UPS details"
                @click="showView = false"
              >
                <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M18 6 6 18M6 6l12 12" /></svg>
              </button>
            </div>

            <div class="px-5 pb-5 pt-4">
              <p class="mb-3 flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wider text-muted">
                <UIcon name="i-lucide-battery-charging" class="size-3.5" />
                Unit details
              </p>
              <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 rounded-lg bg-elevated/60 p-3 text-xs ring-1 ring-inset ring-default">
                <dt class="text-muted">Brand</dt>
                <dd class="truncate font-medium">{{ viewTarget.brand }}</dd>
                <dt class="text-muted">Model</dt>
                <dd class="truncate">{{ viewTarget.model }}</dd>
                <dt class="text-muted">S/N</dt>
                <dd class="truncate font-mono tabular-nums">{{ viewTarget.serialNumber }}</dd>
                <dt class="text-muted">Location</dt>
                <dd class="truncate">{{ viewTarget.location }}</dd>
                <dt class="text-muted">Asset type</dt>
                <dd class="truncate">
                  <span class="rounded-md px-1.5 py-0.5 text-[11px] font-medium ring-1 ring-inset"
                    :class="viewTarget.assetType === ASSET_NON_FIXED ? 'bg-amber-400/10 text-amber-500 ring-amber-400/25' : 'bg-primary/10 text-primary ring-primary/25'">
                    {{ viewTarget.assetType || ASSET_FIXED }}
                  </span>
                </dd>
                <dt class="text-muted">Registered</dt>
                <dd class="truncate tabular-nums">{{ formatStamp(viewTarget.createdAt) }}</dd>
              </dl>
            </div>

            <div class="flex items-center justify-end gap-2 border-t border-default px-5 py-3">
              <UButton color="neutral" variant="ghost" label="Close" @click="showView = false" />
            </div>
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

<style scoped>
/* The eye glances sideways on hover — the same family as the PC Ledger's view action, kept on the
   iris only so the button itself stays flat (HIRO rejects hover motion on the button chrome). */
.ups-eye { display: inline-flex; }
.ups-eye-iris {
  transform-box: fill-box;
  transform-origin: center;
}
@media (hover: hover) and (pointer: fine) {
  .ups-eye-hover:not(:disabled):hover .ups-eye-iris {
    animation: ups-eye-glance 820ms cubic-bezier(0.33, 1, 0.68, 1);
  }
}
@keyframes ups-eye-glance {
  0%, 100% { transform: translateX(0); }
  38% { transform: translateX(1.7px); }
  72% { transform: translateX(-1.3px); }
}
@media (prefers-reduced-motion: reduce) {
  .ups-eye-hover .ups-eye-iris { animation: none !important; }
}
</style>
