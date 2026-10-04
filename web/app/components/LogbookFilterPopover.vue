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

/**
 * The popover's open state, bound so "Clear all" can close it.
 *
 * The popover deliberately STAYS OPEN after every other control, so several filters can be
 * combined without reopening it. "Clear all" is the one exception: once everything is
 * cleared there is nothing left to combine, and leaving an empty panel floating over the
 * restored table reads like the click missed. HIRO asked for this explicitly
 * ("ketika user klik clear popup filter close").
 */
const open = ref(false)

function clearAll() {
  emit('clear')
  open.value = false
}

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
  <UPopover v-model:open="open" :content="{ align: 'start' }" :_ui="{ content: 'p-0 w-80' }">
    <!-- The trigger, wrapped so the counter can hang off the corner WITHOUT joining the flow.

         WHY THE WRAPPER, from two bugs HIRO reported:
         (1) "the Filter text disappeared". UButton renders its `label` into the DEFAULT slot,
             so passing any default-slot content OVERWRITES the label. The old markup put a
             UBadge in that slot, which is why the word vanished as soon as a filter was
             active. The label is now left alone and the counter is a sibling, never a child
             of the button.
         (2) "make sure no CSS shifts". The old UBadge sat in the button's own flex row, so
             activating a filter made the button grow wider and shoved Excel and Add Record
             sideways. The counter is `position: absolute`, which removes it from the flow
             entirely - the button is now exactly the same width whether the count is 0 or 3.
         The wrapper is `relative inline-flex`, the counter is centred on the top-right corner
         and carries a ring in the card's own colour so it reads as floating above the edge
         rather than as a notch cut out of it. Opacity + translateY only, no scale, for the
         same re-rasterisation reason as the rest of the app's animations. -->
    <div class="relative inline-flex">
      <UButton
        icon="i-lucide-list-filter"
        label="Filter"
        variant="outline"
        :class="activeCount ? 'border-primary text-primary' : ''"
      />
      <Transition name="infra-badge">
        <span
          v-if="activeCount"
          aria-hidden="true"
          class="infra-filter-count pointer-events-none absolute -right-1.5 -top-1.5 grid h-[18px] min-w-[18px]
                 place-items-center rounded-full bg-primary px-1 text-[10px] font-bold
                 leading-none text-inverted ring-2 ring-elevated tabular-nums"
        >{{ activeCount }}</span>
      </Transition>
    </div>

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
          <UButton size="xs" variant="ghost" color="error" label="Clear all" @click="clearAll" />
          <span class="text-[11px] text-muted">{{ matchCount }} of {{ totalCount }} rows match</span>
        </div>
      </div>
    </template>
  </UPopover>
</template>

<style scoped>

/* Breathing glow around the welcome avatar.
   "Breathing" means the halo expands outward and fades back in, on a slow cycle, rather than
   blinking — the easing is a symmetric ease-in-out so the inhale and the exhale take the same
   time and the loop has no visible seam.

   THE COLOUR IS NOT HARDCODED. `color-mix()` is fed `var(--ui-primary)`, the same variable
   the stars and the glow orb use, so the halo follows the user's accent automatically. A
   literal hex here would have been the one thing in this banner that stopped matching the
   theme. */
.infra-avatar-glow {
  animation: infra-breathe 4.5s cubic-bezier(0.4, 0, 0.6, 1) infinite;
}

@keyframes infra-breathe {
  0%,
  100% {
    box-shadow:
      0 0 0 0.25rem color-mix(in oklab, var(--ui-primary) 16%, transparent),
      0 0 0 0 color-mix(in oklab, var(--ui-primary) 0%, transparent);
  }
  50% {
    box-shadow:
      0 0 0 0.3rem color-mix(in oklab, var(--ui-primary) 28%, transparent),
      0 0 26px 9px color-mix(in oklab, var(--ui-primary) 42%, transparent);
  }
}
</style>
