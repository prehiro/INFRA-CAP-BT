<script setup lang="ts">
/**
 * Switch-per-rack card for the dashboard (replaces the Access Points tile).
 *
 * ONE QUESTION, ONE CHART
 * "Which rack is the crowded one, and by how much." Every row is a rack, the bar is how many switches
 * sit in it, and the number is printed at the end of the bar so the ranking never has to be read off a
 * scale. Rows are sorted by that count, so the busiest rack is always the first one.
 *
 * WHY THE U FILL IS GONE
 * The first version drew a second track with how much U each rack uses. HIRO: "hapus saja grafik u nya
 * pada dashboard jadi fokus ke jumlah switch per rack. refine teks nya agar sesuai." Two units in one
 * row (buah and slot) made the card answer two questions at once and neither of them quickly - a rack
 * with 3 switches in 42U drew a short bar next to an almost empty track, which reads as "nothing here"
 * when it actually means "plenty of room". One bar per rack is the question this card is for; capacity
 * belongs on the rack page, where the elevation is there to be looked at.
 * GET /api/network/racks/summary still returns sizeU/usedU - this card simply stops drawing them.
 *
 * THE SCALE IS RELATIVE TO THE BUSIEST RACK, so the longest bar is always full. That is deliberate: the
 * comparison between racks is what the card is read for, and a fixed 0..N scale would leave every bar
 * looking alike when no rack is close to full. The count is printed, so no value is hidden by the
 * rescaling.
 */
type RackRow = {
  rackId: number
  rackName: string
  floorId: number
  floorName: string
  switchCount: number
}

const props = withDefaults(defineProps<{
  racks: RackRow[]
  title?: string
  to?: string
  label?: string
  source?: string
  state?: 'loading' | 'ready' | 'error'
}>(), {
  title: 'Switch per Rack',
  label: 'Jumlah switch',
  source: 'Urut terbanyak',
  state: 'ready'
})

// Two roots (card + teleported tooltip): without this the parent's class cannot fall through and the
// grid column span passed from index.vue is dropped.
defineOptions({ inheritAttrs: false })

const rows = computed(() => {
  const max = Math.max(1, ...props.racks.map(r => r.switchCount))
  return [...props.racks]
    .sort((a, b) => b.switchCount - a.switchCount || a.rackName.localeCompare(b.rackName))
    .map((r, i) => ({
      ...r,
      index: i,
      // A rack that has a switch still draws a sliver: an invisible bar reads as a missing row.
      barPct: Math.max(r.switchCount > 0 ? 4 : 0, (r.switchCount / max) * 100)
    }))
})

const totalSwitches = computed(() => props.racks.reduce((n, r) => n + r.switchCount, 0))
const avgSwitches = computed(() =>
  (rows.value.length ? totalSwitches.value / rows.value.length : 0).toFixed(1)
)

/** Which row the pointer is on; every other row steps back. */
const hover = ref<number | null>(null)
const tip = ref<{ x: number, y: number } | null>(null)

const activeRow = computed(() => (hover.value !== null ? rows.value[hover.value] ?? null : null))
const rowDim = (row: { index: number }) => hover.value !== null && hover.value !== row.index

const TIP_W = 200
const TIP_H = 88

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
        <CardMetricIcon kind="rack" :total="totalSwitches" unit="switch" />
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

      <ul v-else class="flex flex-1 flex-col justify-center gap-1">
        <li
          v-for="row in rows"
          :key="row.rackId"
          class="group cursor-default rounded-lg px-2 py-2 transition-opacity duration-200 hover:bg-elevated/50"
          :class="rowDim(row) ? 'opacity-40' : ''"
          tabindex="0"
          @mouseenter="enterRow(row.index, $event)"
          @mousemove="moveTip"
          @mouseleave="clearTip"
          @focus="enterRow(row.index, $event)"
          @blur="clearTip"
        >
          <div class="flex items-center gap-3">
            <!-- Rack name, with the floor underneath: names repeat across buildings, so the floor is
                 what makes a row unambiguous. -->
            <div class="w-14 shrink-0 min-w-0">
              <p class="truncate text-xs font-semibold leading-tight">{{ row.rackName }}</p>
              <p class="truncate text-[10px] leading-tight text-muted">{{ row.floorName }}</p>
            </div>

            <!-- The bar: the count against the busiest rack, with the count printed at its end so the
                 relative scale never hides a value. -->
            <div class="relative h-2.5 min-w-0 flex-1 overflow-hidden rounded-full bg-elevated/80">
              <div
                class="rack-bar h-full rounded-full"
                :style="{ width: `${row.barPct}%`, animationDelay: `${60 + row.index * 70}ms` }"
              />
            </div>

            <span class="w-5 shrink-0 text-right text-xs font-semibold tabular-nums">{{ row.switchCount }}</span>
          </div>
        </li>
      </ul>

      <p v-if="state === 'ready' && rows.length" class="border-t border-default pt-2 text-[11px] text-muted">
        <b class="font-semibold text-default tabular-nums">{{ rows.length }}</b> rack
        <span class="mx-1">·</span><b class="font-semibold text-default tabular-nums">{{ totalSwitches }}</b> switch
        <span class="mx-1">·</span>rata-rata <b class="font-semibold text-default tabular-nums">{{ avgSwitches }}</b> per rack
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
      <p class="mt-0.5 text-[10px] text-muted">{{ activeRow.floorName }}</p>
      <p class="mt-1.5 border-t border-default/70 pt-1.5 text-[10px] text-muted">
        Peringkat <b class="font-semibold text-default tabular-nums">{{ activeRow.index + 1 }}</b>
        dari <b class="font-semibold text-default tabular-nums">{{ rows.length }}</b> rack
      </p>
    </div>
  </Teleport>
</template>

<style scoped>
/* The bar grows from its left edge: the width is the data, the animation is the entrance, so they never
   fight. `both` holds it collapsed during its stagger delay. */
.rack-bar {
  transform-origin: left center;
  background-image: linear-gradient(90deg, color-mix(in oklab, var(--ui-primary) 72%, transparent), var(--ui-primary));
  box-shadow: 0 0 0 1px color-mix(in oklab, var(--ui-primary) 45%, transparent) inset;
  animation: rack-grow 680ms cubic-bezier(0.22, 1, 0.36, 1) both;
  transition: width 0.5s cubic-bezier(0.22, 1, 0.36, 1), filter 0.2s ease;
}

/* A row under the pointer brightens its own bar; nothing moves, so the pointer never loses it. */
li:hover .rack-bar,
li:focus-visible .rack-bar {
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
  .rack-bar {
    animation: none;
  }
}
</style>