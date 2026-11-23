<script setup lang="ts">
/**
 * Rack Capacity card for the dashboard (replaces the Access Points tile).
 *
 * WHAT IT ANSWERS
 * 1. The bar: how many switches sit in each rack, ranked - "which rack is the crowded one".
 * 2. The marker: how much of that rack's U space is used - "and does it still have room".
 *
 * WHY BOTH IN ONE CARD
 * A switch count alone is misleading (one switch can be 1U or 4U) and a U fill alone does not say how
 * many devices that is. The pair is the actual question an admin asks about a rack, so the card draws
 * them side by side on one row per rack instead of two separate charts that have to be compared by eye.
 *
 * TWO TRACKS, NOT ONE: the switch bar and the U track carry different units (buah vs slot), so they get
 * their own track and their own colour rather than sharing a width - one shared width would put "8" and
 * "24%" on the same scale and quietly lie.
 *
 * Numbers come from GET /api/network/racks/summary, which also counts the UPS reservation as used U, so
 * a rack whose only occupant is the UPS does not draw as empty.
 */
type RackRow = {
  rackId: number
  rackName: string
  floorId: number
  floorName: string
  sizeU: number
  switchCount: number
  usedU: number
}

const props = withDefaults(defineProps<{
  racks: RackRow[]
  title?: string
  to?: string
  label?: string
  source?: string
  state?: 'loading' | 'ready' | 'error'
}>(), {
  title: 'Rack Capacity',
  label: 'Switch per rack',
  source: 'Titik = utilisasi U',
  state: 'ready'
})

// Two roots (card + teleported tooltip): without this the parent's class cannot fall through and the
// grid column span passed from index.vue is dropped.
defineOptions({ inheritAttrs: false })

/** Ranked by switch count: the bar is the primary reading, so the biggest rack is the first row. */
const rows = computed(() => {
  const max = Math.max(1, ...props.racks.map(r => r.switchCount))
  return [...props.racks]
    .sort((a, b) => b.switchCount - a.switchCount || b.usedU - a.usedU || a.rackName.localeCompare(b.rackName))
    .map((r, i) => ({
      ...r,
      index: i,
      barPct: (r.switchCount / max) * 100,
      fillPct: r.sizeU > 0 ? Math.min(100, (r.usedU / r.sizeU) * 100) : 0,
      freeU: Math.max(0, r.sizeU - r.usedU)
    }))
})

const totalSwitches = computed(() => props.racks.reduce((n, r) => n + r.switchCount, 0))
const totalUsed = computed(() => props.racks.reduce((n, r) => n + r.usedU, 0))
const totalU = computed(() => props.racks.reduce((n, r) => n + r.sizeU, 0))
const avgFill = computed(() => (totalU.value > 0 ? Math.round((totalUsed.value / totalU.value) * 100) : 0))

/** Which row the pointer is on; every other row steps back. */
const hover = ref<number | null>(null)
const tip = ref<{ x: number, y: number } | null>(null)

const activeRow = computed(() => (hover.value !== null ? rows.value[hover.value] ?? null : null))
const rowDim = (row: { index: number }) => hover.value !== null && hover.value !== row.index

const TIP_W = 210
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
    <!-- h-full so the body really fills the card; the grid's row height is set by the tallest
         neighbour, and without it the content sits at the top with dead space underneath. -->
    <div class="flex h-full flex-1 flex-col gap-3 p-5">
      <header class="flex items-center justify-between gap-3">
        <div class="min-w-0">
          <h2 class="truncate text-sm font-semibold">
            <NuxtLink v-if="to" :to="to" class="transition-colors hover:text-primary">{{ title }}</NuxtLink>
            <template v-else>{{ title }}</template>
          </h2>
          <p class="truncate text-xs text-muted">{{ label }} · {{ source }}</p>
        </div>
        <CardMetricIcon kind="rack" :total="totalSwitches" />
      </header>

      <div v-if="state === 'error'" class="py-6 text-center text-xs text-muted">
        Data rack tidak bisa dimuat.
      </div>

      <div v-else-if="state === 'loading' && !rows.length" class="space-y-3 py-2">
        <span class="block h-6 w-full animate-pulse rounded-lg bg-elevated/60" />
        <span class="block h-6 w-11/12 animate-pulse rounded-lg bg-elevated/60" />
        <span class="block h-6 w-10/12 animate-pulse rounded-lg bg-elevated/60" />
      </div>

      <div v-else-if="!rows.length" class="py-6 text-center text-xs text-muted">
        Belum ada rack.
      </div>

      <div v-else class="flex flex-1 flex-col justify-center gap-2.5">
        <!-- Legend. The two tracks wear these two colours and nothing else does, so the row reads
             without a caption. -->
        <div class="flex items-center gap-4 px-2 text-[10px] text-muted">
          <span class="flex items-center gap-1.5">
            <span class="size-2 rounded-full bg-primary" />
            Switch
          </span>
          <span class="flex items-center gap-1.5">
            <span class="size-2 rounded-full bg-sky-400" />
            U terpakai
          </span>
          <span class="ml-auto tabular-nums">{{ totalU }}U</span>
        </div>

        <ul class="flex flex-col gap-1">
          <li
            v-for="row in rows"
            :key="row.rackId"
            class="group cursor-default rounded-lg px-2 py-1.5 transition-opacity duration-200 hover:bg-elevated/50"
            :class="rowDim(row) ? 'opacity-40' : ''"
            tabindex="0"
            @mouseenter="enterRow(row.index, $event)"
            @mousemove="moveTip"
            @mouseleave="clearTip"
            @focus="enterRow(row.index, $event)"
            @blur="clearTip"
          >
            <div class="flex items-center gap-2.5">
              <!-- Rack name, with the floor underneath: names repeat across buildings, so the floor
                   is what makes a row unambiguous. -->
              <div class="w-14 shrink-0 min-w-0 @max-[420px]:w-12">
                <p class="truncate text-[11px] font-semibold leading-tight">{{ row.rackName }}</p>
                <p class="truncate text-[9px] leading-tight text-muted">{{ row.floorName }}</p>
              </div>

              <!-- Switch count. The width is the count against the busiest rack, so the longest bar
                   is always full - the numbers themselves sit at the end of the row. -->
              <div class="relative h-2.5 min-w-0 flex-1 overflow-hidden rounded-full bg-elevated/80">
                <div
                  class="rack-bar h-full rounded-full"
                  :style="{ width: `${row.barPct}%`, animationDelay: `${60 + row.index * 70}ms` }"
                />
              </div>
              <span class="w-4 shrink-0 text-right text-[11px] font-semibold tabular-nums">{{ row.switchCount }}</span>

              <!-- U fill: its own track, its own scale (0..sizeU). -->
              <div class="hidden h-2.5 w-14 shrink-0 overflow-hidden rounded-full bg-elevated/80 @min-[380px]:block">
                <div
                  class="u-bar h-full rounded-full"
                  :style="{ width: `${row.fillPct}%`, animationDelay: `${180 + row.index * 70}ms` }"
                />
              </div>
              <span class="hidden w-11 shrink-0 text-right text-[10px] tabular-nums text-muted @min-[380px]:block">{{ row.usedU }}/{{ row.sizeU }}U</span>
            </div>
          </li>
        </ul>
      </div>

      <p v-if="state === 'ready' && rows.length" class="border-t border-default pt-2 text-[11px] text-muted">
        <b class="font-semibold text-default tabular-nums">{{ rows.length }}</b> rack
        <span class="mx-1">·</span><b class="font-semibold text-default tabular-nums">{{ totalSwitches }}</b> switch
        <span class="mx-1">·</span><b class="font-semibold text-default tabular-nums">{{ avgFill }}%</b> U terisi
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
        <span class="size-2.5 shrink-0 rounded-sm bg-primary" />
        <span class="min-w-0 flex-1 truncate">{{ activeRow.rackName }}</span>
        <span class="shrink-0 tabular-nums">{{ activeRow.switchCount }}</span>
      </p>
      <p class="mt-0.5 text-[10px] text-muted">{{ activeRow.floorName }} · {{ activeRow.sizeU }}U</p>
      <p class="mt-1.5 border-t border-default/70 pt-1.5 text-[10px] text-muted">
        <b class="font-semibold text-default tabular-nums">{{ activeRow.usedU }}</b> U terpakai
        <span class="mx-1">·</span>
        <b class="font-semibold text-default tabular-nums">{{ activeRow.freeU }}</b> U bebas
        <span class="mx-1">·</span>
        <b class="font-semibold text-default tabular-nums">{{ activeRow.fillPct.toFixed(0) }}%</b>
      </p>
    </div>
  </Teleport>
</template>

<style scoped>
/* The two tracks grow from their left edge: the width is the data, the animation is the entrance, so
   they never fight. `both` holds them collapsed during their stagger delay. */
.rack-bar,
.u-bar {
  transform-origin: left center;
  animation: rack-grow 680ms cubic-bezier(0.22, 1, 0.36, 1) both;
  transition: width 0.5s cubic-bezier(0.22, 1, 0.36, 1), opacity 0.2s ease;
}

/* Switch count: the accent, with a soft top highlight so a full bar is not a flat block. */
.rack-bar {
  background-image: linear-gradient(90deg, color-mix(in oklab, var(--ui-primary) 72%, transparent), var(--ui-primary));
  box-shadow: 0 0 0 1px color-mix(in oklab, var(--ui-primary) 45%, transparent) inset;
}

/* U fill: a second colour, so the two tracks stay distinguishable at a glance. */
.u-bar {
  background-image: linear-gradient(90deg, rgb(56 189 248 / 0.55), rgb(56 189 248 / 0.95));
}

/* A row under the pointer brightens its own bars; nothing moves, so the pointer never loses it. */
li:hover .rack-bar,
li:hover .u-bar,
li:focus-visible .rack-bar,
li:focus-visible .u-bar {
  filter: brightness(1.15) saturate(1.05);
}

@keyframes rack-grow {
  from {
    opacity: 0;
    transform: scaleX(0.02);
  }
  to {
    opacity: 1;
    transform: scaleX(1);
  }
}

@media (prefers-reduced-motion: reduce) {
  .rack-bar,
  .u-bar {
    animation: none;
  }
}
</style>