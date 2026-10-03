<script setup lang="ts">
/**
 * Sidebar header: the app name and nothing else.
 *
 * This used to be a dropdown that switched the accent / neutral colour, mirroring the
 * template's TeamsMenu. HIRO removed it on the grounds that the colour picker already
 * lives in the footer user menu, and having the same two lists in two places was
 * redundant. So this is now static branding — no click target at all.
 */
const props = defineProps<{ collapsed?: boolean }>()

// Keeps the brand mark centred in the 64px collapsed sidebar.
// MEASURED: the header has px-4 (16px) and BrandMenu adds px-1 (4px), so the 32px mark
// sat at x=20..52 — its centre x=36 against a sidebar centre of x=32, i.e. 4px right.
// `-ms-1` pulls it back by exactly that 4px. Same treatment as the footer avatar, and the
// same reason: the vendor padding is not fully absorbed by the collapsed layout.
const shift = computed(() => (props.collapsed ? '-ms-1' : ''))
</script>

<template>
  <div class="flex items-center gap-2 px-1 py-2" :class="shift">
    <span class="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary text-inverted font-bold">
      IC
    </span>

    <span v-if="!collapsed" class="min-w-0 flex-1">
      <span class="block truncate text-sm font-bold leading-tight">INFRA-CAP</span>
      <span class="block truncate text-xs text-dimmed">Internal App</span>
    </span>
  </div>
</template>