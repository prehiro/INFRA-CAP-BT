<script setup lang="ts">
/**
 * Custom date picker for the logbook filter.
 *
 * WHY NOT THE NATIVE <input type="date">: the browser control cannot be styled to match the
 * app at all - it renders the OS date picker with its own chrome, its own colours and an
 * unstyleable calendar icon, and on this machine it shows the US "mm/dd/yyyy" order rather
 * than the Indonesian dd/mm/yyyy the sheet is written in. It looked like a foreign object
 * sitting between two themed dropdowns.
 *
 * This is the same idea as TimePicker: a themed popover driven by Nuxt UI's semantic tokens,
 * so it follows the user's accent colour and light/dark setting automatically.
 *
 * v-model is a bare "yyyy-mm-dd" or empty, matching what the filter state stores.
 */
const props = withDefaults(defineProps<{
  modelValue: string
  placeholder?: string
  /** The other end of the range, so days in between can be highlighted. */
  rangeStart?: string
  rangeEnd?: string
  name?: string
}>(), { placeholder: 'Pick a date', rangeStart: '', rangeEnd: '' })

const emit = defineEmits<{ 'update:modelValue': [string] }>()

const open = ref(false)

/** Parse "yyyy-mm-dd" as a LOCAL date. new Date('2026-10-04') is parsed as UTC and can land
 *  on the previous day in WIB, which is exactly the class of bug already fixed once in the
 *  entry form. */
function parseIso(s: string): Date | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(s ?? '').trim())
  if (!m) return null
  const d = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]))
  return Number.isNaN(d.getTime()) ? null : d
}

function iso(d: Date): string {
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July',
  'August', 'September', 'October', 'November', 'December']
const WEEKDAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

const selected = computed(() => parseIso(props.modelValue))
const today = new Date()

/** The month on display. Seeded from the selection so opening on a chosen date shows that
 *  month; falls back to today. */
const view = ref({ y: today.getFullYear(), m: today.getMonth() })
watch(open, (v) => {
  if (!v) return
  const base = selected.value ?? today
  view.value = { y: base.getFullYear(), m: base.getMonth() }
})

function shiftMonth(delta: number) {
  const d = new Date(view.value.y, view.value.m + delta, 1)
  view.value = { y: d.getFullYear(), m: d.getMonth() }
}

/** 42 cells (6 weeks) so the grid height never changes between months - a calendar that
 *  resizes when you page through it feels broken. */
const grid = computed(() => {
  const first = new Date(view.value.y, view.value.m, 1)
  const offset = (first.getDay() + 6) % 7          // Monday = 0
  const start = new Date(view.value.y, view.value.m, 1 - offset)
  const cells: { iso: string; day: number; inMonth: boolean; isToday: boolean; isStart: boolean; isEnd: boolean; between: boolean }[] = []
  for (let i = 0; i < 42; i++) {
    const d = new Date(start.getFullYear(), start.getMonth(), start.getDate() + i)
    const s = iso(d)
    cells.push({
      iso: s,
      day: d.getDate(),
      inMonth: d.getMonth() === view.value.m,
      isToday: s === iso(today),
      isStart: s === props.rangeStart,
      isEnd: s === props.rangeEnd,
      between: Boolean(props.rangeStart && props.rangeEnd && s > props.rangeStart && s < props.rangeEnd)
    })
  }
  return cells
})

function pick(s: string) {
  emit('update:modelValue', s)
  // Closing on pick is right for From/To in a filter: the change applies instantly (there is
  // no Apply button), so leaving the panel open would just obscure the result it produced.
  open.value = false
}

function clear() {
  emit('update:modelValue', '')
  open.value = false
}

function shiftDay(delta: number) {
  const base = selected.value ?? today
  pick(iso(new Date(base.getFullYear(), base.getMonth(), base.getDate() + delta)))
}

function display(): string {
  const d = selected.value
  if (!d) return ''
  return `${String(d.getDate()).padStart(2, '0')} ${MONTHS[d.getMonth()].slice(0, 3)} ${d.getFullYear()}`
}
</script>

<template>
  <UPopover v-model:open="open" :content="{ align: 'start' }" :_ui="{ content: 'p-0 w-max' }">
    <UButton
      :name="name"
      type="button"
      block
      variant="outline"
      class="justify-start gap-2 font-normal"
      :class="display() ? '' : 'text-muted'"
      :aria-label="`${placeholder}: ${display() || 'not set'}`"
    >
      <UIcon name="i-lucide-calendar" class="size-4 shrink-0 text-muted" />
      <span class="truncate font-normal" :class="display() ? '' : 'text-muted'">
        {{ display() || placeholder }}
      </span>
    </UButton>

    <template #content>
      <!-- Width lives here, not on the popover content: this picker is nested inside the
           filter popover and the vendor clamps a nested popover's content width, so w-64 on
           the content collapsed the grid to ~19px per day and two-digit numbers ran together. -->
      <div class="w-64 p-3" @keydown.left="shiftDay(-1)" @keydown.right="shiftDay(1)" @keydown.esc="open = false">
        <!-- Month header -->
        <div class="mb-2 flex items-center justify-between gap-1">
          <UButton icon="i-lucide-chevron-left" variant="ghost" size="xs" aria-label="Previous month" @click="shiftMonth(-1)" />
          <span class="text-sm font-semibold">{{ MONTHS[view.m] }} {{ view.y }}</span>
          <UButton icon="i-lucide-chevron-right" variant="ghost" size="xs" aria-label="Next month" @click="shiftMonth(1)" />
        </div>

        <div class="mb-1 grid grid-cols-7 gap-0.5">
          <span v-for="w in WEEKDAYS" :key="w" class="text-center text-[10px] font-medium uppercase tracking-wide text-muted">
            {{ w.slice(0, 1) }}{{ w.slice(1, 2).toLowerCase() }}
          </span>
        </div>

        <!-- Keyed on the month so paging slides the grid instead of snapping. -->
        <Transition name="cal" mode="out-in">
          <div :key="view.y + '-' + view.m" class="grid grid-cols-7 gap-0.5">
            <button
              v-for="c in grid" :key="c.iso"
              type="button"
              class="relative h-8 rounded-lg text-sm tabular-nums transition-colors duration-150"
              :class="[
                c.inMonth ? 'text-default' : 'text-muted/40',
                c.between ? 'bg-primary/10 text-primary' : '',
                c.isToday && !c.isStart && !c.isEnd ? 'ring-1 ring-inset ring-primary/60' : '',
                (c.isStart || c.isEnd) ? 'bg-primary font-semibold text-primary-contrast' : '',
                c.iso === modelValue ? 'bg-primary font-semibold text-primary-contrast' : '',
                !c.inMonth ? 'hover:bg-elevated' : 'hover:bg-elevated'
              ]"
              :aria-selected="c.iso === modelValue"
              @click="pick(c.iso)"
            >{{ c.day }}</button>
          </div>
        </Transition>

        <div class="mt-2 flex items-center justify-between border-t border-default pt-2">
          <UButton size="xs" variant="ghost" color="error" label="Clear" @click="clear" />
          <UButton size="xs" variant="ghost" label="Today" @click="pick(iso(today))" />
        </div>
      </div>
    </template>
  </UPopover>
</template>

<style scoped>
/* Month paging: the outgoing grid fades and slides one way, the incoming the other, so the
   direction of travel is legible. Transform only - never width/height. */
.cal-enter-active,
.cal-leave-active {
  transition: opacity 140ms ease, transform 140ms ease;
}
.cal-enter-from {
  opacity: 0;
  transform: translateX(14px);
}
.cal-leave-to {
  opacity: 0;
  transform: translateX(-14px);
}
@media (prefers-reduced-motion: reduce) {
  .cal-enter-active,
  .cal-leave-active {
    transition: none !important;
  }
}
</style>
