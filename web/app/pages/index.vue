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
  <UDashboardPanel>
    <template #header>
      <PageHeader title="Dashboard" />
    </template>

    <template #body>
      <div class="space-y-6">
        <!-- Greeting lives in the body, not the navbar: the navbar is locked to 64px. -->
        <WelcomeBanner class="anim-fade-up" />

        <div class="anim-stagger mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <UCard to="/logbook/cctv" class="cursor-pointer transition-colors hover:border-primary">
            <div class="flex items-center justify-between gap-3">
              <div class="flex items-center gap-3">
                <UIcon name="i-lucide-video" class="size-8 shrink-0 text-success" />
                <div class="min-w-0">
                  <p class="text-2xl font-bold">{{ cctvCount }}</p>
                  <p class="truncate text-xs text-muted">CCTV Log Book</p>
                </div>
              </div>
              <UIcon name="i-lucide-chevron-right" class="size-4 shrink-0 text-muted" />
            </div>
          </UCard>
        </div>

        <UCard class="anim-fade-up mt-6">
          <template #header>
            <h2 class="font-semibold">Modules</h2>
          </template>
          <NuxtLink
            to="/logbook/cctv"
            class="flex items-center justify-between rounded-lg border border-default p-3 transition-colors hover:border-primary"
          >
            <div class="min-w-0">
              <p class="truncate font-medium">CCTV Log Book</p>
              <p class="text-xs text-muted">{{ cctvCount }} rows</p>
            </div>
            <UIcon name="i-lucide-chevron-right" class="size-4 shrink-0 text-muted" />
          </NuxtLink>
        </UCard>
      </div>
    </template>
  </UDashboardPanel>
</template>