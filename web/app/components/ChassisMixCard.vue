<script setup lang="ts">
/**
 * Chassis mix doughnut for the dashboard (replaces the CCTV / Handover tiles).
 *
 * TWO QUESTIONS, ONE CARD
 * 1. The doughnut: what the fleet is made of (Desktop / Notebook / Tablet, each with its share).
 * 2. Hover a slice or its legend row: a tooltip breaks that type down by register - how many of the
 *    desktops are in PC Ledger, still in Factory PC, on standby, or planned for disposal, each with
 *    its share OF THAT TYPE.
 *
 * WHY THE TOOLTIP IS HAND-ROLLED AND NOT UTooltip
 * It has to hang off both an SVG arc and an HTML row. UTooltip wraps its trigger in an element, and a
 * wrapper inside an <svg> is not allowed - so the panel is teleported to <body>, positioned `fixed`
 * from the pointer, and flipped when it would leave the viewport. Being outside the card also means no
 * ancestor can clip it.
 *
 * WHY SVG AND NOT CANVAS: each slice is one <circle> with pathLength=100 and a dash array, so hover,
 * focus and the sweep-in are CSS rather than pointer maths plus a redraw loop - and it stays crisp at
 * any pixel density without a devicePixelRatio dance.
 */
type Part = { label: string, count: number }
type Slice = { value: string, count: number, parts: Part[] }

const props = withDefaults(defineProps<{
  items: Slice[]
  total: number
  /** Field label, e.g. "Computer Chassis". */
  label?: string
  /** Where the numbers come from, e.g. "All registers". */
  source?: string
  state?: 'loading' | 'ready' | 'error'
}>(), {
  label: 'Computer Chassis',
  source: 'All registers',
  state: 'ready'
})

const COLORS = ['var(--ui-primary)', '#38BDF8', '#A78BFA', '#F59E0B', '#F472B6', '#2DD4BF', '#F87171']

// This component renders two roots (the card and the teleported tooltip), so Vue cannot fall the
// parent's class through automatically - without this the grid column span passed from index.vue is
// dropped with a warning and the card silently renders one column wide.
defineOptions({ inheritAttrs: false })

const hovered = ref<number | null>(null)
const ready = ref(false)
const tip = ref<{ x: number, y: number, index: number } | null>(null)

// A timeout, not requestAnimationFrame: rAF never fires in a background tab, so a dashboard opened in
// a tab that is not focused would keep every slice collapsed at "0 100" until the tab was focused.
onMounted(() => {
  setTimeout(() => { ready.value = true }, 60)
})

const segments = computed(() => {
  const sum = props.items.reduce((n, i) => n + i.count, 0) || 1
  let acc = 0
  return props.items.map((it, i) => {
    const pct = (it.count / sum) * 100
    // A sliver of each slice is left empty so neighbouring segments do not bleed into one another.
    const seg = {
      ...it,
      pct,
      color: COLORS[i % COLORS.length],
      len: Math.max(pct - 0.7, 0.3),
      offset: acc,
      index: i,
      parts: it.parts.map(p => ({
        label: p.label,
        count: p.count,
        pct: it.count > 0 ? (p.count / it.count) * 100 : 0
      }))
    }
    acc += pct
    return seg
  })
})

const active = computed(() =>
  hovered.value !== null ? segments.value[hovered.value] ?? null : null
)

const dash = (seg: { len: number }) => (ready.value ? `${seg.len} ${100 - seg.len}` : '0 100')

const TIP_W = 216
const TIP_H = 168

/** Follow the pointer; flip to the other side rather than run off the edge of the window. */
const tipStyle = computed(() => {
  if (!tip.value) return {}
  const pad = 10
  let left = tip.value.x + 14
  let top = tip.value.y + 18
  if (left + TIP_W > window.innerWidth - pad) left = tip.value.x - TIP_W - 14
  if (top + TIP_H > window.innerHeight - pad) top = tip.value.y - TIP_H - 14
  return {
    left: `${Math.max(pad, left)}px`,
    top: `${Math.max(pad, top)}px`
  }
})

function moveTip(index: number, ev: Event) {
  const el = ev.currentTarget as Element
  const r = el.getBoundingClientRect()
  const mouse = ev as MouseEvent
  // Keyboard focus has no pointer coordinates, so anchor to the element itself.
  const x = typeof mouse.clientX === 'number' && mouse.clientX !== 0 ? mouse.clientX : r.x + r.width / 2
  const y = typeof mouse.clientY === 'number' && mouse.clientY !== 0 ? mouse.clientY : r.y
  tip.value = { x, y, index }
  hovered.value = index
}

function clearTip() {
  tip.value = null
  hovered.value = null
}
</script>

<template>
  <UCard
    v-bind="$attrs"
    class="@container flex flex-col"
    :ui="{ body: 'p-0 sm:p-0', header: 'p-0 sm:p-0', footer: 'p-0 sm:p-0' }"
  >
    <div class="flex flex-1 flex-col justify-center gap-4 p-5">
      <header class="flex items-baseline justify-between gap-3">
        <div class="min-w-0">
          <h2 class="truncate text-sm font-semibold">Chassis Mix</h2>
          <p class="truncate text-xs text-muted">{{ label }} · {{ source }}</p>
        </div>
        <span class="shrink-0 text-[11px] text-muted tabular-nums">{{ total }} unit</span>
      </header>

      <div v-if="state === 'error'" class="py-6 text-center text-xs text-muted">
        Data chassis tidak bisa dimuat.
      </div>

      <div
        v-else-if="state === 'loading' && !segments.length"
        class="flex items-center justify-center gap-4 py-6"
      >
        <span class="size-28 animate-pulse rounded-full bg-elevated/60" />
        <div class="space-y-2">
          <span class="block h-3 w-24 animate-pulse rounded bg-elevated/60" />
          <span class="block h-3 w-20 animate-pulse rounded bg-elevated/60" />
          <span class="block h-3 w-16 animate-pulse rounded bg-elevated/60" />
        </div>
      </div>

      <div v-else-if="!segments.length" class="py-6 text-center text-xs text-muted">
        Belum ada data chassis.
      </div>

      <div v-else class="flex flex-col items-center gap-4 @min-[420px]:flex-row @min-[420px]:gap-6">
        <div class="relative size-36 shrink-0 @min-[520px]:size-40">
          <svg viewBox="0 0 100 100" class="size-full -rotate-90" aria-hidden="true">
            <!-- Empty track, so a small slice still reads as part of a whole. -->
            <circle cx="50" cy="50" r="40" fill="none" stroke-width="14" class="stroke-default" />

            <circle
              v-for="seg in segments"
              :key="seg.value"
              cx="50"
              cy="50"
              r="40"
              fill="none"
              :stroke="seg.color"
              :stroke-width="hovered === seg.index ? 18 : 14"
              :opacity="hovered !== null && hovered !== seg.index ? 0.3 : 1"
              pathLength="100"
              :stroke-dasharray="dash(seg)"
              :stroke-dashoffset="-seg.offset"
              class="cursor-pointer"
              :style="{ transitionDelay: ready ? `${seg.index * 90}ms` : '0ms' }"
              @mouseenter="hovered = seg.index"
              @mousemove="moveTip(seg.index, $event)"
              @mouseleave="clearTip"
            />
          </svg>

          <div class="pointer-events-none absolute inset-0 grid place-content-center px-5 text-center">
            <p class="text-2xl font-bold leading-none tabular-nums">
              {{ active ? `${active.pct.toFixed(1)}%` : total }}
            </p>
            <p class="mt-1 truncate text-[10px] uppercase tracking-wider text-muted">
              {{ active ? active.value : 'Total unit' }}
            </p>
          </div>
        </div>

        <!-- w-fit + mx-auto, not w-full: at one column wide the rows used to stretch to the card edge and
             push the count and percentage ~110px away from the chassis name. The block now hugs its
             content and sits centred under the doughnut, so name and numbers read as one line. -->
        <div class="mx-auto w-fit min-w-0">
          <ul class="space-y-0.5">
            <li v-for="seg in segments" :key="seg.value">
              <button
                type="button"
                class="flex items-center gap-2.5 rounded-md px-2 py-1 text-left text-[13px] transition-colors"
                :class="hovered === seg.index ? 'bg-elevated/70' : 'hover:bg-elevated/50'"
                :aria-label="`${seg.value}: ${seg.count} unit, ${seg.pct.toFixed(1)} persen`"
                @mouseenter="moveTip(seg.index, $event)"
                @mousemove="moveTip(seg.index, $event)"
                @mouseleave="clearTip"
                @focus="moveTip(seg.index, $event)"
                @blur="clearTip"
              >
                <span class="size-2.5 shrink-0 rounded-sm" :style="{ backgroundColor: seg.color }" />
                <span class="w-20 truncate">{{ seg.value }}</span>
                <span class="w-9 shrink-0 text-right text-xs text-muted tabular-nums">{{ seg.count }}</span>
                <span class="w-12 shrink-0 text-right font-semibold tabular-nums">
                  {{ seg.pct.toFixed(1) }}%
                </span>
              </button>
            </li>
          </ul>
        </div>
      </div>
    </div>
  </UCard>

  <!-- Teleported: the panel hangs off an SVG arc too, and a child of <svg> other than SVG elements is
       not rendered at all. Fixed positioning also keeps it out from under the card's own overflow. -->
  <Teleport to="body">
    <div
      v-if="tip && active"
      class="pointer-events-none fixed z-[70] rounded-lg border border-default bg-default p-3 shadow-xl"
      :style="[tipStyle, { width: `${TIP_W}px` }]"
      role="tooltip"
    >
      <p class="flex items-center gap-2 text-xs font-semibold">
        <span class="size-2.5 shrink-0 rounded-sm" :style="{ backgroundColor: active.color }" />
        <span class="min-w-0 flex-1 truncate">{{ active.value }}</span>
        <span class="shrink-0 tabular-nums">{{ active.pct.toFixed(1) }}%</span>
      </p>
      <p class="mt-0.5 text-[10px] text-muted tabular-nums">
        {{ active.count }} unit · semua register
      </p>
      <div class="mt-2 space-y-1 border-t border-default/70 pt-2">
        <div
          v-for="part in active.parts"
          :key="part.label"
          class="flex items-center gap-2 text-[11px]"
        >
          <span class="min-w-0 flex-1 truncate" :class="part.count ? '' : 'text-muted'">{{ part.label }}</span>
          <span class="w-8 shrink-0 text-right text-muted tabular-nums">{{ part.count }}</span>
          <span class="w-10 shrink-0 text-right font-medium tabular-nums">{{ part.pct.toFixed(1) }}%</span>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
/* The sweep-in: every segment grows from nothing to its share, staggered a little so it reads as a
   single gesture instead of three. Hover/focus changes must NOT inherit that delay. */
svg circle {
  transition:
    stroke-dasharray 700ms cubic-bezier(0.22, 1, 0.36, 1),
    stroke-width 180ms ease,
    opacity 180ms ease;
}

@media (prefers-reduced-motion: reduce) {
  svg circle {
    transition: none;
  }
}
</style>