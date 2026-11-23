<script setup lang="ts">
/**
 * Switch-per-rack card for the dashboard (replaces the Access Points tile).
 *
 * ONE QUESTION, ONE CHART
 * "Which rack is the crowded one, and by how much." Every cell is a rack, the bar is how many switches
 * sit in it, and the number is printed beside the name so the ranking never has to be read off a scale.
 * Cells are dealt in order of that count, left to right, top to bottom, so the busiest rack is first.
 *
 * TWO BY FOUR, THEN SWIPE
 * Racks are laid out two per row and four rows per page, and the card shows one page at a time:
 * HIRO asked for "4x2", then "buat 2x4" - two cells across, four down, eight per page. That is the
 * shape that keeps a cell wide enough to read (about 225px in this card, against 111px at four across)
 * while holding the page to the same 232px of strip, so the card still cannot be pushed taller by the
 * rack count. The strip scrolls horizontally (touch swipe, trackpad, shift+wheel, the arrow buttons, or
 * drag with the mouse) and snaps to a page boundary.
 *
 * WHY PAGES AND NOT ONE LONG STRIP: a CSS grid with four rows and no explicit column count can only fill
 * column by column, so a single strip would read 1,2,3,4 down the first column and 5,6,7,8 down the
 * second - wrong order for a ranking. Chunking the sorted list into pages of eight and laying the pages out in a
 * flex row keeps the reading order row by row and gives the snap points something meaningful to land on.
 *
 * WHAT THE CELL CARRIES: name and count on one line, the floor under it, and the bar. Two buildings can
 * hold a rack with the same name, so the floor is not decoration - it is the only thing that says which
 * rack a cell is talking about.
 *
 * THE SCALE IS RELATIVE TO THE BUSIEST RACK, so the longest bar is always full. That is deliberate: the
 * comparison between racks is what the card is read for, and a fixed 0..N scale would leave every bar
 * looking alike when no rack is close to full. The count is printed, so no value is hidden by the
 * rescaling.
 *
 * WHY THERE IS NO U FILL: an earlier version drew a second track with how much U each rack uses. HIRO:
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
  label: 'Switch count',
  source: 'By rack',
  state: 'ready'
})

// Two roots (card + teleported tooltip): without this the parent's class cannot fall through and the
// grid column span passed from index.vue is dropped.
defineOptions({ inheritAttrs: false })

const PER_ROW = 2
const PER_ROW_COUNT = 4
const PER_PAGE = PER_ROW * PER_ROW_COUNT

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

/** Pages of eight (two across, four down). The last page holds the remainder, so 9-16 racks read cleanly. */
const pages = computed<Row[][]>(() => {
  const out: Row[][] = []
  for (let i = 0; i < rows.value.length; i += PER_PAGE) out.push(rows.value.slice(i, i + PER_PAGE))
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

/* ------------------------------------------------------------------ paging */

const strip = ref<HTMLElement | null>(null)
const pageIndex = ref(0)
const atStart = ref(true)
const atEnd = ref(false)
/** True while a mouse drag is in progress. The strip drops its scroll-snap for the duration. */
const dragging = ref(false)

/** One page wide plus the gap between pages: the distance the strip travels per arrow press. */
const step = () => {
  const el = strip.value
  if (!el) return 0
  const first = el.firstElementChild as HTMLElement | null
  const gap = Number.parseFloat(getComputedStyle(el).columnGap || '0') || 0
  return (first?.offsetWidth ?? el.clientWidth) + gap
}

const pageCount = computed(() => Math.max(1, pages.value.length))

/** Recomputed from the scroll position rather than tracked, so a trackpad swipe stays in step. */
function syncStrip() {
  const el = strip.value
  if (!el) return
  const s = step()
  atStart.value = el.scrollLeft <= 2
  atEnd.value = el.scrollLeft + el.clientWidth >= el.scrollWidth - 2
  pageIndex.value = s > 0 ? Math.min(pageCount.value - 1, Math.round(el.scrollLeft / s)) : 0
}

function goToPage(i: number) {
  const el = strip.value
  if (!el) return
  const target = Math.max(0, Math.min(pageCount.value - 1, i))
  el.scrollTo({ left: target * step(), behavior: 'smooth' })
}

const canSwipe = computed(() => pageCount.value > 1)

/* ------------------------------------------------------------------ drag */

// A mouse cannot swipe a scroller, so the strip is draggable. The 6px threshold is what keeps a click
// on a cell from turning into a drag: below it the pointer never moved, above it the strip takes over.
//
// SNAP IS DROPPED FOR THE DURATION (`.rack-strip--dragging`). With `scroll-snap-type: x mandatory` left
// on, every assignment to scrollLeft is re-snapped by the browser before the next frame, so the strip
// jumps between page boundaries instead of following the pointer - the drag reads as "stuck". Turning the
// snap back on when the pointer is released lands it on the nearest page, which is what a touch swipe
// does on its own.
let moved = false
let startX = 0
let startLeft = 0

function onPointerDown(e: PointerEvent) {
  if (e.pointerType === 'touch' || !strip.value) return
  moved = false
  startX = e.clientX
  startLeft = strip.value.scrollLeft
}

function onPointerMove(e: PointerEvent) {
  const el = strip.value
  if (!el || e.buttons === 0) return
  const dx = e.clientX - startX
  if (!moved && Math.abs(dx) < 6) return
  if (!moved) {
    moved = true
    dragging.value = true
  }
  el.scrollLeft = startLeft - dx
}

function onPointerUp() {
  if (!moved) {
    dragging.value = false
    return
  }
  const landed = strip.value ? Math.round(strip.value.scrollLeft / step()) : 0
  // Snap back on first, then glide: with the snap still off the scrollTo would be re-snapped anyway.
  dragging.value = false
  goToPage(landed)
  moved = false
}

onMounted(() => {
  syncStrip()
  window.addEventListener('resize', syncStrip)
})

onBeforeUnmount(() => window.removeEventListener('resize', syncStrip))

// A page added or removed by a data refresh changes the scrollable width, so re-measure once the DOM
// has the new cells.
watch([rows, pageCount], async () => {
  await nextTick()
  syncStrip()
})
</script>

<template>
  <UCard
    v-bind="$attrs"
    class="flex flex-col"
    :ui="{ body: 'p-0 sm:p-0 flex-1', header: 'p-0 sm:p-0', footer: 'p-0 sm:p-0' }"
  >
    <!-- No `@container` on this card: the four-column page is a fixed repeat(4, …), not a container
         query, so nothing inside asks for one - and the col-span the parent passes still resolves
         against the dashboard's own container. -->
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

        <!-- The arrows live in the header rather than floating over the strip: a control that covers a
             cell hides the one piece of data the user is swiping towards. They appear only when there
             is a second page, so a card with eight racks carries no dead buttons. -->
        <div class="flex shrink-0 items-center gap-1.5">
          <div v-if="canSwipe" class="flex items-center gap-1">
            <UButton
              color="neutral"
              variant="ghost"
              size="xs"
              class="rack-arrow"
              :disabled="atStart"
              aria-label="Previous page"
              @click="goToPage(pageIndex - 1)"
            >
              <!-- Inline SVG, not <UIcon name="i-lucide-chevron-left">: this build runs Nuxt Icon with
                   provider 'none' and no API fallback, and only the icons named in nuxt.config's
                   clientBundle exist - chevron-LEFT is not in that list (chevron-right is), so the icon
                   form would have rendered as an empty span with no error anywhere. -->
              <svg viewBox="0 0 24 24" class="size-4" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="m15 18-6-6 6-6" />
              </svg>
            </UButton>
            <span class="w-8 text-center text-[10px] tabular-nums text-muted">{{ pageIndex + 1 }}/{{ pageCount }}</span>
            <UButton
              color="neutral"
              variant="ghost"
              size="xs"
              class="rack-arrow"
              :disabled="atEnd"
              aria-label="Next page"
              @click="goToPage(pageIndex + 1)"
            >
              <svg viewBox="0 0 24 24" class="size-4" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="m9 18 6-6-6-6" />
              </svg>
            </UButton>
          </div>
          <CardMetricIcon kind="rack" :total="totalSwitches" unit="switch" />
        </div>
      </header>

      <div v-if="state === 'error'" class="py-6 text-center text-xs text-muted">
        Rack data could not be loaded.
      </div>

      <div v-else-if="state === 'loading' && !rows.length" class="space-y-2 py-2">
        <!-- One page-shaped skeleton, so a slow load does not change the card's height. -->
        <div class="grid grid-cols-2 grid-rows-4 gap-1.5">
          <span class="block h-full min-h-[52px] animate-pulse rounded-md bg-elevated/60" />
          <span class="block h-full min-h-[52px] animate-pulse rounded-md bg-elevated/60" />
          <span class="block h-full min-h-[52px] animate-pulse rounded-md bg-elevated/60" />
          <span class="block h-full min-h-[52px] animate-pulse rounded-md bg-elevated/60" />
          <span class="block h-full min-h-[52px] animate-pulse rounded-md bg-elevated/60" />
          <span class="block h-full min-h-[52px] animate-pulse rounded-md bg-elevated/60" />
          <span class="block h-full min-h-[52px] animate-pulse rounded-md bg-elevated/60" />
          <span class="block h-full min-h-[52px] animate-pulse rounded-md bg-elevated/60" />
        </div>
      </div>

      <div v-else-if="!rows.length" class="py-6 text-center text-xs text-muted">
        No racks yet.
      </div>

      <div
        v-else
        ref="strip"
        class="rack-strip flex flex-1 items-stretch"
        :class="[canSwipe ? 'cursor-grab' : '', dragging ? 'rack-strip--dragging' : '']"
        @scroll.passive="syncStrip"
        @pointerdown="onPointerDown"
        @pointermove="onPointerMove"
        @pointerup="onPointerUp"
        @pointerleave="onPointerUp"
      >
        <section
          v-for="(page, pi) in pages"
          :key="pi"
          class="rack-page"
          :aria-label="`Racks page ${pi + 1}`"
        >
          <div
            v-for="row in page"
            :key="row.rackId"
            class="group flex h-full flex-col justify-center rounded-md px-2 py-1 transition-opacity duration-200 hover:bg-elevated/50"
            :class="rowDim(row) ? 'opacity-40' : ''"
            tabindex="0"
            @mouseenter="enterRow(row.index, $event)"
            @mousemove="moveTip"
            @mouseleave="clearTip"
            @focus="enterRow(row.index, $event)"
            @blur="clearTip"
          >
            <!-- The cell centres its three lines: the strip is twice as tall as one cell's content, and
                 top-aligning them would leave a dead band under every bar and make the grid look like
                 it had failed to fill its page. -->
            <!-- Name and count share one line: at four cells per row the count has to sit beside the
                 name, because a bar with no number next to it is a guess. -->
            <div class="flex items-baseline gap-1">
              <span class="min-w-0 flex-1 truncate text-xs font-semibold leading-tight">{{ row.rackName }}</span>
              <span class="shrink-0 text-xs font-semibold leading-tight tabular-nums">{{ row.switchCount }}</span>
            </div>
            <p class="truncate text-[10px] leading-tight text-muted">{{ row.floorName }}</p>
            <div class="mt-0.5 h-2 overflow-hidden rounded-full bg-elevated/80">
              <div
                class="rack-bar h-full rounded-full"
                :style="{ width: `${row.barPct}%`, animationDelay: `${60 + row.index * 70}ms` }"
              />
            </div>
          </div>
        </section>
      </div>

      <p v-if="state === 'ready' && rows.length" class="border-t border-default pt-2 text-[11px] text-muted">
        <b class="font-semibold text-default tabular-nums">{{ rows.length }}</b> racks
        <span class="mx-1">·</span><b class="font-semibold text-default tabular-nums">{{ totalSwitches }}</b> switches
        <span class="mx-1">·</span>avg <b class="font-semibold text-default tabular-nums">{{ avgSwitches }}</b> per rack
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
        Rank <b class="font-semibold text-default tabular-nums">{{ activeRow.index + 1 }}</b>
        of <b class="font-semibold text-default tabular-nums">{{ rows.length }}</b> racks
      </p>
    </div>
  </Teleport>
</template>

<style scoped>
/* The strip: pages side by side, one page visible, snapping to the page boundary so a swipe lands on a
   whole page instead of cutting a cell in half. */
.rack-strip {
  gap: 0.75rem;
  overflow-x: auto;
  overscroll-behavior-x: contain;
  scroll-snap-type: x mandatory;
  /* The bar is hidden rather than styled: a 6px scrollbar across the card would sit right under the
     cells and read as a divider. Swipe, trackpad, shift+wheel and the arrows all still work. */
  scrollbar-width: none;
  -ms-overflow-style: none;
}

.rack-strip::-webkit-scrollbar {
  display: none;
}

/* While a mouse drag is in progress: no snap (it would yank the strip back to a boundary under the
   pointer), and no text selection inside the cells the pointer is sweeping over. */
.rack-strip--dragging {
  scroll-snap-type: none;
  user-select: none;
}

/* One page per view: two columns by four rows, always the full width of the strip. Four `auto` rows
   share the strip's height, so the page is exactly as tall as the strip no matter how many rows the
   rack count needs - that is what keeps the card a fixed height.
   The rows are packed tightly (4px gap, py-1 cells, mt-0.5 bar) because a 4-row page needs ~30px more
   strip than a 2-row page, and that difference is what pushed the whole dashboard past a 950px-tall
   viewport: at 1500x950 the card went 332 -> 350px and an 11px page scrollbar appeared. */
.rack-page {
  display: grid;
  flex: 0 0 100%;
  min-width: 100%;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  grid-template-rows: repeat(4, auto);
  gap: 0.25rem;
  scroll-snap-align: start;
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