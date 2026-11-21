<script setup lang="ts">
/**
 * Dashboard landing page.
 *
 * WHAT IS ON IT
 * Welcome banner, the live Server Metrics rail, then the Chassis Mix doughnut (spanning two tiles)
 * and one tile per module that has a page, each carrying its real record count.
 *
 * WHY THE CHASSIS DOUGHNUT REPLACED TWO TILES
 * The CCTV and Handover tiles only showed "13" and "5" - a number nobody can act on. The same two
 * columns now answer a question the PC Ledger data already holds: what the fleet is made of
 * (Desktop / Notebook / Tablet, and their share). The counts come from
 * /records/{id}/distribution?field=chassis, which groups in SQL instead of shipping 453 rows to the
 * browser to be counted client-side.
 *
 * TILE SPANS
 * The doughnut takes 2 of 2/3/4 columns and Access Points takes 3 of 3 / 2 of 2 / 2 of 4, so every
 * row is exactly full at every width: 4 cols = 2+1+1 / 1+1+2, 3 cols = 2+1 / 1+1+1 / 3, 2 cols =
 * 2 / 2 / 2 / 2.
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

const { data: ups } = await useFetch<UpsUnit[]>('/ups', {
  baseURL: useRuntimeConfig().public.apiBase,
  headers: authHeaders,
  default: () => []
})

const { data: rackFloors } = await useFetch<FloorMapSummary[]>('/network/floor-maps?scope=rack', {
  baseURL: useRuntimeConfig().public.apiBase,
  headers: authHeaders,
  default: () => []
})

const { data: apFloors } = await useFetch<FloorMapSummary[]>('/network/floor-maps?scope=ap', {
  baseURL: useRuntimeConfig().public.apiBase,
  headers: authHeaders,
  default: () => []
})

const list = computed(() => (entities.value as EntityMeta[]) ?? [])

/** Record count for a dynamic entity, straight from the /entities payload. */
const countOf = (slug: string) => list.value.find(e => e.slug === slug)?.recordCount ?? 0

/** Resolved by slug, never by id: dev and prod databases number their entities differently. */
const pcLedgerId = computed(() => list.value.find(e => e.slug === 'pc_ledger')?.id ?? null)

type DistItem = { value: string, count: number, split?: { value: string, count: number }[] }
type Distribution = { field: string, label: string, total: number, items: DistItem[] }

const chassis = ref<{ value: string, count: number, parts: { label: string, count: number }[] }[]>([])
const chassisTotal = ref(0)
const chassisLabel = ref('Computer Chassis')
const chassisState = ref<'loading' | 'ready' | 'error'>('loading')
const statusDist = ref<{ value: string, count: number }[]>([])

/** Resolved by slug, never by id: dev and prod databases number their entities differently. */
const factoryId = computed(() => list.value.find(e => e.slug === 'factory_pc')?.id ?? null)

/**
 * One list of chassis types, each carrying how many of them sit in each register.
 *
 * pc_ledger is the only one of the two with a status, so its rows come back split on asset_status;
 * Factory PC is a separate entity with no status, so all of its rows land in the "Factory PC" column.
 * "PC Ledger" collects the empty and 'Active' rows - exactly how pages/device/pcledger.vue reads them -
 * so the four columns add up to the type's own total and the pie's denominator matches the register
 * block underneath it (463, not 453).
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
    // Three questions, answered in parallel: what the fleet is made of, how that splits by register,
    // and which register each PC sits in. Each call fails on its own - a prod database without
    // asset_status must not cost us the doughnut.
    const [mix, status, factoryMix] = await Promise.all([
      $fetch<Distribution>(`/records/${id}/distribution?field=chassis&splitBy=asset_status`, opts).catch(() => null),
      $fetch<Distribution>(`/records/${id}/distribution?field=asset_status`, opts).catch(() => null),
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
    chassisLabel.value = mix.label
    statusDist.value = status?.items ?? []
    chassisState.value = 'ready'
  }
  catch {
    // A failed pie must not take the rest of the dashboard with it - the tile says so and moves on.
    chassisState.value = 'error'
  }
}, { immediate: true })

/**
 * A computer sits in exactly ONE register: PC Ledger (asset_status empty or "Active"), Standby, Dispose,
 * or the separate Factory PC entity. PC Ledger is the remainder rather than its own count, so the four
 * always add up to the real total even when a new status word appears in the data.
 */
const standbyCount = computed(() => statusDist.value.find(i => i.value === 'Standby')?.count ?? 0)
const disposeCount = computed(() => statusDist.value.find(i => i.value === 'Plan Dispose')?.count ?? 0)
const factoryCount = computed(() => countOf('factory_pc'))
const activeCount = computed(() =>
  Math.max(countOf('pc_ledger') - standbyCount.value - disposeCount.value, 0)
)
const registers = computed(() => [
  { label: 'PC Ledger', value: activeCount.value, to: '/device/pcledger' },
  { label: 'Factory PC', value: factoryCount.value, to: '/device/factorypc' },
  { label: 'Standby PC', value: standbyCount.value, to: '/device/standbypc' },
  { label: 'Dispose', value: disposeCount.value, to: '/device/dispose' }
])
const registerTotal = computed(() => registers.value.reduce((n, r) => n + r.value, 0))

const rackCount = computed(() =>
  (rackFloors.value as FloorMapSummary[] ?? []).reduce((n, f) => n + (f.rackCount ?? 0), 0)
)
const apCount = computed(() =>
  (apFloors.value as FloorMapSummary[] ?? []).reduce((n, f) => n + (f.accessPointCount ?? 0), 0)
)

const rackFloorCount = computed(() =>
  (rackFloors.value as FloorMapSummary[] ?? []).filter(f => (f.rackCount ?? 0) > 0).length
)
const apFloorCount = computed(() =>
  (apFloors.value as FloorMapSummary[] ?? []).filter(f => (f.accessPointCount ?? 0) > 0).length
)

/** Per-floor AP breakdown for the wide tile. Empty floors are skipped: a "0" chip is noise. */
const apChips = computed(() =>
  (apFloors.value as FloorMapSummary[] ?? [])
    .filter(f => (f.accessPointCount ?? 0) > 0)
    .map(f => ({ label: f.name, value: f.accessPointCount }))
)
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

        <!-- Metrics on the left, tiles on the right. The split only happens at xl: below that the
             metrics panel is full width (its own @container keeps the three charts side by side when
             it has room, stacked when it is the narrow left column). -->
        <div class="grid grid-cols-1 gap-5 xl:min-h-0 xl:flex-1 xl:grid-cols-[300px_minmax(0,1fr)]">
          <!-- System Metrics panel. It renders its own UCard: a card inside a card would
               double the padding and the header. -->
          <MetricsChart />

          <!-- @container, not the viewport: the tile columns follow the width this block actually
               gets once the metrics column takes its 300px, so four columns only appear where four
               columns fit. -->
          <div class="@container min-w-0 xl:h-full">
            <div class="anim-stagger grid grid-cols-1 gap-4 sm:gap-5 @min-[560px]:grid-cols-2 @min-[780px]:grid-cols-3 @min-[1060px]:grid-cols-4 xl:h-full xl:auto-rows-fr">
              <ChassisMixCard
                class="col-span-1 @min-[560px]:col-span-2"
                :items="chassis"
                :total="chassisTotal"
                :label="chassisLabel"
                :state="chassisState"
                :registers="registers"
                :register-total="registerTotal"
              />

              <DashboardCard
                v-for="card in [
                  { to: '/device/pcledger', label: 'PC Ledger', value: countOf('pc_ledger'), icon: 'i-lucide-monitor', tone: 'violet' },
                  { to: '/device/factorypc', label: 'Factory PC', value: countOf('factory_pc'), icon: 'i-lucide-laptop', tone: 'rose' },
                  { to: '/device/ups', label: 'UPS Register', value: (ups as UpsUnit[] ?? []).length, icon: 'i-lucide-battery-charging', tone: 'amber' },
                  { to: '/networks/network-rack', label: 'Network Racks', value: rackCount, sub: `${rackFloorCount} floor`, icon: 'i-lucide-server', tone: 'cyan' },
                  { to: '/networks/access-point', label: 'Access Points', value: apCount, sub: `${apFloorCount} floor`, icon: 'i-lucide-wifi', tone: 'primary', wide: true, chips: apChips }
                ]"
                :key="card.to"
                v-bind="card"
              />
            </div>
          </div>
        </div>
      </div>
    </template>
  </UDashboardPanel>
</template>