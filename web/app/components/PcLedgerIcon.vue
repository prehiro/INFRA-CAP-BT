<script setup lang="ts">
const scanId = `pc-scan-${useId()}`
</script>

<template>
  <!-- Original geometry: Iconify andrsrxn:device-pc-cpu. Decorative motion only, not live PC status. -->
  <svg class="pc-ledger-glyph" viewBox="0 0 24 24" fill="none" aria-hidden="true" focusable="false">
    <defs>
      <linearGradient :id="scanId" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stop-color="currentColor" stop-opacity="0" />
        <stop offset="1" stop-color="currentColor" stop-opacity=".24" />
      </linearGradient>
    </defs>
    <g class="pc-outline" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
      <rect x="12.74" y="4.26" width="9.57" height="15.48" rx="2" />
      <path d="M12.74 5.49H5.7c-1.89 0-2.83 0-3.42.59c-.58.58-.58 1.52-.58 3.41v2.95c0 1.89 0 2.83.58 3.42c.59.58 1.53.58 3.42.58h6.8m-6.15 3.02h4.31m-2.16 0v-2.98" />
      <path d="M15.94 7.86h3.39m-3.39 2.94h3.39" />
      <path fill="currentColor" d="M17.53 15.03a.56.56 0 1 1 0 1.12a.56.56 0 0 1 0-1.12" />
    </g>
    <path fill="currentColor" opacity=".12" d="M1.69 9.49c0-1.89 0-2.83.58-3.41c.59-.59 1.53-.59 3.42-.59h3.05c1.89 0 2.83 0 3.42.59c.58.58.58 1.52.58 3.41v2.95c0 1.89 0 2.83-.58 3.42c-.59.58-1.53.58-3.42.58H5.7c-1.89 0-2.83 0-3.42-.58c-.58-.59-.58-1.53-.58-3.42z" />
    <g class="pc-motion" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
      <path class="pc-trace pc-monitor-trace" pathLength="100" d="M12.74 5.49H5.7c-1.89 0-2.83 0-3.42.59c-.58.58-.58 1.52-.58 3.41v2.95c0 1.89 0 2.83.58 3.42c.59.58 1.53.58 3.42.58h6.8" />
      <rect class="pc-trace pc-tower-trace" pathLength="100" x="12.74" y="4.26" width="9.57" height="15.48" rx="2" />
      <path class="pc-trace pc-trace-head pc-monitor-trace" pathLength="100" d="M12.74 5.49H5.7c-1.89 0-2.83 0-3.42.59c-.58.58-.58 1.52-.58 3.41v2.95c0 1.89 0 2.83.58 3.42c.59.58 1.53.58 3.42.58h6.8" />
      <rect class="pc-trace pc-trace-head pc-tower-trace" pathLength="100" x="12.74" y="4.26" width="9.57" height="15.48" rx="2" />
      <g class="pc-scan">
        <rect x="3.6" y="6.1" width="7.1" height="1.9" :fill="`url(#${scanId})`" stroke="none" />
        <path d="M3.6 8h7.1" stroke-width=".85" />
      </g>
      <path class="pc-vent pc-vent-one" pathLength="100" d="M15.94 7.86h3.39" />
      <path class="pc-vent pc-vent-two" pathLength="100" d="M15.94 10.8h3.39" />
      <circle class="pc-power" cx="17.53" cy="15.59" r="1.6" stroke-width=".8" />
    </g>
  </svg>
</template>

<style scoped>
.pc-ledger-glyph { display: block; overflow: visible; }
.pc-outline { opacity: .72; }
.pc-motion { pointer-events: none; }
.pc-trace { --trace-start: 100; --trace-end: -18; stroke-dasharray: 18 82; stroke-width: 1.5; opacity: 0; animation: pc-line-travel 6.4s cubic-bezier(.4,0,.2,1) infinite; }
.pc-trace-head { --trace-start: 85; --trace-end: -33; stroke-dasharray: 3 97; stroke-width: 1.9; filter: drop-shadow(0 0 .55px currentColor); }
.pc-tower-trace { animation-delay: .35s; }
.pc-scan { opacity: 0; animation: pc-screen-scan 6.4s cubic-bezier(.45,0,.25,1) infinite; }
.pc-vent { stroke-dasharray: 100; opacity: 0; animation: pc-vent-load 6.4s ease-out infinite; }
.pc-vent-two { animation-delay: .22s; }
.pc-power { transform-box: fill-box; transform-origin: center; opacity: 0; animation: pc-power-echo 6.4s cubic-bezier(.16,1,.3,1) infinite; }
@keyframes pc-line-travel {
  0% { stroke-dashoffset: var(--trace-start); opacity: 0; }
  6% { opacity: 1; }
  40% { stroke-dashoffset: var(--trace-end); opacity: 1; }
  47%, 100% { stroke-dashoffset: var(--trace-end); opacity: 0; }
}
@keyframes pc-screen-scan {
  0%, 12% { transform: translateY(0); opacity: 0; }
  18% { opacity: .85; }
  40% { transform: translateY(6px); opacity: .85; }
  46%, 100% { transform: translateY(6px); opacity: 0; }
}
@keyframes pc-vent-load {
  0%, 30% { stroke-dashoffset: 100; opacity: 0; }
  34% { opacity: 1; }
  42%, 52% { stroke-dashoffset: 0; opacity: 1; }
  60%, 100% { stroke-dashoffset: 0; opacity: 0; }
}
@keyframes pc-power-echo {
  0%, 48% { transform: scale(.6); opacity: 0; }
  52% { opacity: .7; }
  70%, 100% { transform: scale(1.65); opacity: 0; }
}
@media (prefers-reduced-motion: reduce), print {
  .pc-outline { opacity: 1; }
  .pc-motion { display: none; }
  .pc-motion > * { animation: none; }
}
</style>
