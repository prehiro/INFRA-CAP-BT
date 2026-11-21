<script setup lang="ts">
/**
 * Chassis mix doughnut for the dashboard (replaces the CCTV / Handover tiles).
 *
 * WHY SVG AND NOT CANVAS: each slice is one <circle> with pathLength=100 and a dash array, so hover,
 * focus and the sweep-in are CSS rather than pointer maths plus a redraw loop - and it stays crisp at
 * any pixel density without a devicePixelRatio dance.
 *
 * The look follows the PrimeUI Pro pie demo: a doughnut with the total in the middle, the hovered
 * slice taking over that readout, and a legend of swatch / value / share. Hovering a legend row
 * highlights its slice, so both are one control surface instead of two.
 */
const props = withDefaults(defineProps<{
  items: { value: string, count: number }[]
  total: number
  /** Field label, e.g. "Computer Chassis". */
  label?: string
  /** Where the numbers come from, e.g. "PC Ledger". */
  source?: string
  state?: 'loading' | 'ready' | 'error'
}>(), {
  label: 'Computer Chassis',
  source: 'PC Ledger',
  state: 'ready'
})

const COLORS = ['var(--ui-primary)', '#38BDF8', '#A78BFA', '#F59E0B', '#F472B6', '#2DD4BF', '#F87171']

const hovered = ref<number | null>(null)
const ready = ref(false)

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
      index: i
    }
    acc += pct
    return seg
  })
})

const active = computed(() =>
  hovered.value !== null ? segments.value[hovered.value] ?? null : null
)

const dash = (seg: { len: number }) => (ready.value ? `${seg.len} ${100 - seg.len}` : '0 100')
</script>

<template>
  <UCard
    class="@container flex flex-col"
    :ui="{ body: 'p-0 sm:p-0', header: 'p-0 sm:p-0', footer: 'p-0 sm:p-0' }"
  >
    <div class="flex flex-1 flex-col justify-center gap-5 p-5">
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

      <div v-else class="flex flex-col items-center gap-5 @min-[420px]:flex-row @min-[420px]:gap-6">
        <div class="relative size-40 shrink-0 @min-[520px]:size-44">
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
              @mouseleave="hovered = null"
            />
          </svg>

          <div class="pointer-events-none absolute inset-0 grid place-content-center px-6 text-center">
            <p class="text-2xl font-bold leading-none tabular-nums">
              {{ active ? `${active.pct.toFixed(1)}%` : total }}
            </p>
            <p class="mt-1 truncate text-[10px] uppercase tracking-wider text-muted">
              {{ active ? active.value : 'Total unit' }}
            </p>
          </div>
        </div>

        <ul class="w-full min-w-0 space-y-1">
          <li v-for="seg in segments" :key="seg.value">
            <button
              type="button"
              class="flex w-full items-center gap-2.5 rounded-md px-2 py-1.5 text-left transition-colors"
              :class="hovered === seg.index ? 'bg-elevated/70' : 'hover:bg-elevated/50'"
              :aria-label="`${seg.value}: ${seg.count} unit, ${seg.pct.toFixed(1)} persen`"
              @mouseenter="hovered = seg.index"
              @mouseleave="hovered = null"
              @focus="hovered = seg.index"
              @blur="hovered = null"
            >
              <span class="size-2.5 shrink-0 rounded-sm" :style="{ backgroundColor: seg.color }" />
              <span class="min-w-0 flex-1 truncate text-sm">{{ seg.value }}</span>
              <span class="shrink-0 text-xs text-muted tabular-nums">{{ seg.count }}</span>
              <span class="w-14 shrink-0 text-right text-sm font-semibold tabular-nums">
                {{ seg.pct.toFixed(1) }}%
              </span>
            </button>
          </li>
        </ul>
      </div>
    </div>
  </UCard>
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
  /* Collapsed dashes are the "not yet swept" state, so with no animation they must start open. */
  svg circle {
    transition: none;
  }
}
</style>