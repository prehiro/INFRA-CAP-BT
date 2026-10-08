<script setup lang="ts">
/**
 * Dashboard landing page.
 *
 * The Master Data / Transaksi sections were removed at HIRO's request, so the cards no
 * longer enumerate the runtime entity tree. What remains is the CCTV Log Book — the only
 * functional module in the app today — plus a direct entry point to it.
 */
const token = useToken()

const { data: entities } = await useFetch<EntityMeta[]>('/entities', {
  baseURL: useRuntimeConfig().public.apiBase,
  headers: computed(() => (token.value ? { Authorization: `Bearer ${token.value}` } : {})),
  default: () => []
})

const list = computed(() => (entities.value as EntityMeta[]) ?? [])
const CCTV_SLUG = 'cctv_log_book'
const cctv = computed(() => list.value.find(e => e.slug === CCTV_SLUG))
const cctvCount = computed(() => cctv.value?.recordCount ?? 0)
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
          <!-- CCTV Log Book Card -->
          <UCard
            to="/logbook/cctvacc"
            class="cursor-pointer group hover:-translate-y-1 transition-transform duration-300 shadow-lg hover:shadow-xl hover:border-primary/50"
          >
            <div class="flex items-center justify-between gap-4 p-6">
              <div class="flex items-center gap-4">
                <UIcon name="i-lucide-video" class="size-10 shrink-0 text-success" />
                <div class="min-w-0">
                  <p class="text-3xl font-bold tracking-tight">{{ cctvCount }}</p>
                  <p class="truncate text-xs text-muted">CCTV Access Requests</p>
                </div>
              </div>
              <UIcon name="i-lucide-chevron-right" class="size-5 shrink-0 text-muted opacity-75 group-hover:opacity-100" />
            </div>
          </UCard>

          <!-- Placeholder for Future Modules (styled as disabled card) -->
          <UCard class="opacity-50 pointer-events-none">
            <div class="flex items-center justify-between gap-4 p-6">
              <div class="flex items-center gap-4">
                <UIcon name="i-lucide-folder" class="size-10 shrink-0 text-muted" />
                <div class="min-w-0">
                  <p class="text-3xl font-bold tracking-tight opacity-50">0</p>
                  <p class="truncate text-xs text-muted">Handover Log</p>
                </div>
              </div>
              <UIcon name="i-lucide-chevron-right" class="size-5 shrink-0 text-muted opacity-25" />
            </div>
          </UCard>

          <!-- Another Placeholder -->
          <UCard class="opacity-50 pointer-events-none">
            <div class="flex items-center justify-between gap-4 p-6">
              <div class="flex items-center gap-4">
                <UIcon name="i-lucide-file-text" class="size-10 shrink-0 text-muted" />
                <div class="min-w-0">
                  <p class="text-3xl font-bold tracking-tight opacity-50">0</p>
                  <p class="truncate text-xs text-muted">Audit Log</p>
                </div>
              </div>
              <UIcon name="i-lucide-chevron-right" class="size-5 shrink-0 text-muted opacity-25" />
            </div>
          </UCard>
        </div>
      </div>
    </template>
  </UDashboardPanel>
</template>
