<template>
  <!-- UPS header glyph - a battery with a charge bolt, drawn in the same stroke family as the other
       Device Ledger title icons (PcLedgerIcon / StandbyIcon). The page passes `pc-add-tilt` so the
       whole mark shares the register pages' tilt animation. -->
  <svg class="ups-glyph" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">
    <rect class="ups-glyph-shell" x="2.75" y="6.5" width="15.5" height="11" rx="2.25" />
    <path class="ups-glyph-cap" d="M20.75 10.5v3" />
    <path class="ups-glyph-bolt" d="M10.4 9.2 7.4 13.1h3.2l-1 2.9 3.1-4h-3.2z" />
  </svg>
</template>

<style scoped>
.ups-glyph { width: 100%; height: 100%; }

/* Charge pulse: the bolt brightens on a slow cycle, the way the sidebar's battery mark reads as
   "live". Opacity only - no layout, no transform - so it cannot shift the title row. */
.ups-glyph-bolt {
  fill: currentColor;
  opacity: 0.85;
  animation: ups-charge 3.2s ease-in-out infinite;
}
.ups-glyph-shell { stroke-dasharray: 62; stroke-dashoffset: 0; }

@keyframes ups-charge {
  0%, 100% { opacity: 0.55; }
  50% { opacity: 1; }
}

@media (prefers-reduced-motion: reduce) {
  .ups-glyph-bolt { animation: none; opacity: 0.85; }
}
</style>
