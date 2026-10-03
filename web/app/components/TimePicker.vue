<script setup lang="ts">
/**
 * Compact time picker: a button that shows the current time and opens a popover with
 * separate hour and minute wheels. Built by hand rather than using <input type="time">
 * because the native control's dropdown cannot be styled to match the app, and because
 * the paper form wants a plain "HH:mm" with the date supplied separately.
 *
 * v-model is the time ONLY ("HH:mm", 24h). The caller owns the date - on the CCTV sheet
 * the date is always today, so it is composed at save time (see composeDateTime in
 * cctvacc.vue). Keeping the date out of this component means editing one field never
 * silently rewrites another.
 */
const props = withDefaults(defineProps<{
  modelValue: string | null | undefined
  placeholder?: string
  minuteStep?: number
  name?: string
}>(), { placeholder: 'Select time', minuteStep: 15, name: undefined })

const emit = defineEmits<{ 'update:modelValue': [string] }>()

const HOURS = Array.from({ length: 24 }, (_, i) => String(i).padStart(2, '0'))
const MINUTES = Array.from({ length: 60 / props.minuteStep }, (_, i) => String(i * props.minuteStep).padStart(2, '0'))

const open = ref(false)
const hour = ref('09')
const minute = ref('00')

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

function commit(h: string, m: string) {
  hour.value = h
  minute.value = m
  emit('update:modelValue', `${h}:${m}`)
}

function display(): string {
  const t = parseTime(props.modelValue)
  if (!t) return ''
  return `${t.h}:${t.m}`
}
</script>

<template>
  <UPopover v-model:open="open" :content="{ align: 'start' }" :_ui="{ content: 'p-0' }">
    <UButton
      :name="name"
      type="button"
      block
      variant="outline"
      class="justify-start font-normal"
      :class="display() ? '' : 'text-muted'"
      :aria-label="`${placeholder}: ${display() || 'not set'}`"
    >
      <UIcon name="i-lucide-clock" class="size-4 shrink-0 text-muted" />
      <span class="truncate font-mono tabular-nums" :class="display() ? '' : 'text-muted'">
        {{ display() || placeholder }}
      </span>
    </UButton>

    <template #content>
      <div class="flex divide-x divide-default" @keydown.esc="open = false">
        <div class="max-h-56 w-20 overflow-y-auto py-1">
          <button v-for="h in HOURS" :key="h" type="button"
                  class="block w-full px-3 py-1.5 text-left font-mono text-sm tabular-nums transition-colors hover:bg-elevated"
                  :class="h === hour ? 'bg-primary/15 font-semibold text-primary' : ''"
                  @click="commit(h, minute)">{{ h }}</button>
        </div>
        <div class="max-h-56 w-20 overflow-y-auto py-1">
          <button v-for="m in MINUTES" :key="m" type="button"
                  class="block w-full px-3 py-1.5 text-left font-mono text-sm tabular-nums transition-colors hover:bg-elevated"
                  :class="m === minute ? 'bg-primary/15 font-semibold text-primary' : ''"
                  @click="commit(hour, m)">{{ m }}</button>
        </div>
      </div>
    </template>
  </UPopover>
</template>
