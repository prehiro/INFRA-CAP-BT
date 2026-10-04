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
    <!-- Brand mark. HIRO asked for the Iconify icon 'streamline-cyber:network' in place of
         the old "IC" letters (2026-10-04).

         OFFLINE NOTE, because this is not the usual situation. nuxt.config.ts sets
         clientBundle.scan so icons referenced in the source are resolved AT BUILD TIME and
         embedded, and clientBundleFallback: '' so nothing is ever fetched from
         api.iconify.design at runtime - the office server 10.89.6.237 has no internet and
         would silently drop anything fetched live. 'streamline' is NOT among the locally
         installed collections (only @iconify-json/lucide is), so this icon is bundled from
         the network by the build machine rather than read from node_modules. That is fine for
         a build here, but it means a `npm install` on a machine without internet could not
         re-resolve it. If the icon ever fails to appear, check the clientBundle `size`
         budget in nuxt.config.ts before anything else - it is 200000 and overrunning it drops
         icons silently. -->
    <span class="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary text-inverted">
      <UIcon name="i-streamline-cyber:network" class="size-5" />
    </span>

    <span v-if="!collapsed" class="min-w-0 flex-1">
      <span class="block truncate text-sm font-bold leading-tight">INFRA-BTCAP</span>
      <span class="block truncate text-xs text-dimmed">Information System Dept</span>
    </span>
  </div>
</template>