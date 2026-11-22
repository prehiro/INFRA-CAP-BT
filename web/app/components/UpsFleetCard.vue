<script setup lang="ts">
/**
 * UPS Fleet card for the dashboard (replaces the Network Racks tile).
 *
 * WHAT IT ANSWERS
 * 1. The ribbon: the whole register as one 100% bar, split by MODEL - the shape of the fleet at a glance.
 * 2. The bars: the same models ranked, so the exact counts are readable.
 * 3. Hover anything: units, share of the register, the brand, and how many distinct locations that
 *    model covers.
 *
 * ONE COLOUR PER MODEL, and the ribbon uses the SAME colour as its bar. Two colour languages in one
 * card (brand colour on the ribbon, model colour on the bars) is what makes a small chart look noisy:
 * the ribbon is the same four numbers as the bars, so it should wear the same four colours. The brand
 * did not disappear - it is in the tooltip of every row ("9 unit · APC").
 *
 * WHY NOT A DOUGHNUT
 * 16/4 across two brands is an 80/20 split - a pie of that carries almost no information, and this page
 * already has two doughnuts. The models are the dimension that varies, and they read better as bars.
 *
 * The tooltip is hand-rolled and teleported so the card's own overflow cannot clip it.
 */
type ModelRow = { label: string, brand: string, count: number, locations: number }

const props = withDefaults(defineProps<{
  models: ModelRow[]
  total: number
  /** Distinct locations across the whole register - the footer line. */
  locations?: number
  title?: string
  to?: string
  label?: string
  source?: string
  state?: 'loading' | 'ready' | 'error'
}>(), {
  title: 'UPS Fleet',
  label: 'Registered units',
  source: 'By model',
  state: 'ready'
})

/** Same palette the two doughnuts on this page use, so the row of cards reads as one set. */
const COLORS = ['var(--ui-primary)', '#38BDF8', '#A78BFA', '#F59E0B', '#F472B6', '#2DD4BF', '#F87171']

// Two roots (card + teleported tooltip): without this the parent's class cannot fall through and the
// grid column span passed from index.vue is dropped.
defineOptions({ inheritAttrs: false })

/** Model rows with their share, each in its own colour, ranked by count. */
const rows = computed(() => {
  const sum = props.total || 1
  return props.models.map((m, i) => ({
    ...m,
    pct: (m.count / sum) * 100,
    color: COLORS[i % COLORS.length],
    index: i
  }))
})

/** Which row the pointer is on; every other row and ribbon step back. */
const hover = ref<number | null>(null)
const tip = ref<{ x: number, y: number } | null>(null)

const activeRow = computed(() =>
  hover.value !== null ? rows.value[hover.value] ?? null : null)

const rowDim = (row: { index: number }) => hover.value !== null && hover.value !== row.index

const TIP_W = 208
const TIP_H = 132

/** Follow the pointer; flip to the other side rather than run off the edge of the window. */
const tipStyle = computed(() => {
  if (!tip.value) return {}
  const pad = 10
  let left = tip.value.x + 14
  let top = tip.value.y + 18
  if (left + TIP_W > window.innerWidth - pad) left = tip.value.x - TIP_W - 14
  if (top + TIP_H > window.innerHeight - pad) top = tip.value.y - TIP_H - 14
  return { left: `${Math.max(pad, left)}px`, top: `${Math.max(pad, top)}px` }
})

function pointAt(ev: Event) {
  const el = ev.currentTarget as Element
  const r = el.getBoundingClientRect()
  const mouse = ev as MouseEvent
  // Keyboard focus carries no pointer coordinates, so anchor to the element itself.
  const cx = mouse.clientX
  const cy = mouse.clientY
  tip.value = {
    x: typeof cx === 'number' && cx !== 0 ? cx : r.x + r.width / 2,
    y: typeof cy === 'number' && cy !== 0 ? cy : r.y
  }
}

const enterRow = (i: number, ev: Event) => { hover.value = i; pointAt(ev) }
const moveTip = (ev: Event) => { pointAt(ev) }
const clearTip = () => { hover.value = null; tip.value = null }
</script>

<template>
  <UCard
    v-bind="$attrs"
    class="@container flex flex-col"
    :ui="{ body: 'p-0 sm:p-0 flex-1', header: 'p-0 sm:p-0', footer: 'p-0 sm:p-0' }"
  >
    <!-- h-full so the body really fills the card: without it the content sits at the top and the
         grid's row height (set by the treemap next door) leaves ~90px dead at the bottom. -->
    <div class="flex h-full flex-1 flex-col gap-3 p-5">
      <header class="flex items-center justify-between gap-3">
        <div class="min-w-0">
          <h2 class="truncate text-sm font-semibold">
            <NuxtLink v-if="to" :to="to" class="transition-colors hover:text-primary">{{ title }}</NuxtLink>
            <template v-else>{{ title }}</template>
          </h2>
          <p class="truncate text-xs text-muted">{{ label }} · {{ source }}</p>
        </div>
        <CardMetricIcon kind="ups" :total="total" />
      </header>

      <div v-if="state === 'error'" class="py-6 text-center text-xs text-muted">
        Data UPS tidak bisa dimuat.
      </div>

      <div v-else-if="state === 'loading' && !rows.length" class="space-y-3 py-2">
        <span class="block h-2.5 w-full animate-pulse rounded-full bg-elevated/60" />
        <span class="block h-2 w-11/12 animate-pulse rounded-full bg-elevated/60" />
        <span class="block h-2 w-9/12 animate-pulse rounded-full bg-elevated/60" />
        <span class="block h-2 w-6/12 animate-pulse rounded-full bg-elevated/60" />
      </div>

      <div v-else-if="!rows.length" class="py-6 text-center text-xs text-muted">
        Belum ada data UPS.
      </div>

      <!-- The chart block takes the slack and centres itself, so the footer stays on the baseline. -->
      <div v-else class="flex flex-1 flex-col justify-center gap-3">
        <!-- The whole register as one 100% bar. Same colours, same order as the bars below. -->
        <!-- The dim sits on the outer span and the entrance on the inner one: an animation with
             fill-mode:both holds opacity:1 at its end, which would beat an inline opacity on the SAME
             element and the hover-dim would silently do nothing. -->
        <div class="flex h-2.5 w-full overflow-hidden rounded-full bg-elevated">
          <span
            v-for="row in rows"
            :key="`rib-${row.label}`"
            class="h-full cursor-default transition-opacity duration-200"
            :class="rowDim(row) ? 'opacity-30' : ''"
            :style="{ width: `${row.pct}%` }"
            tabindex="0"
            @mouseenter="enterRow(row.index, $event)"
            @mousemove="moveTip"
            @mouseleave="clearTip"
            @focus="enterRow(row.index, $event)"
            @blur="clearTip"
          >
            <span
              class="ribbon-seg block h-full w-full"
              :style="{ backgroundColor: row.color, animationDelay: `${row.index * 90}ms` }"
            />
          </span>
        </div>

        <!-- gap-1.5, not gap-2: at a 240px card (1600px window) the extra 6px pushed the dashboard 15px
             past the viewport. -->
        <ul class="flex flex-col gap-1.5">
          <li
            v-for="row in rows"
            :key="row.label"
            class="cursor-default transition-opacity duration-200"
            :class="rowDim(row) ? 'opacity-35' : ''"
            tabindex="0"
            @mouseenter="enterRow(row.index, $event)"
            @mousemove="moveTip"
            @mouseleave="clearTip"
            @focus="enterRow(row.index, $event)"
            @blur="clearTip"
          >
            <!-- Below ~230px of card the model name has to win over the "%": the number is still in the
                 tooltip and the bar already carries the proportion, while a truncated "Smart-UPS
                 SMT3000…" loses the part that identifies the unit. -->
            <div class="flex items-baseline justify-between gap-2">
              <!-- No colour dot here: the bar directly under the label already wears the colour, and
                   the 12px the dot costs is what truncated "Smart-UPS SMT3000RMI2UC" at 204-244px. -->
              <span class="min-w-0 truncate text-[11px] font-medium @max-[230px]:text-[10px]">{{ row.label }}</span>
              <span class="shrink-0 text-[11px] tabular-nums text-muted">
                <b class="font-semibold text-default">{{ row.count }}</b>
                <span class="mx-0.5 @max-[230px]:hidden">·</span><span class="@max-[230px]:hidden">{{ row.pct.toFixed(0) }}%</span>
              </span>
            </div>
            <div class="mt-0.5 h-1.5 w-full overflow-hidden rounded-full bg-elevated">
              <div
                class="bar-fill h-full rounded-full"
                :style="{ width: `${row.pct}%`, backgroundColor: row.color, animationDelay: `${120 + row.index * 90}ms` }"
              />
            </div>
          </li>
        </ul>
      </div>

      <p v-if="state === 'ready' && rows.length" class="border-t border-default pt-2 text-[11px] text-muted">
        Tersebar di <b class="font-semibold text-default tabular-nums">{{ locations ?? 0 }}</b> lokasi
        <span class="mx-1">·</span><b class="font-semibold text-default tabular-nums">{{ rows.length }}</b> model
      </p>
    </div>
  </UCard>

  <!-- Teleported: fixed positioning keeps the card's own overflow from clipping it. -->
  <Teleport to="body">
    <div
      v-if="tip && activeRow"
      class="pointer-events-none fixed z-[70] rounded-lg border border-default bg-default p-3 shadow-xl"
      :style="[tipStyle, { width: `${TIP_W}px` }]"
      role="tooltip"
    >
      <p class="flex items-center gap-2 text-xs font-semibold">
        <span class="size-2.5 shrink-0 rounded-sm" :style="{ backgroundColor: activeRow.color }" />
        <span class="min-w-0 flex-1 truncate">{{ activeRow.label }}</span>
        <span class="shrink-0 tabular-nums">{{ activeRow.pct.toFixed(1) }}%</span>
      </p>
      <p class="mt-0.5 text-[10px] text-muted tabular-nums">
        {{ activeRow.count }} unit · {{ activeRow.brand }}
      </p>
      <p class="mt-1.5 border-t border-default/70 pt-1.5 text-[10px] text-muted">
        Dipasang di <b class="font-semibold text-default tabular-nums">{{ activeRow.locations }}</b> lokasi
      </p>
    </div>
  </Teleport>
</template>

<style scoped>
/* Both the ribbon segments and the bars grow from their left edge; the width is the data, the scale is
   the entrance, so they never fight. `both` keeps them collapsed during their stagger delay. */
.ribbon-seg,
.bar-fill {
  transform-origin: left center;
  animation: bar-grow 720ms cubic-bezier(0.22, 1, 0.36, 1) both;
}

/* The bar's own width transition covers a data refresh without re-running the entrance. */
.bar-fill {
  transition: width 0.5s cubic-bezier(0.22, 1, 0.36, 1);
}

@keyframes bar-grow {
  from {
    opacity: 0;
    transform: scaleX(0.02);
  }
  to {
    opacity: 1;
    transform: scaleX(1);
  }
}

/* A bar under the pointer brightens; the row itself never moves, so nothing shifts under the cursor. */
li:hover .bar-fill {
  filter: brightness(1.12) saturate(1.05);
}

@media (prefers-reduced-motion: reduce) {
  .ribbon-seg,
  .bar-fill {
    animation: none;
  }
}
</style>
