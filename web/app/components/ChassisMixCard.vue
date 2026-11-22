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
 * WHY FILLED PATHS AND NOT A STROKED CIRCLE WITH A DASH ARRAY
 * A dash array gives butted ends and cannot be rounded: `stroke-linecap="round"` adds a 7px semicircle
 * at each end, which would need a ~6% gap between slices to stay separated. The slices here are drawn
 * as annular sectors with a small corner radius instead (the Chart.js `borderRadius` look), so the ends
 * stay softly squared and the 0.7% gap survives. The trade: there is no dash to animate, so the
 * entrance is a staggered scale+fade rather than a sweep.
 *
 * WHY THE TOOLTIP IS HAND-ROLLED AND NOT UTooltip
 * It has to hang off both an SVG arc and an HTML row. UTooltip wraps its trigger in an element, and a
 * wrapper inside an <svg> is not allowed - so the panel is teleported to <body>, positioned `fixed`
 * from the pointer, and flipped when it would leave the viewport. Being outside the card also means no
 * ancestor can clip it.
 */
type Part = { label: string, count: number }
// `parts` is optional: a card whose population is already one register (the active PCs) has
// nothing to break a slice down into, and then the tooltip shows just the slice itself.
type Slice = { value: string, count: number, parts?: Part[] }

const props = withDefaults(defineProps<{
  items: Slice[]
  total: number
  /** Card title. Defaults to the Chassis Mix heading, which is what the fleet card uses. */
  title?: string
  /** Where the title links to. Omit for a plain heading. */
  to?: string
  /** Field label, e.g. "Computer Chassis". */
  label?: string
  /** Where the numbers come from, e.g. "All registers". */
  source?: string
  /** Which top-right badge to draw. 'fleet' = the whole registered fleet, 'domain' = assigned PCs. */
  icon?: 'fleet' | 'domain'
  state?: 'loading' | 'ready' | 'error'
}>(), {
  title: 'Chassis Mix',
  label: 'By Chassis',
  source: 'All registered PC',
  icon: 'fleet',
  state: 'ready'
})

const COLORS = ['var(--ui-primary)', '#38BDF8', '#A78BFA', '#F59E0B', '#F472B6', '#2DD4BF', '#F87171']

// This component renders two roots (the card and the teleported tooltip), so Vue cannot fall the
// parent's class through automatically - without this the grid column span passed from index.vue is
// dropped with a warning and the card silently renders one column wide.
defineOptions({ inheritAttrs: false })

/** Geometry, in the 100x100 viewBox: the ring runs from R_IN to R_OUT, i.e. 14 units thick. */
const R_OUT = 47
const R_IN = 33
/** Corner radius. 3.2 units is ~4.6 CSS px on the 144px doughnut: softened, not pill-shaped. */
const CORNER = 3.2
const TAU = Math.PI * 2

const hovered = ref<number | null>(null)
const tip = ref<{ x: number, y: number, index: number } | null>(null)

const polar = (radius: number, turn: number) => {
  const a = turn * TAU
  return [50 + radius * Math.cos(a), 50 + radius * Math.sin(a)]
}

/**
 * One annular sector from turn t0 to t1 with rounded corners of radius r.
 * Each corner is a quadratic through the sharp corner point, which is the cheap way to soften it
 * without solving the real arc/arc tangency - at this radius the difference is invisible.
 */
function ringPath(t0: number, t1: number, ro: number, ri: number, r: number) {
  const dOut = r / ro / TAU
  const dIn = r / ri / TAU
  const large = (t1 - t0) > 0.5 ? 1 : 0

  const [ax, ay] = polar(ro, t0 + dOut)
  const [bx, by] = polar(ro, t1 - dOut)
  const [c1x, c1y] = polar(ro, t1)
  const [cx, cy] = polar(ro - r, t1)
  const [dx, dy] = polar(ri + r, t1)
  const [c2x, c2y] = polar(ri, t1)
  const [ex, ey] = polar(ri, t1 - dIn)
  const [fx, fy] = polar(ri, t0 + dIn)
  const [c3x, c3y] = polar(ri, t0)
  const [gx, gy] = polar(ri + r, t0)
  const [hx, hy] = polar(ro - r, t0)
  const [c0x, c0y] = polar(ro, t0)

  return [
    `M${ax} ${ay}`,
    `A${ro} ${ro} 0 ${large} 1 ${bx} ${by}`,
    `Q${c1x} ${c1y} ${cx} ${cy}`,
    `L${dx} ${dy}`,
    `Q${c2x} ${c2y} ${ex} ${ey}`,
    `A${ri} ${ri} 0 ${large} 0 ${fx} ${fy}`,
    `Q${c3x} ${c3y} ${gx} ${gy}`,
    `L${hx} ${hy}`,
    `Q${c0x} ${c0y} ${ax} ${ay}`,
    'Z'
  ].join(' ')
}

const segments = computed(() => {
  const sum = props.items.reduce((n, i) => n + i.count, 0) || 1
  let acc = 0
  return props.items.map((it, i) => {
    const pct = (it.count / sum) * 100
    // A sliver of each slice is left empty so neighbouring segments do not bleed into one another.
    const len = Math.max(pct - 0.7, 0.3)
    const t0 = acc / 100
    const t1 = (acc + len) / 100
    acc += pct

    // Hovering swells the ring the way the old stroke-width did: 2 units outward and 2 inward.
    const hot = hovered.value === i
    const ro = R_OUT + (hot ? 2 : 0)
    const ri = R_IN - (hot ? 2 : 0)
    // Never let the corner radius eat more than a quarter of the inner edge.
    const r = Math.min(CORNER, (t1 - t0) * TAU * ri * 0.25)

    return {
      ...it,
      pct,
      color: COLORS[i % COLORS.length],
      d: ringPath(t0, t1, ro, ri, r),
      index: i,
      parts: (it.parts ?? []).map(p => ({
        label: p.label,
        count: p.count,
        pct: it.count > 0 ? (p.count / it.count) * 100 : 0
      }))
    }
  })
})

const active = computed(() =>
  hovered.value !== null ? segments.value[hovered.value] ?? null : null
)

// Unique per instance: this card renders twice on the dashboard and a fixed id would be
// duplicated across the two SVGs.
const glowId = useId()

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
      <header class="flex items-center justify-between gap-3">
        <div class="min-w-0">
          <!-- The title links to the page when `to` is given, so a chart card can still be the way
               into its list without wrapping the whole card in an <a> (the legend rows are buttons
               and must not be nested inside a link). -->
          <h2 class="truncate text-sm font-semibold">
            <NuxtLink
              v-if="to"
              :to="to"
              class="transition-colors hover:text-primary"
            >{{ title }}</NuxtLink>
            <template v-else>{{ title }}</template>
          </h2>
          <p class="truncate text-xs text-muted">{{ label }} · {{ source }}</p>
        </div>
        <!-- The count moved into the badge's tooltip/aria-label; the corner shows what the card
             counts instead of a third identical grey "467 unit". -->
        <CardMetricIcon :kind="icon" :total="total" />
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

      <div v-else class="flex w-full flex-col items-center gap-3 @min-[420px]:flex-row @min-[420px]:gap-6">
        <div class="relative size-32 shrink-0 @min-[520px]:size-40">
          <svg viewBox="0 0 100 100" class="size-full -rotate-90" aria-hidden="true">
            <!-- Hover glow. Blurring SourceGraphic keeps the slice's own colour, so the halo matches the
                 arc instead of being one flat accent. The alpha transfer is what makes it "sedikit": a
                 plain drop-shadow at full strength reads as a second, brighter ring. The filter region
                 is widened because the default -10%/120% clips the blur on the outer edge.
                 The id comes from useId() - this card renders twice on the dashboard and a fixed id
                 would be duplicated across the two SVGs. -->
            <defs>
              <filter :id="glowId" x="-45%" y="-45%" width="190%" height="190%">
                <feGaussianBlur in="SourceGraphic" stdDeviation="2.2" result="blur" />
                <feComponentTransfer in="blur" result="soft">
                  <feFuncA type="linear" slope="0.55" />
                </feComponentTransfer>
                <feMerge>
                  <feMergeNode in="soft" />
                  <feMergeNode in="SourceGraphic" />
                </feMerge>
              </filter>
            </defs>

            <!-- Empty track, so a small slice still reads as part of a whole. -->
            <circle cx="50" cy="50" r="40" fill="none" stroke-width="14" class="stroke-default" />

            <!-- The dim scales the whole slice, the animation fades it - so they must live on
                 different elements or the animation's final opacity:1 would beat the dim. -->
            <g
              v-for="seg in segments"
              :key="seg.value"
              class="transition-opacity duration-200"
              :opacity="hovered !== null && hovered !== seg.index ? 0.3 : 1"
            >
              <path
                :d="seg.d"
                :fill="seg.color"
                class="slice cursor-pointer"
                :filter="hovered === seg.index ? `url(#${glowId})` : undefined"
                :style="{ animationDelay: `${seg.index * 90}ms` }"
                @mouseenter="hovered = seg.index"
                @mousemove="moveTip(seg.index, $event)"
                @mouseleave="clearTip"
              />
            </g>
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
                class="flex items-center gap-2.5 rounded-md px-2 py-0.5 text-left text-[13px] transition-colors"
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
        {{ active.count }} unit · {{ source }}
      </p>
      <div v-if="active.parts.length" class="mt-2 space-y-1 border-t border-default/70 pt-2">
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
/* Staggered entrance. This replaces the old dash-array sweep: an annular path has no dash to grow, so
   each slice scales up from the doughnut centre instead. `both` keeps it collapsed during its delay. */
.slice {
  transform-box: view-box;
  transform-origin: 50px 50px;
  animation: slice-in 700ms cubic-bezier(0.22, 1, 0.36, 1) both;
}

@keyframes slice-in {
  from {
    opacity: 0;
    transform: scale(0.9);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}

@media (prefers-reduced-motion: reduce) {
  .slice {
    animation: none;
  }
}
</style>