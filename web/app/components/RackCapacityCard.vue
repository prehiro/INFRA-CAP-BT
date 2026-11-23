<script setup lang="ts">
/**
 * Switch-per-rack card for the dashboard (replaces the Access Points tile).
 *
 * ONE QUESTION, ONE CHART
 * "Which rack is the crowded one, and by how much." Every cell is a rack, the bar is how many switches
 * sit in it, and the number is printed beside the name so the ranking never has to be read off a scale.
 * Cells are dealt in order of that count, left to right, so the busiest rack is always the first one.
 *
 * WHY FOUR PER ROW
 * The first version put every rack on its own full-width row, which made the card as tall as the rack
 * count: 6 racks already pushed the card past its neighbours and any further rack made the whole page
 * scroll (HIRO: "kalau rack bertambah, user jadi harus scroll page untuk melihat kebawah. bisa bagi
 * jadi 2 section? masing-masing row max 4 rack"). Four cells per row with the list split into sections
 * of four keeps the card at two bands - about 160px for eight racks - so ten racks cost 40px instead of
 * another 250px, and the page keeps its no-scroll grid.
 *
 * WHAT THE COMPACT CELL KEEPS: name and count on one line, the floor under it, and the bar. Four cells
 * per row would have been too narrow for the floor at the width the card had when this was a one-column
 * list, but a double-width card gives each cell ~230px, so the floor stays - two buildings can hold a
 * rack with the same name and dropping it would make two different racks look identical.
 *
 * THE SCALE IS RELATIVE TO THE BUSIEST RACK, so the longest bar is always full. That is deliberate: the
 * comparison between racks is what the card is read for, and a fixed 0..N scale would leave every bar
 * looking alike when no rack is close to full. The count is printed, so no value is hidden by the
 * rescaling.
 *
 * WHY THERE IS NO U FILL: the first version drew a second track with how much U each rack uses. HIRO:
 * "hapus saja grafik u nya pada dashboard jadi fokus ke jumlah switch per rack." Two units in one cell
 * (buah and slot) made it answer two questions and neither quickly. GET /api/network/racks/summary
 * still returns sizeU/usedU - this card simply stops drawing them.
 */
type RackRow = {
  rackId: number
  rackName: string
  floorId: number
  floorName: string
  switchCount: number
}

/** A rack with its rank and its bar width worked out, so the template does no arithmetic. */
type Row = RackRow & { index: number, barPct: number }

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

/** Racks per section. Four keeps a section to one row at any card width above ~430px. */
const PER_SECTION = 4

const rows = computed<Row[]>(() => {
  const max = Math.max(1, ...props.racks.map(r => r.switchCount))
  return [...props.racks]
    .sort((a, b) => b.switchCount - a.switchCount || a.rackName.localeCompare(b.rackName))
    .map((r, i) => ({
      ...r,
      index: i,
      // A rack that has a switch still draws a sliver: an invisible bar reads as a missing rack.
      barPct: Math.max(r.switchCount > 0 ? 6 : 0, (r.switchCount / max) * 100)
    }))
})

/** Sections of four. The boundary is also the visual break, so a rack count of 5-8 reads as two bands
 *  rather than one long list that happens to wrap. */
const sections = computed<Row[][]>(() => {
  const out: Row[][] = []
  for (let i = 0; i < rows.value.length; i += PER_SECTION) out.push(rows.value.slice(i, i + PER_SECTION))
  return out
})

const totalSwitches = computed(() => props.racks.reduce((n, r) => n + r.switchCount, 0))
const avgSwitches = computed(() => (
  rows.value.length ? totalSwitches.value / rows.value.length : 0
).toFixed(1))

/** Which cell the pointer is on; every other one steps back. */
const hover = ref<number | null>(null)
const tip = ref<{ x: number, y: number } | null>(null)

const activeRow = computed(() => (hover.value !== null ? rows.value[hover.value] ?? null : null))
const rowDim = (row: Row) => hover.value !== null && hover.value !== row.index

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

      <div v-else-if="state === 'loading' && !rows.length" class="space-y-2 py-2">
        <!-- The skeleton keeps the two-band shape, so a slow load does not change the card's height. -->
        <div class="rack-band grid gap-1.5">
          <span class="block h-11 animate-pulse rounded-md bg-elevated/60" />
          <span class="block h-11 animate-pulse rounded-md bg-elevated/60" />
          <span class="block h-11 animate-pulse rounded-md bg-elevated/60" />
          <span class="block h-11 animate-pulse rounded-md bg-elevated/60" />
        </div>
        <div class="rack-band grid gap-1.5">
          <span class="block h-11 animate-pulse rounded-md bg-elevated/60" />
          <span class="block h-11 animate-pulse rounded-md bg-elevated/60" />
        </div>
      </div>

      <div v-else-if="!rows.length" class="py-6 text-center text-xs text-muted">
        Belum ada rack.
      </div>

      <!-- One band per section: four cells, then a break, then the next four. The break is the gap
           between sections, which is what makes 5-8 racks read as two groups instead of one wrap. -->
      <div v-else class="flex flex-1 flex-col justify-center gap-3">
        <div
          v-for="(section, si) in sections"
          :key="si"
          class="rack-band grid gap-1.5"
        >
          <div
            v-for="row in section"
            :key="row.rackId"
            class="group cursor-default rounded-md px-2 py-2 transition-opacity duration-200 hover:bg-elevated/50"
            :class="rowDim(row) ? 'opacity-40' : ''"
            tabindex="0"
            @mouseenter="enterRow(row.index, $event)"
            @mousemove="moveTip"
            @mouseleave="clearTip"
            @focus="enterRow(row.index, $event)"
            @blur="clearTip"
          >
            <!-- Name and count share one line: at four cells per row the count has to sit beside the
                 name, because a bar with no number next to it is a guess. -->
            <div class="flex items-baseline gap-1">
              <span class="min-w-0 flex-1 truncate text-xs font-semibold leading-tight">{{ row.rackName }}</span>
              <span class="shrink-0 text-xs font-semibold leading-tight tabular-nums">{{ row.switchCount }}</span>
            </div>
            <!-- The floor came back once the cells got wider: two buildings can hold a rack with the same
                 name, and at four per row this line is the only place that says which one it is. -->
            <p class="truncate text-[10px] leading-tight text-muted">{{ row.floorName }}</p>
            <div class="mt-1.5 h-2 overflow-hidden rounded-full bg-elevated/80">
              <div
                class="rack-bar h-full rounded-full"
                :style="{ width: `${row.barPct}%`, animationDelay: `${60 + row.index * 70}ms` }"
              />
            </div>
          </div>
        </div>
      </div>

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
/* Two cells per row on a narrow card, four once the card itself is wide enough. Hand-written rather
   than a Tailwind variant on purpose: a variant class only exists in the CSS if the scanner finds that
   exact string in the source, and a breakpoint used in exactly one component is the one that gets
   missed - the grid then silently stays at two columns and the card grows a row taller. */
.rack-band {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

@container (min-width: 460px) {
  .rack-band {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }
}

/* The bar grows from its left edge: the width is the data, the animation is the entrance, so they never
   fight. `both` holds it collapsed during its stagger delay. */
.rack-bar {
  transform-origin: left center;
  background-image: linear-gradient(90deg, color-mix(in oklab, var(--ui-primary) 72%, transparent), var(--ui-primary));
  box-shadow: 0 0 0 1px color-mix(in oklab, var(--ui-primary) 45%, transparent) inset;
  animation: rack-grow 680ms cubic-bezier(0.22, 1, 0.36, 1) both;
  transition: width 0.5s cubic-bezier(0.22, 1, 0.36, 1), filter 0.2s ease;
}

/* A cell under the pointer brightens its own bar; nothing moves, so the pointer never loses it. */
.group:hover .rack-bar,
.group:focus-visible .rack-bar {
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