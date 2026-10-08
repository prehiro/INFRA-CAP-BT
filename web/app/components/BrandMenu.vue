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
    <span class="brand-mark relative flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary text-inverted">
      <span class="brand-ring" />
      <span class="brand-ring brand-ring--2" />
      <span class="brand-icon">
        <UIcon name="i-streamline-cyber:network" class="size-5" />
      </span>
    </span>

    <span v-if="!collapsed" class="min-w-0 flex-1">
      <span class="block truncate text-sm font-bold leading-tight">INFRA-BTCAP</span>
      <span class="block truncate text-xs text-dimmed">Information System Dept</span>
    </span>
  </div>
</template>

<style scoped>
/* Same three motions as the login mark, so the brand reads identically everywhere it appears:
   the glyph turns slowly, its brightness breathes, and two rings travel out and back.

   COLOUR IS A TOKEN HERE, unlike on the login page. That page is a fixed dark surface with a
   hardcoded #00dc82, but the sidebar follows the theme picker, so the ring uses `--ui-primary`
   and changes with the user's accent. A literal green would have been the one part of the mark
   that stopped matching when the accent was switched to violet.

   The badge ITSELF is untouched - same size, same solid primary fill, same white glyph - so the
   rings are absolutely positioned children and nothing scales a box that contains a glyph
   (a scaled glyph rasterises at the in-between size and reads soft while it moves).

   The rotation and the sway deliberately sit on two different elements: both want `transform`,
   and on a single element the later `animation` shorthand silently wins per property, which
   would leave one of the two not running at all. */
.brand-icon {
  display: grid;
  place-items: center;
  animation: brand-spin 18s linear infinite;
}

.brand-icon > * {
  animation: brand-sway 3.5s ease-in-out infinite;
}

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

@keyframes brand-spin {
  to { transform: rotate(360deg); }
}

@keyframes brand-sway {
  0%, 100% { opacity: 0.7; }
  50%      { opacity: 1; }
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