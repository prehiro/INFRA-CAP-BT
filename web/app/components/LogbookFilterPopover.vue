<script setup lang="ts">
/**
 * Filter popover for the CCTV logbook.
 *
 * Deliberately a popover rather than a row of dropdowns in the toolbar: the toolbar already
 * carries Search, Add Row, Export and Print, and three more inline controls would crowd it
 * and push the table narrower than it needs to be on a 1280px screen.
 *
 * Options are derived from the rows that are actually loaded, not from a hard-coded list, so
 * "Section" always offers exactly the sections that exist and "PIC Name" exactly the people
 * who have signed something.
 */
export interface LogbookFilters {
  from: string
  to: string
  section: string
  pic: string
  unsignedOnly: boolean
}

const props = defineProps<{
  modelValue: LogbookFilters
  sections: string[]
  pics: string[]
  /** How many rows survive the current filter - shown live so the user is never guessing. */
  matchCount: number
  totalCount: number
}>()

const emit = defineEmits<{ 'update:modelValue': [LogbookFilters]; clear: [] }>()

const draft = ref<LogbookFilters>({ ...props.modelValue })
const open = ref(false)

// The draft always mirrors the APPLIED value. It used to re-seed only on open, which left
// the popover showing stale controls after "Clear all" while the chips were already gone and
// the table had changed - three parts of the screen disagreeing about the same state.
// Syncing unconditionally is safe: editing the draft does not touch modelValue, so a
// half-finished edit is never clobbered, and applying simply re-seeds it with what was just
// applied.
watch(open, (v) => { if (v) draft.value = { ...props.modelValue } })
watch(() => props.modelValue, (v) => { draft.value = { ...v } }, { deep: true })

const PRESETS = [
  { key: 'all', label: 'All time' },
  { key: 'today', label: 'Today' },
  { key: 'week', label: 'This week' },
  { key: 'month', label: 'This month' }
] as const

function iso(d: Date): string {
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

function presetRange(key: string): { from: string; to: string } {
  const now = new Date()
  if (key === 'today') return { from: iso(now), to: iso(now) }
  if (key === 'week') {
    // Monday-start week, which is what an Indonesian office calendar uses.
    const d = new Date(now)
    const dow = (d.getDay() + 6) % 7
    d.setDate(d.getDate() - dow)
    return { from: iso(d), to: iso(now) }
  }
  if (key === 'month') {
    const first = new Date(now.getFullYear(), now.getMonth(), 1)
    return { from: iso(first), to: iso(now) }
  }
  return { from: '', to: '' }
}

function whichPreset(): string {
  for (const p of PRESETS) {
    if (p.key === 'all') continue
    const r = presetRange(p.key)
    if (r.from === draft.value.from && r.to === draft.value.to) return p.key
  }
  return (!draft.value.from && !draft.value.to) ? 'all' : ''
}

function applyPreset(key: string) {
  const r = presetRange(key)
  draft.value = { ...draft.value, from: r.from, to: r.to }
  emit('update:modelValue', { ...draft.value })
}

function apply() {
  emit('update:modelValue', { ...draft.value })
}

/** How many filters are currently APPLIED (not draft) - drives the badge on the trigger. */
const activeCount = computed(() => {
  const f = props.modelValue
  let n = 0
  if (f.from || f.to) n++
  if (f.section) n++
  if (f.pic) n++
  if (f.unsignedOnly) n++
  return n
})

function isDirty(): boolean {
  return props.sections.length > 0 || props.pics.length > 0
}
</script>

<template>
  <UPopover v-model:open="open" :content="{ align: 'start' }" :_ui="{ content: 'p-0 w-80' }">
    <UButton
      icon="i-lucide-list-filter"
      label="Filter"
      variant="outline"
      :class="activeCount ? 'border-primary text-primary' : ''"
    >
      <template v-if="activeCount">
        <UBadge color="primary" variant="solid" size="sm" class="ml-1">{{ activeCount }}</UBadge>
      </template>
    </UButton>

    <template #content>
      <div v-if="isDirty()" class="p-4">
        <!-- Quick ranges first: these are what people actually reach for, and they should be
             one click rather than two date pickers. -->
        <p class="mb-2 text-[11px] font-semibold uppercase tracking-wide text-muted">Quick range</p>
        <div class="flex flex-wrap gap-1.5">
          <UButton
            v-for="p in PRESETS" :key="p.key"
            size="xs"
            :variant="whichPreset() === p.key ? 'solid' : 'soft'"
            :color="whichPreset() === p.key ? 'primary' : 'neutral'"
            :label="p.label"
            @click="applyPreset(p.key)"
          />
        </div>

        <div class="my-3 border-t border-default" />

        <div class="grid grid-cols-2 gap-2">
          <UFormField label="From" name="f-from">
            <UInput v-model="draft.from" type="date" class="w-full" />
          </UFormField>
          <UFormField label="To" name="f-to">
            <UInput v-model="draft.to" type="date" class="w-full" />
          </UFormField>
        </div>

        <div class="mt-2 space-y-2">
          <UFormField label="Section" name="f-section">
            <USelect
              v-model="draft.section" :items="sections" value-key="" class="w-full"
              placeholder="All sections"
            />
          </UFormField>
          <UFormField label="PIC Name" name="f-pic">
            <USelect
              v-model="draft.pic" :items="pics" searchable value-key="" class="w-full"
              placeholder="All requesters"
            />
          </UFormField>
        </div>

        <label class="mt-3 flex cursor-pointer items-center justify-between gap-3 rounded-lg border border-default px-3 py-2 transition-colors hover:bg-elevated">
          <span class="text-sm">Unsigned only</span>
          <USwitch v-model="draft.unsignedOnly" />
        </label>

        <div class="mt-3 flex items-center justify-between gap-2 border-t border-default pt-3">
          <UButton size="xs" variant="ghost" color="error" label="Clear all" @click="emit('clear')" />
          <UButton size="xs" label="Apply" @click="apply" />
        </div>

        <p class="mt-2 text-center text-[11px] text-muted">
          {{ matchCount }} of {{ totalCount }} rows match
        </p>
      </div>
      <div v-else class="p-4 text-center text-sm text-muted">Loading options...</div>
    </template>
  </UPopover>
</template>
