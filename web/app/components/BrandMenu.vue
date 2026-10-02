<script setup lang="ts">
/**
 * Sidebar header: the app name and nothing else.
 *
 * This used to be a dropdown that switched the accent / neutral colour, mirroring the
 * template's TeamsMenu. HIRO removed it on the grounds that the colour picker already
 * lives in the footer user menu, and having the same two lists in two places was
 * redundant. So this is now static branding — no click target at all.
 */
defineProps<{ collapsed?: boolean }>()
</script>

<template>
  <div class="flex items-center gap-2 px-1 py-2">
    <span class="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary text-inverted font-bold">
      IC
    </span>

    <span v-if="!collapsed" class="min-w-0 flex-1">
      <span class="block truncate text-sm font-bold leading-tight">INFRA-CAP</span>
      <span class="block truncate text-xs text-dimmed">Internal App</span>
    </span>
  </div>
</template>