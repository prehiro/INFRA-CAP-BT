<script setup lang="ts">
// Geometry: Iconify lucide:video - the same icon the sidebar uses for CCTV Access.
// Decorative motion only, not live camera status.
</script>

<template>
  <svg class="cctv-glyph" viewBox="0 0 24 24" fill="none" aria-hidden="true" focusable="false">
    <g class="cctv-outline" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
      <rect x="2" y="6" width="14" height="12" rx="2" />
      <path d="m16 13 5.223 3.482a.5.5 0 0 0 .777-.416V7.87a.5.5 0 0 0-.752-.432L16 10.5" />
    </g>
    <g class="cctv-motion" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round">
      <!-- The recording lens dot, echoing the PC Ledger power light. -->
      <circle class="cctv-lens" cx="5" cy="9" r="1.15" stroke-width="1.6" />
      <!-- A trace running the outline of the body, the same travelling dash as PC Ledger. -->
      <rect class="cctv-trace" pathLength="100" x="2" y="6" width="14" height="12" rx="2" />
      <path class="cctv-trace cctv-trace-head" pathLength="100" d="m16 13 5.223 3.482a.5.5 0 0 0 .777-.416V7.87a.5.5 0 0 0-.752-.432L16 10.5" />
      <!-- A scan line travelling down the body, the CCTV counterpart of the PC screen scan. -->
      <g class="cctv-scan">
        <rect x="3.1" y="7" width="11.8" height="1.7" fill="currentColor" fill-opacity=".16" stroke="none" />
        <path d="M3.1 8.85h11.8" stroke-width=".85" />
      </g>
      <path class="cctv-beam" pathLength="100" d="M16 10.5v3" />
    </g>
  </svg>
</template>

<style scoped>
.cctv-glyph { display: block; overflow: visible; }
.cctv-outline { opacity: .72; }
.cctv-motion { pointer-events: none; }

/* Same timings as PcLedgerIcon so the two registers read as one family. */
.cctv-trace {
  --trace-start: 100;
  --trace-end: -18;
  stroke-dasharray: 18 82;
  stroke-width: 1.5;
  opacity: 0;
  animation: cctv-line-travel 6.4s cubic-bezier(.4,0,.2,1) infinite;
}
.cctv-trace-head {
  --trace-start: 85;
  --trace-end: -33;
  stroke-dasharray: 3 97;
  stroke-width: 1.9;
  filter: drop-shadow(0 0 .55px currentColor);
  animation-delay: .35s;
}
.cctv-lens {
  transform-box: fill-box;
  transform-origin: center;
  opacity: 0;
  animation: cctv-lens-echo 6.4s cubic-bezier(.16,1,.3,1) infinite;
}
.cctv-scan {
  opacity: 0;
  animation: cctv-screen-scan 6.4s cubic-bezier(.45,0,.25,1) infinite;
}
.cctv-beam {
  stroke-dasharray: 100;
  opacity: 0;
  animation: cctv-beam-load 6.4s ease-out infinite;
}

@keyframes cctv-line-travel {
  0% { stroke-dashoffset: var(--trace-start); opacity: 0; }
  6% { opacity: 1; }
  40% { stroke-dashoffset: var(--trace-end); opacity: 1; }
  47%, 100% { stroke-dashoffset: var(--trace-end); opacity: 0; }
}
@keyframes cctv-screen-scan {
  0%, 12% { transform: translateY(0); opacity: 0; }
  18% { opacity: .85; }
  40% { transform: translateY(7.4px); opacity: .85; }
  46%, 100% { transform: translateY(7.4px); opacity: 0; }
}
@keyframes cctv-lens-echo {
  0%, 48% { transform: scale(.6); opacity: 0; }
  52% { opacity: .7; }
  70%, 100% { transform: scale(1.65); opacity: 0; }
}
@keyframes cctv-beam-load {
  0%, 30% { stroke-dashoffset: 100; opacity: 0; }
  34% { opacity: 1; }
  42%, 52% { stroke-dashoffset: 0; opacity: 1; }
  60%, 100% { stroke-dashoffset: 0; opacity: 0; }
}

@media (prefers-reduced-motion: reduce), print {
  .cctv-outline { opacity: 1; }
  .cctv-motion { display: none; }
  .cctv-motion > * { animation: none; }
}
</style>
