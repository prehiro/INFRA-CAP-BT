<template>
  <!-- Lucide monitor-x geometry: PC marked for disposal, matching the sidebar. -->
  <svg class="dispose-glyph" viewBox="0 0 24 24" fill="none" aria-hidden="true" focusable="false">
    <g class="dispose-outline" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
      <path d="m14.5 12.5l-5-5m0 5l5-5" />
      <rect width="20" height="14" x="2" y="3" rx="2" />
      <path d="M12 17v4m-4 0h8" />
    </g>
    <g class="dispose-motion" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
      <rect class="dispose-trace" pathLength="100" width="20" height="14" x="2" y="3" rx="2" />
      <path class="dispose-trace dispose-trace--stand" pathLength="100" d="M12 17v4m-4 0h8" />
      <path class="dispose-trace dispose-trace--cross-one" pathLength="100" d="m14.5 12.5l-5-5" />
      <path class="dispose-trace dispose-trace--cross-two" pathLength="100" d="m9.5 12.5l5-5" />
    </g>
  </svg>
</template>

<style scoped>
.dispose-glyph { display: block; overflow: visible; }
.dispose-outline { opacity: .72; }
.dispose-motion { pointer-events: none; }
.dispose-trace {
  stroke-dasharray: 18 82;
  stroke-dashoffset: 100;
  opacity: 0;
  animation: dispose-line-travel 6.4s cubic-bezier(.4, 0, .2, 1) infinite;
}
.dispose-trace--stand { animation-delay: .35s; }
.dispose-trace--cross-one { animation-delay: .6s; }
.dispose-trace--cross-two { animation-delay: .8s; }
@keyframes dispose-line-travel {
  0% { stroke-dashoffset: 100; opacity: 0; }
  6% { opacity: 1; }
  40% { stroke-dashoffset: -18; opacity: 1; }
  47%, 100% { stroke-dashoffset: -18; opacity: 0; }
}
@media (prefers-reduced-motion: reduce), print {
  .dispose-outline { opacity: 1; }
  .dispose-motion { display: none; }
  .dispose-trace { animation: none; }
}
</style>
