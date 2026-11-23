<script setup lang="ts">
/**
 * Access Points card for the dashboard - the last cell of the tile grid, right of Switches per Rack.
 *
 * WHAT IT ANSWERS
 * How the access points are spread across the floor plans: one column per floor, the column's height is
 * that floor's share of the busiest one, and the number above it is the count itself. A floor with none
 * still gets its column, because "this floor has no AP" is an answer, not a gap.
 *
 * WHY COLUMNS AND NOT ANOTHER ROW OF BARS
 * Every other card in this row is horizontal - the UPS ribbon, the rack card's rows. A column chart is
 * the one shape in the row that reads at a glance as "per floor, tallest first", and it is the only one
 * that leaves the card's width free for the floor names underneath.
 *
 * WHY THE COUNT SITS ABOVE THE COLUMN
 * The height already carries the comparison; the number carries the value. Without it the reader has to
 * measure against the tallest column to know whether a floor holds 3 APs or 30.
 *
 * ONE COLOUR, NO LEGEND
 * There is a single series, so a legend would be a second name for the same thing. The only second
 * dimension in the data - primary vs BCP access point - is per AP and would need a call per floor to
 * split, which is a second source of truth for a card this size.
 *
 * The tooltip is hand-rolled and teleported so the card's own overflow cannot clip it.
 */
type FloorRow = { id: number, name: string, aps: number, racks: number }

const props = withDefaults(defineProps<{
  floors: FloorRow[]
  total: number
  title?: string
  to?: string
  label?: string
  source?: string
  state?: 'loading' | 'ready' | 'error'
}>(), {
  title: 'Access Points',
  label: 'Per floor',
  source: 'Ranked by count',
  state: 'ready'
})

// Two roots (card + teleported tooltip): without this the parent's class cannot fall through and the
// grid column span passed from index.vue is dropped.
defineOptions({ inheritAttrs: false })

/** SIX columns is what stays readable in a one-column card; the rest are counted in the footer. */
const MAX_COLUMNS = 6

/** Ranked floors, tallest first, with the height each column should take (0-100% of the tallest). */
const columns = computed(() => {
  const rows = [...props.floors].sort((a, b) => b.aps - a.aps || a.name.localeCompare(b.name))
  const shown = rows.slice(0, MAX_COLUMNS)
  const peak = shown.reduce((n, f) => Math.max(n, f.aps), 0) || 1
  return shown.map((f, i) => ({ ...f, index: i, pct: (f.aps / peak) * 100 }))
})

/** Floors beyond the sixth: the footer says how many, so the chart never lies about the total. */
const hiddenCount = computed(() => Math.max(0, props.floors.length - MAX_COLUMNS))

/** Which column the pointer is on; every other column steps back. */
const hover = ref<number | null>(null)
const tip = ref<{ x: number, y: number } | null>(null)

const activeFloor = computed(() =>
  hover.value !== null ? columns.value[hover.value] ?? null : null)

const rowDim = (row: { index: number }) => hover.value !== null && hover.value !== row.index

/** Share of every access point in the register - the tooltip's second line. */
const shareOf = (aps: number) =>
  ((aps / (props.total || 1)) * 100).toFixed(0)

const TIP_W = 200
const TIP_H = 118

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
         grid's row height leaves dead space under the chart. -->
    <div class="flex h-full flex-1 flex-col gap-3 p-5">
      <header class="flex items-center justify-between gap-3">
        <div class="min-w-0">
          <h2 class="truncate text-sm font-semibold">
            <NuxtLink v-if="to" :to="to" class="transition-colors hover:text-primary">{{ title }}</NuxtLink>
            <template v-else>{{ title }}</template>
          </h2>
          <p class="truncate text-xs text-muted">{{ label }} · {{ source }}</p>
        </div>
        <CardMetricIcon kind="ap" :total="total" unit="access point" unit-plural="access points" />
      </header>

      <div v-if="state === 'error'" class="py-6 text-center text-xs text-muted">
        Access point data could not be loaded.
      </div>

      <div v-else-if="state === 'loading' && !columns.length" class="flex flex-1 items-end gap-2 pb-1">
        <!-- One column-shaped skeleton per slot, so a slow load does not change the card's height. -->
        <div v-for="n in 4" :key="`sk-${n}`" class="flex flex-1 flex-col items-center gap-1">
          <span class="h-3 w-4 animate-pulse rounded bg-elevated/60" />
          <span class="w-full max-w-9 flex-1 animate-pulse rounded-md bg-elevated/50" />
          <span class="h-2.5 w-full animate-pulse rounded bg-elevated/60" />
        </div>
      </div>

      <div v-else-if="!columns.length" class="py-6 text-center text-xs text-muted">
        No access points yet.
      </div>

      <!-- items-stretch + flex-1 on the track: every column is as tall as the tallest, so the
           comparison is the FILL, never the box around it. -->
      <div v-else class="flex flex-1 items-stretch gap-1.5 sm:gap-2">
        <div
          v-for="f in columns"
          :key="f.id"
          class="group flex min-w-0 flex-1 cursor-default flex-col items-center gap-1 transition-opacity duration-200"
          :class="rowDim(f) ? 'opacity-40' : ''"
          tabindex="0"
          @mouseenter="enterRow(f.index, $event)"
          @mousemove="moveTip"
          @mouseleave="clearTip"
          @focus="enterRow(f.index, $event)"
          @blur="clearTip"
        >
          <!-- The count: the height carries the comparison, this carries the value. -->
          <span
            class="text-[11px] font-semibold leading-none tabular-nums"
            :class="f.aps === 0 ? 'text-muted' : (f.pct === 100 ? 'text-primary' : 'text-default')"
          >{{ f.aps }}</span>

          <!-- The track. overflow-hidden is what makes the fill's corners follow the track's. -->
          <div class="relative w-full flex-1 overflow-hidden rounded-md bg-elevated/50">
            <div
              class="ap-fill absolute inset-x-0 bottom-0 rounded-t-sm bg-primary/75"
              :style="{ height: `${f.pct}%`, animationDelay: `${90 + f.index * 70}ms` }"
            >
              <!-- The lit top edge: the "signal" line, so a short column still reads as a live one. -->
              <span class="ap-cap" />
            </div>
          </div>

          <span class="w-full truncate text-center text-[10px] leading-tight text-muted">{{ f.name }}</span>
        </div>
      </div>

      <p v-if="state === 'ready' && columns.length" class="border-t border-default pt-2 text-[11px] text-muted">
        <b class="font-semibold text-default tabular-nums">{{ floors.length }}</b> floors
        <span class="mx-1">·</span><b class="font-semibold text-default tabular-nums">{{ total }}</b> access points
        <template v-if="hiddenCount">
          <span class="mx-1">·</span>top {{ MAX_COLUMNS }} of {{ floors.length }}
        </template>
      </p>
    </div>
  </UCard>

  <!-- Teleported: fixed positioning keeps the card's own overflow from clipping it. -->
  <Teleport to="body">
    <div
      v-if="tip && activeFloor"
      class="pointer-events-none fixed z-[70] rounded-lg border border-default bg-default p-3 shadow-xl"
      :style="[tipStyle, { width: `${TIP_W}px` }]"
      role="tooltip"
    >
      <p class="flex items-center gap-2 text-xs font-semibold">
        <span class="size-2.5 shrink-0 rounded-sm bg-primary" />
        <span class="min-w-0 flex-1 truncate">{{ activeFloor.name }}</span>
        <span class="shrink-0 tabular-nums">{{ activeFloor.aps }}</span>
      </p>
      <!-- A floor with none says so: "0 access points · 0% of all" is arithmetically right and reads
           like a rounding error. -->
      <p v-if="activeFloor.aps === 0" class="mt-0.5 text-[10px] text-muted">
        No access points on this floor
      </p>
      <p v-else class="mt-0.5 text-[10px] text-muted tabular-nums">
        {{ activeFloor.aps }} access point<span v-if="activeFloor.aps !== 1">s</span> ·
        {{ shareOf(activeFloor.aps) }}% of all
      </p>
      <p class="mt-1.5 border-t border-default/70 pt-1.5 text-[10px] text-muted">
        <b class="font-semibold text-default tabular-nums">{{ activeFloor.racks }}</b>
        rack<span v-if="activeFloor.racks !== 1">s</span> on this floor
      </p>
    </div>
  </Teleport>
</template>

<style scoped>
/* Each column grows from the baseline; the height IS the data, so the entrance only scales the box and
   never changes the ratio. `both` keeps a column collapsed through its stagger delay. */
.ap-fill {
  transform-origin: bottom center;
  animation: ap-rise 640ms cubic-bezier(0.22, 1, 0.36, 1) both;
  transition: height 0.5s cubic-bezier(0.22, 1, 0.36, 1);
}

/* The lit edge on top of the fill. Not a glow - a 2px line, so the eye finds the level of each column. */
.ap-cap {
  position: absolute;
  inset-inline: 0;
  top: 0;
  height: 2px;
  background: rgb(255 255 255 / 0.45);
}

/* A column under the pointer brightens; the column itself never moves, so nothing shifts. */
.group:hover .ap-fill,
.group:focus-visible .ap-fill {
  filter: brightness(1.12) saturate(1.05);
}

.group:focus-visible {
  outline: none;
  border-radius: 0.375rem;
  box-shadow: 0 0 0 2px var(--ui-primary);
}

@keyframes ap-rise {
  from {
    opacity: 0;
    transform: scaleY(0.04);
  }
  to {
    opacity: 1;
    transform: scaleY(1);
  }
}

@media (prefers-reduced-motion: reduce) {
  .ap-fill {
    animation: none;
  }
}
</style>