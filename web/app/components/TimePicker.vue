<script setup lang="ts">
/**
 * Time picker styled as an analogue clock face, modelled on the design HIRO supplied:
 * two boxes (hour / minute) above a circular dial, a hand pointing at the chosen
 * position, and a filled marker on the selection.
 *
 * The dial is DUAL-LABELLED, exactly like the reference image: the OUTER ring is the
 * familiar 12-hour face (12 at the top, then 1..11 clockwise) and the INNER ring shows
 * the 24-hour equivalent at the same position (00 above 12, then 13..23). So a user
 * thinking in 24-hour terms reads 14 off the inner ring while their finger is on the "2"
 * of the outer one - no mental conversion needed. Minute positions reuse the same twelve
 * spokes at 5-minute increments.
 *
 * FLOW (HIRO, 2026-10-03): open -> press an hour -> dial switches to minutes -> press a
 * minute and the popover CLOSES ITSELF. There is no OK, no Cancel and no hint line
 * (all three were removed at HIRO's request), so the only way out is to finish or click
 * away, and every pick is committed as it happens. Selecting an hour does NOT close,
 * because a minute still has to be chosen.
 *
 * FIXED SIZE (HIRO, "size nya konsisten jangan berubah-ubah"): the popover is pinned to
 * w-64 and the dial to size-[190px], and the inner 24-hour ring - which only exists in the
 * hour step - is drawn inside the SAME circle rather than replacing the outer ring. That
 * matters because the inner ring's labels are a smaller font on a smaller radius, so the
 * minute step used to render visibly shorter and the whole dialog jumped in height when
 * you moved from hours to minutes.
 *
 * THEME - READ THIS BEFORE "FIXING" THE COLOURS:
 * the SVG fills are bound to Nuxt UI's CSS variables through :style, NOT to Tailwind
 * `fill-*` utility classes. Tailwind v4 emits NO `.fill-primary` / `.fill-elevated` /
 * `.fill-default` rules for Nuxt UI's semantic colour names, so writing them silently does
 * nothing and every shape falls back to SVG's default BLACK fill - the dial renders as a
 * black disc. Verified: zero matching rules in document.styleSheets while
 * `getComputedStyle(circle).fill` came back "rgb(0, 0, 0)". Binding the variables directly
 * still honours the user's accent colour and light/dark setting, because Nuxt UI rewrites
 * these variables whenever the theme or colour mode changes.
 *
 * v-model is the time ONLY ("HH:mm", 24h). The caller owns the date - on the CCTV sheet the
 * date is always today, so it is composed at save time (see composeDateTime in
 * cctvacc.vue). Keeping the date out of this component means editing one field never
 * silently rewrites another.
 */
const props = withDefaults(defineProps<{
  modelValue: string | null | undefined
  placeholder?: string
  /** Minute granularity. 5 (HIRO's choice) gives the twelve spokes a person reads off a
   *  wall clock; the original default of 15 was too coarse for a logbook. */
  minuteStep?: number
  name?: string
}>(), { placeholder: 'Select time', minuteStep: 5, name: undefined })

const emit = defineEmits<{ 'update:modelValue': [string] }>()

const open = ref(false)
/** Which half of the dial is being edited right now. */
const step = ref<'hour' | 'minute'>('hour')
const hour = ref('09')
const minute = ref('00')
/** Set by the dial pointer handler: true when the hit lands on the inner (24-hour) ring. */
const pendingInner = ref(false)

const R = 78          // outer ring radius (SVG viewBox is 0 0 200 200, centre 100,100)
const R_INNER = 52    // inner 24-hour ring radius
const R_HAND = 64     // where the hand stops, just inside the selection marker

/* Theme tokens, bound inline because Tailwind emits no fill-* utilities for them. */
const C = {
  face: 'var(--ui-bg-elevated)',
  strong: 'var(--ui-text-highlighted)',
  dim: 'var(--ui-text-dimmed)',
  primary: 'var(--ui-color-primary-500)',
  onPrimary: 'var(--ui-text-inverted)'
}

/** "HH:mm", "9:5", or a full "dd/MM/yy HH:mm" / ISO string -> the time parts. */
function parseTime(v: unknown): { h: string; m: string } | null {
  const s = String(v ?? '').trim()
  if (!s) return null
  const m = s.match(/(\d{1,2}):(\d{2})/)
  if (!m) return null
  return { h: m[1]!.padStart(2, '0'), m: m[2]! }
}

function snap(value: string): string {
  // Snap an off-step minute (e.g. a legacy "09:07" row) DOWN to the nearest spoke so the
  // dial always has something under the hand instead of pointing between two.
  const snapped = Math.floor(Number(value) / props.minuteStep) * props.minuteStep
  return String(Math.min(snapped, 59)).padStart(2, '0')
}

/** Opening always starts on the hour, seeded from the current value. */
function loadFromModel() {
  const t = parseTime(props.modelValue)
  hour.value = t ? t.h : '09'
  minute.value = t ? snap(t.m) : '00'
  step.value = 'hour'
  pendingInner.value = false
}

watch(open, (v) => { if (v) loadFromModel() })

/** Twelve spokes. Index 0 is straight up; each is 30 degrees clockwise. */
function point(i: number, r: number) {
  const a = ((i * 30) - 90) * (Math.PI / 180)
  return { x: 100 + r * Math.cos(a), y: 100 + r * Math.sin(a) }
}

/** Outer label: the 12-hour face. */
function outerLabel(i: number): string {
  return i === 0 ? '12' : String(i)
}

/** Inner label: the 24-hour equivalent of the same spoke (00 above 12, then 13..23). */
function innerLabel(i: number): string {
  return i === 0 ? '00' : String(i + 12)
}

/** Minute label for the same spoke. */
function minuteLabel(i: number): string {
  return String(i * props.minuteStep).padStart(2, '0')
}

/** Which of the twelve spokes is selected right now. */
const selectedIndex = computed(() => {
  if (step.value === 'hour') return Number(hour.value) % 12
  return Math.round(Number(minute.value) / props.minuteStep) % (60 / props.minuteStep)
})

const handEnd = computed(() => point(selectedIndex.value, R_HAND))

/** Push the draft out to the parent on every pick - there is no OK button to defer it. */
function commit() {
  emit('update:modelValue', `${hour.value}:${minute.value}`)
}

function select(i: number) {
  if (step.value === 'hour') {
    // The dial is 12-hour but the model is 24-hour, so resolve which half the user meant
    // from the ring they actually pressed: the outer ring keeps the current AM/PM, the
    // inner ring means the opposite one.
    const target12 = i === 0 ? 12 : i
    const currentlyPm = Number(hour.value) >= 12
    let h: number
    if (pendingInner.value) h = target12 === 12 ? 0 : target12 + 12
    else h = currentlyPm ? target12 : (target12 === 12 ? 0 : target12)
    hour.value = String(h).padStart(2, '0')
    pendingInner.value = false
    step.value = 'minute'
    commit()
  } else {
    minute.value = minuteLabel(i)
    commit()
    // Picking a minute is the end of the interaction - close without needing an outside click.
    open.value = false
  }
}

/** Map a press on the dial to a spoke, and record whether it hit the inner or outer ring. */
function onDialPointer(e: PointerEvent) {
  const svg = e.currentTarget as SVGSVGElement
  const box = svg.getBoundingClientRect()
  if (!box.width) return
  const scale = 200 / box.width
  const x = (e.clientX - box.left) * scale - 100
  const y = (e.clientY - box.top) * scale - 100
  const dist = Math.hypot(x, y)
  if (dist < 14) return                     // dead zone around the centre dot
  pendingInner.value = step.value === 'hour' && dist < (R_INNER + R) / 2
  let deg = (Math.atan2(y, x) * 180) / Math.PI + 90
  if (deg < 0) deg += 360
  select(Math.round(deg / 30) % 12)
}

function focus(which: 'hour' | 'minute') {
  step.value = which
  pendingInner.value = false
}

function display(): string {
  const t = parseTime(props.modelValue)
  return t ? `${t.h}:${t.m}` : ''
}
</script>

<template>
  <UPopover v-model:open="open" :content="{ align: 'start', sideOffset: 6, avoidCollisions: true, collisionPadding: 8 }" :_ui="{ content: 'p-0 overflow-hidden' }">
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
      <!-- Fixed width, and the dial keeps one footprint in both steps. -->
      <div class="w-64 pb-2" @keydown.esc="open = false">
        <!-- The two boxes. Clicking one decides which half of the dial you are editing. -->
        <div class="flex items-center justify-center gap-1 px-4 pt-3 pb-1">
          <button type="button"
                  class="w-16 rounded-lg py-1.5 text-center font-mono text-2xl tabular-nums transition-colors"
                  :class="step === 'hour' ? 'bg-primary/15 text-primary' : 'bg-elevated text-muted hover:text-default'"
                  aria-label="Choose hour"
                  @click="focus('hour')">{{ hour }}</button>
          <span class="font-mono text-2xl font-semibold text-default">:</span>
          <button type="button"
                  class="w-16 rounded-lg py-1.5 text-center font-mono text-2xl tabular-nums transition-colors"
                  :class="step === 'minute' ? 'bg-primary/15 text-primary' : 'bg-elevated text-muted hover:text-default'"
                  aria-label="Choose minute"
                  @click="focus('minute')">{{ minute }}</button>
        </div>

        <svg viewBox="0 0 200 200" class="mx-auto block size-[190px] touch-none select-none"
             role="presentation" @pointerdown="onDialPointer">
          <circle cx="100" cy="100" :r="R + 10" :style="{ fill: C.face }" />

          <!-- The inner 24-hour ring is ALWAYS drawn, in both steps, so the dial keeps one
               footprint and the dialog never changes size between hour and minute. -->
          <text v-for="i in 12" :key="'i' + i"
                :x="point(i - 1, R_INNER).x" :y="point(i - 1, R_INNER).y"
                text-anchor="middle" dominant-baseline="central"
                class="font-mono text-[11px]"
                :style="{ fill: step === 'hour' ? C.dim : 'transparent' }">{{ innerLabel(i - 1) }}</text>

          <!-- outer ring: minutes while editing minutes, the 12-hour face otherwise -->
          <text v-for="i in 12" :key="'o' + i"
                :x="point(i - 1, R).x" :y="point(i - 1, R).y"
                text-anchor="middle" dominant-baseline="central"
                class="font-mono text-[15px]"
                :style="{ fill: selectedIndex === i - 1 ? C.primary : C.strong }"
                :font-weight="selectedIndex === i - 1 ? 600 : 400"
          >{{ step === 'minute' ? minuteLabel(i - 1) : outerLabel(i - 1) }}</text>

          <!-- hand, from the centre dot out to the selection -->
          <line x1="100" y1="100" :x2="handEnd.x" :y2="handEnd.y"
                :style="{ stroke: C.primary }" stroke-width="2.5" stroke-linecap="round" />
          <circle cx="100" cy="100" r="4" :style="{ fill: C.primary }" />

          <!-- selection marker on the chosen spoke -->
          <circle :cx="point(selectedIndex, R).x" :cy="point(selectedIndex, R).y"
                  r="14" :style="{ fill: C.primary }" />
          <text :x="point(selectedIndex, R).x" :y="point(selectedIndex, R).y"
                text-anchor="middle" dominant-baseline="central"
                class="font-mono text-[15px]"
                :style="{ fill: C.onPrimary }" font-weight="600"
          >{{ step === 'minute' ? minuteLabel(selectedIndex) : outerLabel(selectedIndex) }}</text>
        </svg>
      </div>
    </template>
  </UPopover>
</template>
