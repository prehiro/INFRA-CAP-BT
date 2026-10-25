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
    <!-- Shared local server/network mark; colors follow the sidebar theme. -->
    <span class="brand-mark relative flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary text-inverted">
      <span class="brand-ring" />
      <span class="brand-ring brand-ring--2" />
      <span class="brand-icon">
        <LoginNetworkIcon :size="22" />
      </span>
    </span>

    <span v-if="!collapsed" class="min-w-0 flex-1">
      <span class="block truncate text-sm font-bold tracking-wide leading-[1.4] text-black dark:text-white"><AnimatedBrandText intro :tracking="0.025" :line-height="1.4" style="color: inherit" /></span>
      <span class="block truncate text-xs text-dimmed">Information System Dept</span>
    </span>
  </div>
</template>

<style scoped>
.brand-icon { display: grid; place-items: center; }

.brand-ring {
  position: absolute;
  inset: 0;
  /* rounded-lg on the badge is 8px, so the ring has to match it or the corners double up. */
  border-radius: 8px;
  border: 1px solid color-mix(in oklab, var(--ui-primary) 75%, transparent);
  pointer-events: none;
  will-change: transform, opacity;
  animation: brand-ring 2.8s ease-in-out infinite;
}

.brand-ring--2 {
  animation-duration: 3.4s;
  animation-delay: -1.7s;
}

/* Back to scale(1) at the end of every cycle, so this reads as out AND back rather than as a
   one-way ping. The opacity is already near zero at the outer end, which is what keeps the far
   ring from drawing a line across the INFRA-BTCAP wordmark sitting just to its right. */
@keyframes brand-ring {
  0%, 100% { transform: scale(1);   opacity: 0.45; }
  50%      { transform: scale(1.6); opacity: 0.05; }
}

@media (prefers-reduced-motion: reduce) {
  .brand-icon,
  .brand-icon > *,
  .brand-ring {
    animation: none;
  }
}
</style>