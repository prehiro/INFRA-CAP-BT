<script setup lang="ts">
/**
 * Handover Log Book — /logbook/handover
 *
 * Placeholder page created 2026-10-02 together with the "Log Book" sidebar group
 * (CCTV Access + Handover). It deliberately reuses the stock page shell — UDashboardPanel
 * with the shared PageHeader, which is what carries the sidebar collapse control.
 *
 * There is no `handover_log_book` entity in the database yet, so nothing here is wired to
 * the API. When the module is built it should follow the same pattern as cctvacc.vue:
 * a dedicated entity slug in SeedService, stored through the generic metadata engine, with
 * only the presentation bespoke to the paper form.
 */
</script>

<template>
  <UDashboardPanel>
    <template #header>
      <!-- PageHeader carries the sidebar collapse control in the navbar's #leading slot,
           exactly as the Nuxt dashboard template does on every page. -->
      <PageHeader title="Handover" />
    </template>

    <template #body>
      <div class="anim-fade-up">
        <UCard>
          <div class="flex flex-col items-center justify-center gap-3 py-14 text-center">
            <UIcon name="i-lucide-clipboard-list" class="size-10 text-dimmed" />
            <div>
              <h2 class="text-lg font-semibold">Handover Log Book</h2>
              <p class="mt-1 max-w-md text-sm text-muted">
                This module has not been built yet. It will follow the same pattern as
                CCTV Access: a dedicated entity in the generic metadata engine, with only
                the paper form layout written by hand.
              </p>
            </div>
          </div>
        </UCard>
      </div>
    </template>
  </UDashboardPanel>
</template>