<script setup lang="ts">
/**
 * Treemap card - "PC per Model": one tile per model, the tile's AREA being that model's share of the
 * active PC list. A treemap earns its place over a pie here because a pie with twelve slices is
 * unreadable while a treemap still gives every model its own labelled rectangle.
 *
 * WHY THE LAYOUT IS MEASURED AND NOT A FIXED viewBox
 * Squarifying needs the real aspect ratio of the box, or the "squares" come out stretched and the small
 * tiles end up as slivers. A ResizeObserver feeds the container's pixel size into the layout, and the
 * tiles are absolutely positioned divs in that pixel space - HTML tiles, not SVG, because the labels
 * then wrap and ellipsize by themselves instead of being hand-truncated text nodes.
 *
 * SQUARIFY (Bruls et al.): walk the items largest-first, keep adding the next one to the current row
 * while the row's worst aspect ratio improves, then lay that row along the shorter side. That is what
 * keeps tiles close to square instead of long thin strips.
 *
 * COLOURS come from the same array as ChassisMixCard so the two cards read as one set, and index by
 * rank (largest tile first) so two neighbouring tiles never share a colour.
 */
type Item = { label: string, value: number }
type Rect = { x: number, y: number, w: number, h: number }
type Tile = Rect & { label: string, value: number, pct: number, color: string, index: number }

const props = withDefaults(defineProps<{
  items: Item[]
  total: number
  /** Card title. */
  title?: string
  /** Field label, e.g. "Computer Model". */
  label?: string
  /** Where the numbers come from, e.g. "PC aktif". */
  source?: string
  state?: 'loading' | 'ready' | 'error'
}>(), {
  title: 'PC per Model',
  label: 'Computer Model',
  source: 'PC aktif',
  state: 'ready'
})

const COLORS = ['var(--ui-primary)', '#38BDF8', '#A78BFA', '#F59E0B', '#F472B6', '#2DD4BF', '#F87171']

const hovered = ref<number | null>(null)

/** Aspect ratio of the worst-shaped cell in a row laid along `side`. 1 is a perfect square. */
function worstRatio(row: number[], side: number): number {
  const sum = row.reduce((n, a) => n + a, 0)
  const thickness = sum / side
  return row.reduce((worst, area) => {
    const aspect = Math.max((thickness * thickness) / area, area / (thickness * thickness))
    return Math.max(worst, aspect)
  }, 0)
}

/** Squarified treemap. Returns one pixel rect per item, in the same order as `items`. */
function squarify(items: { value: number }[], width: number, height: number): Rect[] {
  const total = items.reduce((n, i) => n + i.value, 0)
  if (!total || width <= 0 || height <= 0) return []

  const areas = items.map(i => (i.value / total) * width * height)
  const out: Rect[] = []
  let x = 0
  let y = 0
  let w = width
  let h = height
  let i = 0

  while (i < areas.length) {
    const side = Math.min(w, h)
    const row: number[] = []
    let sum = 0
    let best = Number.POSITIVE_INFINITY
    let j = i

    while (j < areas.length) {
      const area = areas[j]!
      const score = worstRatio([...row, area], side)
      // Stop before the ratio gets worse than the row we already have.
      if (row.length && score > best) break
      row.push(area)
      sum += area
      best = score
      j++
    }

    if (w >= h) {
      // Shorter side is vertical: a full-height strip on the left, cells stacked top to bottom.
      const thickness = sum / h
      let cy = y
      for (const area of row) {
        const cell = area / thickness
        out.push({ x, y: cy, w: thickness, h: cell })
        cy += cell
      }
      x += thickness
      w -= thickness
    }
    else {
      // Shorter side is horizontal: a full-width strip on top, cells side by side.
      const thickness = sum / w
      let cx = x
      for (const area of row) {
        const cell = area / thickness
        out.push({ x: cx, y, w: cell, h: thickness })
        cx += cell
      }
      y += thickness
      h -= thickness
    }
    i = j
  }

  return out
}

const root = ref<HTMLElement | null>(null)
const box = ref({ w: 0, h: 0 })
let observer: ResizeObserver | undefined

function measure() {
  const el = root.value
  if (!el) return
  const r = el.getBoundingClientRect()
  box.value = { w: Math.floor(r.width), h: Math.floor(r.height) }
}

// Watching the ref rather than measuring in onMounted: the treemap div only exists once the data is in
// (the skeleton renders first), so at onMounted time the ref is still null. A null check there returns
// early and leaves the card unmeasured forever - which is exactly how the first version showed an empty
// body with no error. flush: 'post' so the element is in the DOM before getBoundingClientRect.
watch(root, (el) => {
  observer?.disconnect()
  observer = undefined
  if (!el) return
  measure()
  observer = new ResizeObserver(measure)
  observer.observe(el)
}, { flush: 'post', immediate: true })

onUnmounted(() => observer?.disconnect())

const tiles = computed<Tile[]>(() => {
  const { w, h } = box.value
  if (!w || !h || !props.items.length) return []

  const sorted = [...props.items].sort((a, b) => b.value - a.value)
  const rects = squarify(sorted, w, h)
  const total = props.total || sorted.reduce((n, i) => n + i.value, 0)

  return sorted.map((it, i) => ({
    ...rects[i]!,
    label: it.label,
    value: it.value,
    pct: total ? (it.value / total) * 100 : 0,
    color: COLORS[i % COLORS.length]!,
    index: i
  }))
})
</script>

<template>
  <UCard :ui="{ header: 'px-4 py-3', body: 'p-0 min-h-0 flex-1' }" class="anim-fade-up @container flex flex-col overflow-hidden">
    <template #header>
      <header class="flex items-baseline justify-between gap-3">
        <div class="min-w-0">
          <h2 class="truncate text-sm font-semibold">{{ title }}</h2>
          <p class="truncate text-xs text-muted">{{ label }} · {{ source }}</p>
        </div>
        <span class="shrink-0 text-[11px] text-muted tabular-nums">{{ total }} unit</span>
      </header>
    </template>

    <div v-if="state === 'error'" class="py-6 text-center text-xs text-muted">
      Data model tidak bisa dimuat.
    </div>

    <div
      v-else-if="state === 'loading' && !tiles.length"
      class="grid h-full min-h-24 grid-cols-3 grid-rows-2 gap-px p-3 pt-0"
    >
      <span class="animate-pulse rounded bg-elevated/60" />
      <span class="animate-pulse rounded bg-elevated/60" />
      <span class="animate-pulse rounded bg-elevated/60" />
      <span class="animate-pulse rounded bg-elevated/60" />
      <span class="animate-pulse rounded bg-elevated/60" />
      <span class="animate-pulse rounded bg-elevated/60" />
    </div>

    <!-- The empty check keys off `items`, NOT `tiles`: `tiles` is empty until this very div is mounted
         and measured, so an empty-if guard here would hide the treemap forever and it could never
         grow its first tile. -->
    <div v-else-if="!items.length" class="py-6 text-center text-xs text-muted">
      Belum ada data model.
    </div>

    <div v-else class="flex h-full min-h-0 flex-col gap-3 p-3 pt-0 @min-[420px]:flex-row">
      <!-- Measured pixel box: every tile is absolutely positioned in it. -->
      <div ref="root" class="relative min-h-24 min-w-0 flex-1">
        <div
          v-for="tile in tiles"
          :key="tile.label"
          class="absolute p-px"
          :style="{ left: `${tile.x}px`, top: `${tile.y}px`, width: `${tile.w}px`, height: `${tile.h}px` }"
        >
          <UTooltip :text="`${tile.label} · ${tile.value} unit · ${tile.pct.toFixed(1)}%`">
            <div
              class="flex h-full w-full flex-col justify-between overflow-hidden rounded-[3px] p-1.5 transition-opacity duration-200"
              :style="{ backgroundColor: tile.color, opacity: hovered !== null && hovered !== tile.index ? 0.35 : 1 }"
              @mouseenter="hovered = tile.index"
              @mouseleave="hovered = null"
            >
              <!-- A 20-unit tile in a 12-tile treemap is ~1.5% of the area; the label would be a
                   clipped stub, so the label only appears where it can actually be read. -->
              <span
                v-if="tile.h >= 28 && tile.w >= 50"
                class="truncate text-[11px] font-medium leading-tight text-zinc-900/80"
              >{{ tile.label }}</span>
              <span
                v-if="tile.h >= 18"
                class="text-[13px] font-semibold leading-none tabular-nums text-zinc-900"
              >{{ tile.value }}</span>
            </div>
          </UTooltip>
        </div>
      </div>

      <!-- Ranked list beside the treemap. Two reasons it is here and not a legend row under each tile:
           the five 80-unit models are all the same size, so in a wide short box they pack into four
           identical tall bars (124x198 at a 640px card) and the chart says nothing the list does not -
           and this is the only place a 2-unit tile can show its name. Hovering a row highlights its tile. -->
      <!-- max-h + overflow: the list must not set the card's min-content height. Nine rows at a
           comfortable 21px made the card 397px tall, which is what pushed the 1440px layout from two
           rows to three and brought the page scrollbar back. Bounded here, the card stays at the row
           height its neighbours already set. -->
      <ul class="w-full shrink-0 space-y-px @min-[420px]:max-h-[172px] @min-[420px]:w-[176px] @min-[420px]:overflow-y-auto">
        <li v-for="tile in tiles" :key="tile.label">
          <button
            type="button"
            class="flex w-full items-center gap-2 rounded-md px-1.5 py-0 text-left text-[11px] transition-colors"
            :class="hovered === tile.index ? 'bg-elevated/70' : 'hover:bg-elevated/50'"
            :aria-label="`${tile.label}: ${tile.value} unit, ${tile.pct.toFixed(1)} persen`"
            @mouseenter="hovered = tile.index"
            @mouseleave="hovered = null"
          >
            <span class="size-2 shrink-0 rounded-sm" :style="{ backgroundColor: tile.color }" />
            <span class="min-w-0 flex-1 truncate">{{ tile.label }}</span>
            <span class="w-6 shrink-0 text-right text-muted tabular-nums">{{ tile.value }}</span>
            <span class="w-10 shrink-0 text-right font-medium tabular-nums">{{ tile.pct.toFixed(1) }}%</span>
          </button>
        </li>
      </ul>
    </div>
  </UCard>
</template>