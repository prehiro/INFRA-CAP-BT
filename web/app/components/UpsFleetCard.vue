<script setup lang="ts">
/**
 * UPS Fleet card for the dashboard (replaces the Network Racks tile).
 *
 * WHAT IT ANSWERS
 * 1. The ribbon: which BRANDS the UPS register is made of and how they split (APC vs the rest).
 * 2. The bars: which MODELS, ranked - the list a maintenance/spares conversation actually needs.
 *    Each bar wears its brand's colour, so the ribbon doubles as the legend for the bars.
 * 3. Hover anything: a tooltip with the unit count, the share of the register and, per model, how
 *    many distinct locations that model covers.
 *
 * WHY BRAND COLOUR AND NOT ONE COLOUR PER MODEL
 * There are two brands and four models: colour-per-model would make the ribbon unreadable (four
 * colours for two brands) and would invent a distinction the data does not have. Colour-per-brand
 * lets the ribbon and the bars explain each other.
 *
 * WHY NOT A DOUGHNUT
 * 16/4 is a 80/20 split - a pie of that carries almost no information (see the two doughnuts already
 * on this page). A ribbon states the brand split in 10px and leaves the vertical space to the four
 * models, which is the dimension that varies.
 *
 * The tooltip is hand-rolled and teleported for the same reason as ChassisMixCard: the trigger can be
 * an SVG-free HTML row, but a teleported panel is what keeps the card's own overflow from clipping it.
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

const COLORS = ['var(--ui-primary)', '#38BDF8', '#A78BFA', '#F59E0B', '#F472B6', '#2DD4BF', '#F87171']

// Two roots (card + teleported tooltip): without this the parent's class cannot fall through and the
// grid column span passed from index.vue is dropped.
defineOptions({ inheritAttrs: false })

/** Brands in the register, ranked, each with the colour its bars will wear. */
const brands = computed(() => {
  const map = new Map<string, number>()
  for (const m of props.models) map.set(m.brand, (map.get(m.brand) ?? 0) + m.count)
  const sum = props.total || 1
  return [...map.entries()]
    .map(([label, count], i) => ({
      label,
      count,
      pct: (count / sum) * 100,
      color: COLORS[i % COLORS.length],
      index: i
    }))
    .sort((a, b) => b.count - a.count)
    .map((b, i) => ({ ...b, index: i }))
})

const colorOf = (brand: string) =>
  brands.value.find(b => b.label === brand)?.color ?? COLORS[0]

/** Model rows with their share, wearing their brand's colour. */
const rows = computed(() => {
  const sum = props.total || 1
  return props.models.map((m, i) => ({
    ...m,
    pct: (m.count / sum) * 100,
    color: colorOf(m.brand),
    index: i
  }))
})

/** What the pointer is on: a model row or a brand chip. Both dim the other's marks. */
const hover = ref<{ type: 'model' | 'brand', index: number } | null>(null)
const tip = ref<{ x: number, y: number } | null>(null)

const activeRow = computed(() =>
  hover.value?.type === 'model' ? rows.value[hover.value.index] ?? null : null)
const activeBrand = computed(() =>
  hover.value?.type === 'brand' ? brands.value[hover.value.index] ?? null : null)
const brandModels = computed(() =>
  activeBrand.value
    ? rows.value.filter(r => r.brand === activeBrand.value!.label)
    : [])

/** Rows the current hover is NOT about - they step back instead of the hovered one shouting. */
const rowDim = (row: ModelRow & { index: number }) => {
  if (!hover.value) return false
  if (hover.value.type === 'model') return hover.value.index !== row.index
  return row.brand !== brands.value[hover.value.index]?.label
}
const brandDim = (brand: { index: number }) => {
  if (!hover.value) return false
  return hover.value.type === 'brand'
    ? hover.value.index !== brand.index
    : rows.value[hover.value.index]?.brand !== brand.label
}

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
  const mouse = ev as MouseEvent | FocusEvent
  const cx = (mouse as MouseEvent).clientX
  const cy = (mouse as MouseEvent).clientY
  // Keyboard focus carries no pointer coordinates, so anchor to the element itself.
  tip.value = {
    x: typeof cx === 'number' && cx !== 0 ? cx : r.x + r.width / 2,
    y: typeof cy === 'number' && cy !== 0 ? cy : r.y
  }
}

const enterModel = (i: number, ev: Event) => { hover.value = { type: 'model', index: i }; pointAt(ev) }
const enterBrand = (i: number, ev: Event) => { hover.value = { type: 'brand', index: i }; pointAt(ev) }
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
         grid's row height (397px, set by the treemap next door) leaves ~90px dead at the bottom. -->
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
        <!-- Brand ribbon: 10px that says "80 / 20" without a pie. -->
        <div>
          <div class="flex h-2.5 w-full overflow-hidden rounded-full bg-elevated">
            <span
              v-for="brand in brands"
              :key="brand.label"
              class="ribbon-seg h-full"
              :style="{
                width: `${brand.pct}%`,
                backgroundColor: brand.color,
                animationDelay: `${brand.index * 110}ms`,
                opacity: brandDim(brand) ? 0.3 : 1
              }"
            />
          </div>
          <ul class="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1">
            <li
              v-for="brand in brands"
              :key="brand.label"
              class="flex cursor-default items-center gap-1.5 text-[11px] transition-opacity"
              :class="brandDim(brand) ? 'opacity-40' : ''"
              tabindex="0"
              @mouseenter="enterBrand(brand.index, $event)"
              @mousemove="moveTip"
              @mouseleave="clearTip"
              @focus="enterBrand(brand.index, $event)"
              @blur="clearTip"
            >
              <span class="size-1.5 shrink-0 rounded-full" :style="{ backgroundColor: brand.color }" />
              <span class="text-muted">{{ brand.label }}</span>
              <span class="font-semibold tabular-nums">{{ brand.count }}</span>
              <span class="text-muted tabular-nums">{{ brand.pct.toFixed(0) }}%</span>
            </li>
          </ul>
        </div>

        <!-- Ranked models. The track is the card's own elevated surface, so a 10% bar still reads. -->
        <!-- gap-1.5, not gap-2: at a 240px card (1600px window) the extra 6px pushed the dashboard 15px past the viewport. -->
        <ul class="flex flex-col gap-1.5">
          <li
            v-for="row in rows"
            :key="row.label"
            class="cursor-default transition-opacity duration-200"
            :class="rowDim(row) ? 'opacity-35' : ''"
            tabindex="0"
            @mouseenter="enterModel(row.index, $event)"
            @mousemove="moveTip"
            @mouseleave="clearTip"
            @focus="enterModel(row.index, $event)"
            @blur="clearTip"
          >
            <!-- Below ~230px of card the model name has to win over the "%": the number is still in the
                 tooltip and the bar already carries the proportion, while a truncated "Smart-UPS
                 SMT3000…" loses the part that identifies the unit. -->
            <div class="flex items-baseline justify-between gap-2">
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
      v-if="tip && (activeRow || activeBrand)"
      class="pointer-events-none fixed z-[70] rounded-lg border border-default bg-default p-3 shadow-xl"
      :style="[tipStyle, { width: `${TIP_W}px` }]"
      role="tooltip"
    >
      <template v-if="activeRow">
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
      </template>

      <template v-else-if="activeBrand">
        <p class="flex items-center gap-2 text-xs font-semibold">
          <span class="size-2.5 shrink-0 rounded-sm" :style="{ backgroundColor: activeBrand.color }" />
          <span class="min-w-0 flex-1 truncate">{{ activeBrand.label }}</span>
          <span class="shrink-0 tabular-nums">{{ activeBrand.pct.toFixed(1) }}%</span>
        </p>
        <p class="mt-0.5 text-[10px] text-muted tabular-nums">
          {{ activeBrand.count }} unit · {{ brandModels.length }} model
        </p>
        <div class="mt-2 space-y-1 border-t border-default/70 pt-2">
          <div v-for="m in brandModels" :key="m.label" class="flex items-center gap-2 text-[11px]">
            <span class="min-w-0 flex-1 truncate">{{ m.label }}</span>
            <span class="w-6 shrink-0 text-right font-medium tabular-nums">{{ m.count }}</span>
          </div>
        </div>
      </template>
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
