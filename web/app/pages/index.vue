<script setup lang="ts">
/**
 * Dashboard landing page.
 *
 * WHAT IS ON IT
 * Welcome banner, the module tiles, and the live Server Metrics rail down the right edge - one tile
 * per module that has a page - plus two chassis doughnuts ("All Capacitor PC's" for the whole fleet,
 * "Active PC's" for the active PC Ledger list) and the Switch per Rack card (how many switches sit
 * in each rack, ranked).
 *
 * Factory PC and UPS Register tiles were removed: both were single-number counts of registers that
 * already have their own pages, and the two chassis doughnuts answer the question better.
 *
 * WHY TWO DOUGHNUTS AND NOT ONE
 * They answer different questions and count different populations: All Capacitor PC's is pc_ledger +
 * Factory PC (463), Active PC's is only the ACTIVE rows of the pc_ledger list (447 - the count the
 * PC Ledger page's own header shows). They are separate fetches on
 * purpose - one shared fetch is what made the two cards drift apart the last time they shared state.
 *
 * TILE SPANS
 * Every doughnut is one column and Switch per Rack is the only double-width card, so the grid holds
 * seven cells: 4+3 at four columns, 3+3+1 at three and 2+2+2+1 at two, where the double-width card
 * takes the last row by itself.
 *
 * WHY SWITCH PER RACK REPLACED THE ACCESS POINTS TILE
 * The AP tile was one number plus per-floor chips - it answered "how many APs" and nothing else, and
 * the count is already on the page's own header. The rack card has the same double-width footprint and
 * a question worth the space: which rack holds the most switches. It first carried a second track with
 * U usage as well, and that was cut (HIRO: "hapus saja grafik u nya pada dashboard jadi fokus ke jumlah
 * switch per rack") - two units in one row forced the reader to compare buah against slot, and the
 * rack page already shows capacity where it can be read against the elevation.
 *
 * Audit Log and User Management are deliberately NOT here: they are admin/system pages, not data
 * modules, and both are role-gated - a tile would be a dead end for anyone without Admin.
 */
const token = useToken()

const authHeaders = computed(() => (token.value ? { Authorization: `Bearer ${token.value}` } : {}))

const { data: entities } = await useFetch<EntityMeta[]>('/entities', {
  baseURL: useRuntimeConfig().public.apiBase,
  headers: authHeaders,
  default: () => []
})

/** One row per rack across every floor: switches it holds and how much U they occupy.
 *  GET /api/network/racks/summary - one call for the whole building, so the card does not walk
 *  floor by floor. usedU already includes the UPS reservation. */
type RackRow = {
  rackId: number
  rackName: string
  floorId: number
  floorName: string
  switchCount: number
}

const { data: rackRows, error: rackError } = await useFetch<RackRow[]>('/network/racks/summary', {
  baseURL: useRuntimeConfig().public.apiBase,
  headers: authHeaders,
  default: () => []
})

/** One row of the UPS register (api/Controllers/UpsController.cs). */
type UpsUnit = { id: number, brand: string, model: string, serialNumber: string, location: string, createdAt: string }

// The register is 20 rows, not the EAV record table, so one list call is the whole population - no
// distribution endpoint needed and nothing to aggregate server-side.
const { data: upsUnits, error: upsError } = await useFetch<UpsUnit[]>('/ups', {
  baseURL: useRuntimeConfig().public.apiBase,
  headers: authHeaders,
  default: () => []
})

const list = computed(() => (entities.value as EntityMeta[]) ?? [])

/** Resolved by slug, never by id: dev and prod databases number their entities differently. */
const pcLedgerId = computed(() => list.value.find(e => e.slug === 'pc_ledger')?.id ?? null)

type DistItem = { value: string, count: number, split?: { value: string, count: number }[] }
type Distribution = { field: string, label: string, total: number, items: DistItem[] }

const chassis = ref<{ value: string, count: number, parts: { label: string, count: number }[] }[]>([])
const chassisTotal = ref(0)
const chassisState = ref<'loading' | 'ready' | 'error'>('loading')

/** Resolved by slug, never by id: dev and prod databases number their entities differently. */
const factoryId = computed(() => list.value.find(e => e.slug === 'factory_pc')?.id ?? null)

/**
 * One list of chassis types, each carrying how many of them sit in each register - that is what the
 * hover tooltip breaks down.
 *
 * pc_ledger is the only one of the two with a status, so its rows come back split on asset_status;
 * Factory PC is a separate entity with no status, so all of its rows land in the "Factory PC" column.
 * "PC Ledger" collects the empty and 'Active' rows - exactly how pages/device/pcledger.vue reads them -
 * so the four add up to the type's own total and the pie's denominator is the whole fleet (463), not
 * the pc_ledger entity on its own (453 rows, 447 of them active).
 */
function mergeChassis(ledger: DistItem[], factory: DistItem[]) {
  const factoryOf = new Map(factory.map(i => [i.value, i.count]))
  const names = new Set([...ledger.map(i => i.value), ...factoryOf.keys()])
  const pick = (split: DistItem['split'], test: (v: string) => boolean) =>
    (split ?? []).filter(s => test(s.value)).reduce((n, s) => n + s.count, 0)

  return [...names]
    .map((name) => {
      const split = ledger.find(i => i.value === name)?.split
      const parts = [
        { label: 'PC Ledger', count: pick(split, v => v === '' || v === '(kosong)' || v === 'Active') },
        { label: 'Standby PC', count: pick(split, v => v === 'Standby') },
        { label: 'Factory PC', count: factoryOf.get(name) ?? 0 },
        { label: 'Dispose', count: pick(split, v => v === 'Plan Dispose') }
      ]
      return { value: name, count: parts.reduce((n, p) => n + p.count, 0), parts }
    })
    .filter(i => i.count > 0)
    .sort((a, b) => b.count - a.count)
}

// A plain $fetch rather than useFetch: the entity id only exists once /entities has answered, and a
// URL whose identity changes is exactly what useFetch does not want.
watch(pcLedgerId, async (id) => {
  if (!id) return
  chassisState.value = 'loading'
  const opts = { baseURL: useRuntimeConfig().public.apiBase, headers: authHeaders.value }
  const fid = factoryId.value
  try {
    // Two questions, answered in parallel: what the fleet is made of (split by register, for the
    // tooltip) and how many of each chassis the Factory PC entity holds. Each call fails on its own -
    // a prod database without asset_status must not cost us the doughnut.
    const [mix, factoryMix] = await Promise.all([
      $fetch<Distribution>(`/records/${id}/distribution?field=chassis&splitBy=asset_status`, opts).catch(() => null),
      fid
        ? $fetch<Distribution>(`/records/${fid}/distribution?field=chassis`, opts).catch(() => null)
        : Promise.resolve(null)
    ])
    if (!mix) {
      chassisState.value = 'error'
      return
    }
    chassis.value = mergeChassis(mix.items, factoryMix?.items ?? [])
    chassisTotal.value = chassis.value.reduce((n, i) => n + i.count, 0)
    chassisState.value = 'ready'
  }
  catch {
    // A failed pie must not take the rest of the dashboard with it - the tile says so and moves on.
    chassisState.value = 'error'
  }
}, { immediate: true })

/**
 * The second doughnut: the PC Ledger list, active rows only (447), split by chassis.
 *
 * It is deliberately a separate fetch from `chassis` above - that one counts the fleet (pc_ledger +
 * Factory PC = 463) and merging the two is what made the cards drift. No `parts`: the slice is already
 * a single register, so the tooltip has nothing to break it down into.
 */
const activeChassis = ref<{ value: string, count: number }[]>([])
const activeTotal = ref(0)
const activeState = ref<'loading' | 'ready' | 'error'>('loading')

/** An empty asset_status (or the literal 'Active') is an active PC - the same predicate
 *  pages/device/pcledger.vue filters its default table on. */
const isActive = (v: string) => v === '' || v === '(kosong)' || v === 'Active'

function activeOnly(rows: DistItem[]) {
  return rows
    .map(row => ({
      value: row.value,
      count: (row.split ?? []).filter(s => isActive(s.value)).reduce((n, s) => n + s.count, 0)
    }))
    .filter(i => i.count > 0)
}

// Same shape as the fetch above, but split by asset_status so only the active rows can be kept.
watch(pcLedgerId, async (id) => {
  if (!id) return
  activeState.value = 'loading'
  const res = await $fetch<Distribution>(
    `/records/${id}/distribution?field=chassis&splitBy=asset_status`,
    { baseURL: useRuntimeConfig().public.apiBase, headers: authHeaders.value }
  ).catch(() => null)

  if (!res) {
    activeState.value = 'error'
    return
  }
  activeChassis.value = activeOnly(res.items)
  // Summed from the slices, not taken from res.total: that one is the whole entity (453).
  activeTotal.value = activeChassis.value.reduce((n, i) => n + i.count, 0)
  activeState.value = 'ready'
}, { immediate: true })

const models = ref<{ label: string, value: number }[]>([])
const modelsTotal = ref(0)
const modelsState = ref<'loading' | 'ready' | 'error'>('loading')

watch(pcLedgerId, async (id) => {
  if (!id) return
  modelsState.value = 'loading'
  const res = await $fetch<Distribution>(
    `/records/${id}/distribution?field=computer_model&splitBy=asset_status`,
    { baseURL: useRuntimeConfig().public.apiBase, headers: authHeaders.value }
  ).catch(() => null)

  if (!res) {
    modelsState.value = 'error'
    return
  }
  models.value = res.items
    .map(row => ({
      label: row.value,
      value: (row.split ?? []).filter(s => isActive(s.value)).reduce((n, s) => n + s.count, 0)
    }))
    .filter(i => i.value > 0)
  // Summed from the tiles, not res.total: that one counts all 453 records, active or not.
  modelsTotal.value = models.value.reduce((n, i) => n + i.value, 0)
  modelsState.value = 'ready'
}, { immediate: true })

/**
 * UPS aggregates for the fleet card.
 *
 * One population: every row of the UPS register (20 units). Grouped by model for the bars, and the
 * brand is carried on each row so the card can derive the ribbon from the SAME rows - a second
 * grouping would be a second source of truth that can drift from the bars.
 */
const upsModels = computed(() => {
  const groups = new Map<string, { label: string, brand: string, count: number, places: Set<string> }>()
  for (const u of (upsUnits.value as UpsUnit[]) ?? []) {
    const label = u.model?.trim() || '(tanpa model)'
    const brand = u.brand?.trim() || '(tanpa merek)'
    const g = groups.get(label) ?? { label, brand, count: 0, places: new Set<string>() }
    g.count += 1
    if (u.location?.trim()) g.places.add(u.location.trim())
    groups.set(label, g)
  }
  return [...groups.values()]
    .map(g => ({ label: g.label, brand: g.brand, count: g.count, locations: g.places.size }))
    .sort((a, b) => b.count - a.count || a.label.localeCompare(b.label))
})

/** The card shows its own error line rather than an empty chart. */
const upsState = computed<'loading' | 'ready' | 'error'>(() => (upsError.value ? 'error' : 'ready'))

/** Summed from the rows, so the card's denominator is the register itself. */
const upsTotal = computed(() => upsModels.value.reduce((n, m) => n + m.count, 0))

/** Distinct locations across the register - the card's footer line. */
const upsLocations = computed(() =>
  new Set(((upsUnits.value as UpsUnit[]) ?? []).map(u => u.location?.trim()).filter(Boolean)).size
)

/** The card shows its own error line rather than an empty chart. */
const rackState = computed<'loading' | 'ready' | 'error'>(() => (rackError.value ? 'error' : 'ready'))
</script>

<template>
  <UDashboardPanel :ui="{ body: 'p-6' }">
    <template #header>
      <PageHeader title="Dashboard" />
    </template>

    <template #body>
      <!-- min-h-full + flex-1: the metrics column is as tall as the space left under the banner, so the
           panel reaches the bottom of the viewport instead of stopping where its content ends. -->
      <div class="flex min-h-full flex-col gap-6">
        <!-- Greeting lives in the body, not the navbar: the navbar is locked to 64px. -->
        <WelcomeBanner class="anim-fade-up" />

        <!-- Tiles on the left, metrics on the RIGHT. The split only happens at xl: below that the
             metrics panel is full width (its own @container keeps the three charts side by side when
             it has room, stacked when it is the narrow right column).
             min-h-0 on both children is what keeps the page scroll-free: a flex/grid item defaults to
             min-height:auto, so without it the tiles' min-content height pushes the whole page taller
             than the viewport and the app's page wrapper grows a scrollbar. -->
        <div class="grid grid-cols-1 gap-4 sm:gap-5 xl:min-h-0 xl:flex-1 xl:grid-cols-[minmax(0,1fr)_300px]">
          <!-- @container, not the viewport: the tile columns follow the width this block actually
               gets once the metrics column takes its 300px, so four columns only appear where four
               columns fit. 860px, not 1060: at 1060 a 1440-wide window only got three columns, which
               made three rows of cards and pushed the page past the viewport height. -->
          <div class="@container min-w-0 xl:h-full">
            <div class="anim-stagger grid grid-cols-1 gap-4 sm:gap-4 @min-[520px]:grid-cols-2 @min-[700px]:grid-cols-3 @min-[860px]:grid-cols-4 xl:h-full xl:auto-rows-fr">
              <ChassisMixCard
                class="col-span-1"
                title="Registered PC"
                icon="fleet"
                label="Overall PC"
                source="By form factor"
                :items="chassis"
                :total="chassisTotal"
                :state="chassisState"
              />

              <!-- The new doughnut, one slot right of the first: the PC Ledger list by chassis.
                   One column wide, like its neighbours. -->
              <ChassisMixCard
                class="col-span-1"
                title="Domain-Joined PC"
                icon="domain"
                label="Assigned to users"
                source="By form factor"
                to="/device/pcledger"
                :items="activeChassis"
                :total="activeTotal"
                :state="activeState"
              />

              <!-- Treemap of the active PCs per model, two columns wide: twelve categories is too many
                   for a pie but reads fine as tiles, and the width gives the small models a readable
                   block instead of a sliver. -->
              <TreemapCard
                class="col-span-1 @min-[560px]:col-span-2"
                title="Model Treemap"
                label="Domain-Joined PC"
                source="By brand/model"
                :items="models"
                :total="modelsTotal"
                :state="modelsState"
              />

              <!-- UPS Fleet, one column: the register's brand split as a ribbon and its four models
                   ranked underneath. Replaces the Network Racks tile, which was a single count of a
                   page that is still in the sidebar. -->
              <UpsFleetCard
                class="col-span-1"
                title="UPS Fleet"
                label="Registered units"
                source="By model"
                to="/device/ups"
                :models="upsModels"
                :total="upsTotal"
                :locations="upsLocations"
                :state="upsState"
              />

              <!-- Switch per Rack, two columns wide like the Access Points tile it replaces: one row
                   per rack, the bar is how many switches sit in it, ranked. It deliberately carries
                   switch counts only - a second track with U usage made every row a comparison of two
                   different units, and the rack page is where capacity is looked at properly. -->
              <RackCapacityCard
                class="col-span-1 @min-[560px]:col-span-2"
                title="Switch per Rack"
                label="Switch count"
                source="By rack"
                to="/networks/network-rack"
                :racks="(rackRows as RackRow[] ?? [])"
                :state="rackState"
              />
            </div>
          </div>

          <!-- System Metrics panel, right column. It renders its own UCard: a card inside a card
               would double the padding and the header. -->
          <MetricsChart />
        </div>
      </div>
    </template>
  </UDashboardPanel>
</template>