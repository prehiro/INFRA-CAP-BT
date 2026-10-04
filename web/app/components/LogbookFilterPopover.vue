<script setup lang="ts">
/**
 * Filter popover for the CCTV logbook.
 *
 * Deliberately a popover rather than a row of dropdowns in the toolbar: the toolbar already
 * carries Search, Add Row, Excel and Print, and three more inline controls would crowd it
 * and push the table narrower than it needs to be on a 1280px screen.
 *
 * APPLIES IMMEDIATELY (HIRO, 2026-10-04). There is no Apply button and no separate draft:
 * every control writes straight through to the parent's filter object, so the table, the
 * chips and the row counter update the moment a choice is made. That also removed a whole
 * class of bug this component used to have - a draft that could drift out of step with the
 * applied value, which is how the popover once showed old dates and an ON switch while the
 * chips were already gone. One state, one source of truth, nothing to reconcile.
 *
 * The popover stays OPEN after a change on purpose, so several filters can be combined
 * without reopening it; the chips under the toolbar are the record of what is active.
 */
export interface LogbookFilters {
  from: string
  to: string
  section: string
  pic: string
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

/** The single write path. Every control funnels through here. */
function set<K extends keyof LogbookFilters>(key: K, value: string) {
  emit('update:modelValue', { ...props.modelValue, [key]: value })
}

const activeCount = computed(() => {
  const f = props.modelValue
  let n = 0
  if (f.from || f.to) n++
  if (f.section) n++
  if (f.pic) n++
  return n
})

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
    if (r.from === props.modelValue.from && r.to === props.modelValue.to) return p.key
  }
  return (!props.modelValue.from && !props.modelValue.to) ? 'all' : ''
}

function applyPreset(key: string) {
  emit('update:modelValue', { ...props.modelValue, ...presetRange(key) })
}
</script>

<template>
  <UPopover :content="{ align: 'start' }" :_ui="{ content: 'p-0 w-80' }">
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
      <!-- Fixed height: a nested USelect renders its list inside this box, and letting the
           panel size to its content made the whole popover jump every time one opened. -->
      <div class="min-h-[19rem] p-4">
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

        <!-- Custom pickers instead of the native <input type="date">: the OS control cannot
             be styled to match the app and shows the US mm/dd/yyyy order on this machine,
             while the sheet is written dd/mm/yyyy. Each one is told the OTHER end of the
             range so the days in between light up, which makes a From..To band readable at
             a glance instead of two disconnected dates. -->
        <div class="grid grid-cols-2 gap-2">
          <UFormField label="From" name="f-from">
            <!-- BOTH ends are passed to BOTH pickers. The `between` highlight needs the two
                 endpoints to know what "in between" means; passing only the far end left the
                 From picker with no start, so the days inside the band never lit up. -->
            <DatePicker
              :model-value="modelValue.from" placeholder="Start date"
              :range-start="modelValue.from" :range-end="modelValue.to"
              @update:model-value="set('from', $event)"
            />
          </UFormField>
          <UFormField label="To" name="f-to">
            <DatePicker
              :model-value="modelValue.to" placeholder="End date"
              :range-start="modelValue.from" :range-end="modelValue.to"
              @update:model-value="set('to', $event)"
            />
          </UFormField>
        </div>

        <div class="mt-2 space-y-2">
          <UFormField label="Section" name="f-section">
            <USelect
              :model-value="modelValue.section" :items="sections" class="w-full"
              placeholder="All sections" @update:model-value="set('section', $event ?? '')"
            />
          </UFormField>
          <UFormField label="PIC Name" name="f-pic">
            <USelect
              :model-value="modelValue.pic" :items="pics" searchable class="w-full"
              placeholder="All requesters" @update:model-value="set('pic', $event ?? '')"
            />
          </UFormField>
        </div>

        <div class="mt-3 flex items-center justify-between gap-2 border-t border-default pt-3">
          <UButton size="xs" variant="ghost" color="error" label="Clear all" @click="emit('clear')" />
          <span class="text-[11px] text-muted">{{ matchCount }} of {{ totalCount }} rows match</span>
        </div>
      </div>
    </template>
  </UPopover>
</template>
