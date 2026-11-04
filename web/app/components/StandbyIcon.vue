<script setup lang="ts">
const scanId = `standby-scan-${useId()}`
</script>

<template>
  <!-- Standby PC header glyph — mirrors PcLedgerIcon line-animation family,
       but carries the sidebar's streamline-flex:desktop-screensaver-sleep shape. -->
  <svg class="standby-glyph" viewBox="0 0 14 14" fill="none" aria-hidden="true" focusable="false">
    <defs>
      <linearGradient :id="scanId" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stop-color="currentColor" stop-opacity="0" />
        <stop offset="1" stop-color="currentColor" stop-opacity=".24" />
      </linearGradient>
    </defs>
    <g class="st-outline" stroke="currentColor" stroke-width=".8" stroke-linecap="round" stroke-linejoin="round">
      <!-- Monitor body outline -->
      <path d="m8.471 10.42l.64 2.708H4.889l.639-2.708m-2.27 2.708h7.485" />
      <path d="M5.937.88c-1.401.023-2.77.095-4.067.26a.94.94 0 0 0-.803.72C.828 2.902.75 3.833.75 5.646c0 1.737.2 2.68.364 3.665c.074.443.42.798.865.851c1.593.19 3.29.258 5.021.258c1.735 0 3.403-.068 5.018-.259c.447-.053.794-.409.867-.853c.058-.354.12-.705.177-1.085" />
      <!-- Sleep arc -->
      <path d="M10.53 6.594c1.466 0 2.458-.612 2.873-1.733c.094-.253-.129-.494-.399-.494c-1.98 0-3.093-1.114-3.093-3.094q0-.08.003-.158c.01-.338-.273-.64-.595-.539C8.102.957 7.437 1.97 7.437 3.5c0 1.98 1.113 3.094 3.093 3.094" />
    </g>
    <g class="st-motion" stroke="currentColor" stroke-width=".9" stroke-linecap="round" stroke-linejoin="round">
      <!-- Line travel traces -->
      <path class="st-trace" pathLength="100" d="m8.471 10.42l.64 2.708H4.889l.639-2.708" />
      <path class="st-trace" pathLength="100" d="M5.937.88c-1.401.023-2.77.095-4.067.26a.94.94 0 0 0-.803.72C.828 2.902.75 3.833.75 5.646c0 1.737.2 2.68.364 3.665c.074.443.42.798.865.851c1.593.19 3.29.258 5.021.258c1.735 0 3.403-.068 5.018-.259" />
      <path class="st-trace" pathLength="100" d="M10.53 6.594c1.466 0 2.458-.612 2.873-1.733" />
    </g>
    <g class="st-scan">
      <rect x="4.5" y="2.5" width="6" height="1.2" :fill="`url(#${scanId})`" stroke="none" />
      <path d="M4.5 3.7h6" stroke-width=".85" />
    </g>
  </svg>
</template>

<style scoped>
.standby-glyph { display: block; overflow: visible; margin: 0 auto; }
.st-outline { opacity: .72; }
.st-motion { pointer-events: none; }
.st-trace { --trace-start: 100; --trace-end: -18; stroke-dasharray: 18 82; stroke-width: .9; opacity: 0; animation: st-line-travel 6.4s cubic-bezier(.4,0,.2,1) infinite; }
.st-trace:nth-child(2) { animation-delay: .35s; }
.st-trace:nth-child(3) { animation-delay: .7s; }
.st-scan { opacity: 0; animation: st-screen-scan 6.4s cubic-bezier(.45,0,.25,1) infinite; }

@keyframes st-line-travel {
  0% { stroke-dashoffset: var(--trace-start); opacity: 0; }
  6% { opacity: 1; }
  40% { stroke-dashoffset: var(--trace-end); opacity: 1; }
  47%, 100% { stroke-dashoffset: var(--trace-end); opacity: 0; }
}
@keyframes st-screen-scan {
  0%, 12% { transform: translateY(0); opacity: 0; }
  18% { opacity: .85; }
  40% { transform: translateY(4px); opacity: .85; }
  46%, 100% { transform: translateY(4px); opacity: 0; }
}
@media (prefers-reduced-motion: reduce), print {
  .st-outline { opacity: 1; }
  .st-motion, .st-scan { display: none; }
  .st-motion > *, .st-scan { animation: none; }
}
</style>
