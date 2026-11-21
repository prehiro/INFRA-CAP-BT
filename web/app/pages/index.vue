<script setup lang="ts">
/**
 * Dashboard landing page.
 *
 * WHAT IS ON IT
 * Welcome banner, the live Server Metrics chart, then one tile per module that actually has a page -
 * seven of them, each carrying its real record count. Until 2026-11-21 this grid held the CCTV tile
 * plus two hand-written placeholders ("0", opacity-50, pointer-events-none) for Handover and Audit,
 * even though both pages already existed; the numbers now come from the API instead of being typed in.
 *
 * WHY THE LAST TILE IS WIDE
 * Seven tiles do not fit a 3-column grid evenly (3 + 3 + 1 leaves a hole). The Access Point tile spans
 * the full row and carries its per-floor breakdown, so the last row is deliberately full instead of
 * accidentally short.
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
      <div class="space-y-8">
        <!-- Greeting lives in the body, not the navbar: the navbar is locked to 64px. -->
        <WelcomeBanner class="anim-fade-up" />

        <!-- System Metrics panel. It renders its own UCard: a card inside a card would
             double the padding and the header. -->
        <MetricsChart />

        <div class="anim-stagger mt-8 grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
          <DashboardCard
            v-for="card in [
              { to: '/logbook/cctvacc', label: 'CCTV Access Requests', value: countOf('cctv_log_book'), icon: 'i-lucide-video', tone: 'emerald' },
              { to: '/logbook/handover', label: 'Handover Log Book', value: countOf('handover_log_book'), icon: 'i-lucide-folder-check', tone: 'sky' },
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
    </template>
  </UDashboardPanel>
</template>