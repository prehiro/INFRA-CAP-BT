<script setup lang="ts">
/**
 * Time picker: a trigger button plus a popover with separate hour and minute columns.
 * Built by hand rather than using <input type="time"> because the native dropdown cannot
 * be styled to match the app, and because the paper form wants a plain "HH:mm" with the
 * date supplied separately.
 *
 * v-model is the time ONLY ("HH:mm", 24h). The caller owns the date - on the CCTV sheet
 * the date is always today, so it is composed at save time (see composeDateTime in
 * cctvacc.vue). Keeping the date out of this component means editing one field never
 * silently rewrites another.
 *
 * Picking a MINUTE closes the popover (HIRO's request): the hour column stays open until
 * a minute is chosen, so the common case is open -> hour -> minute -> done with no need to
 * click outside. Picking an hour deliberately does NOT close, because a minute still has
 * to be chosen.
 */
const props = withDefaults(defineProps<{
  modelValue: string | null | undefined
  placeholder?: string
  /** Minute granularity. 5 (HIRO's choice) gives the 12 values a person actually reads off
   *  a wall clock; the previous default of 15 was too coarse for a logbook. */
  minuteStep?: number
  name?: string
}>(), { placeholder: 'Select time', minuteStep: 5, name: undefined })

const emit = defineEmits<{ 'update:modelValue': [string] }>()

const HOURS = Array.from({ length: 24 }, (_, i) => String(i).padStart(2, '0'))
const MINUTES = Array.from({ length: 60 / props.minuteStep }, (_, i) => String(i * props.minuteStep).padStart(2, '0'))

const open = ref(false)
const hour = ref('09')
const minute = ref('00')
const hourCol = ref<HTMLElement | null>(null)
const minCol = ref<HTMLElement | null>(null)

/** Accept "HH:mm", "9:5", "09:30", or a full "dd/MM/yy HH:mm" / ISO string and pull out the time. */
function parseTime(v: unknown): { h: string; m: string } | null {
  const s = String(v ?? '').trim()
  if (!s) return null
  const m = s.match(/(\d{1,2}):(\d{2})/)
  if (!m) return null
  return { h: m[1]!.padStart(2, '0'), m: m[2]! }
}

watch(() => props.modelValue, (v) => {
  const t = parseTime(v)
  if (t) {
    hour.value = t.h
    // Snap an off-step minute (e.g. a legacy "09:07" row) DOWN to the nearest step so the
    // wheel always has something selected instead of appearing empty.
    const step = props.minuteStep
    const snapped = Math.floor(Number(t.m) / step) * step
    minute.value = String(Math.min(snapped, 59)).padStart(2, '0')
  }
}, { immediate: true })

/** Centre the chosen value in its column when the popover opens. Without this the wheels
 *  always open scrolled to the top of the day, so picking 13:45 meant scrolling past 13
 *  rows of hours every single time. */
watch(open, async (v) => {
  if (!v) return
  await nextTick()
  for (const col of [hourCol.value, minCol.value]) {
    const sel = col?.querySelector('[data-selected="true"]') as HTMLElement | null
    if (!col || !sel) continue
    col.scrollTop = sel.offsetTop - col.clientHeight / 2 + sel.clientHeight / 2
  }
})

function commit(next: { h?: string; m?: string }, close = false) {
  if (next.h !== undefined) hour.value = next.h
  if (next.m !== undefined) minute.value = next.m
  emit('update:modelValue', `${hour.value}:${minute.value}`)
  if (close) open.value = false
}

function period(): string {
  return Number(hour.value) < 12 ? 'AM' : 'PM'
}

function display(): string {
  const t = parseTime(props.modelValue)
  if (!t) return ''
  return `${t.h}:${t.m}`
}
</script>

<template>
  <UPopover v-model:open="open" :content="{ align: 'start' }" :_ui="{ content: 'p-0 overflow-hidden' }">
    <UButton
      :name="name"
      type="button"
      block
      variant="outline"
      class="justify-start gap-2 font-normal"
      :class="display() ? '' : 'text-muted'"
      :aria-label="`${placeholder}: ${display() || 'not set'}`"
    >
      <UIcon name="i-lucide-clock" class="size-4 shrink-0 text-muted" />
      <span class="truncate font-mono tabular-nums" :class="display() ? '' : 'text-muted'">
        {{ display() || placeholder }}
      </span>
    </UButton>

    <template #content>
      <div class="w-56" @keydown.esc="open = false">
        <div class="flex items-baseline justify-between border-b border-default px-3 py-2">
          <span class="text-[11px] font-medium uppercase tracking-wide text-muted">Selected</span>
          <span class="font-mono text-lg font-semibold tabular-nums text-primary">
            {{ hour }}:{{ minute }}<span class="ml-1 text-xs font-normal text-muted">{{ period() }}</span>
          </span>
        </div>

        <div class="flex divide-x divide-default">
          <div ref="hourCol" class="max-h-44 w-[5.5rem] overflow-y-auto py-1">
            <button v-for="h in HOURS" :key="h" type="button"
                    :data-selected="h === hour"
                    class="block w-full px-3 py-1.5 text-left font-mono text-sm tabular-nums transition-colors hover:bg-elevated"
                    :class="h === hour ? 'bg-primary/15 font-semibold text-primary' : ''"
                    @click="commit({ h })">{{ h }}</button>
          </div>
          <div ref="minCol" class="max-h-44 w-[5.5rem] overflow-y-auto py-1">
            <button v-for="m in MINUTES" :key="m" type="button"
                    :data-selected="m === minute"
                    class="block w-full px-3 py-1.5 text-left font-mono text-sm tabular-nums transition-colors hover:bg-elevated"
                    :class="m === minute ? 'bg-primary/15 font-semibold text-primary' : ''"
                    @click="commit({ m }, true)">{{ m }}</button>
          </div>
        </div>
      </div>
    </template>
  </UPopover>
</template>
