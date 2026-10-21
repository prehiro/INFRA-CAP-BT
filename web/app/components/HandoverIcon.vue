<script setup lang="ts">
// Geometry: Iconify lucide:clipboard-list - the same icon the sidebar uses for Handover.
// Decorative motion only, not live record status.
//
// The vendor path is one long path; it is split here into its parts so each can carry
// its own motion. The parts together are exactly the vendor geometry, nothing added.
</script>

<template>
  <svg class="handover-glyph" viewBox="0 0 24 24" fill="none" aria-hidden="true" focusable="false">
    <g class="handover-outline" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
      <rect class="handover-clip" x="8" y="2" width="8" height="4" rx="1" />
      <path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2" />
      <path d="M12 11h4" />
      <path d="M12 16h4" />
      <path d="M8 11h.01" />
      <path d="M8 16h.01" />
    </g>
    <g class="handover-motion" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round">
      <!-- The travelling dash along the board, the same trace as PC Ledger and CCTV. -->
      <path class="handover-trace" pathLength="100" d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2" />
      <path class="handover-trace handover-trace-head" pathLength="100" d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2" />
      <!-- The clip lifting off the board, the counterpart of the CCTV lens echo. -->
      <rect class="handover-clip-echo" x="8" y="2" width="8" height="4" rx="1" stroke-width="1.6" />
      <!-- A scan line running down the board, the counterpart of the CCTV screen scan. -->
      <g class="handover-scan">
        <rect x="5.4" y="7" width="13.2" height="1.7" fill="currentColor" fill-opacity=".16" stroke="none" />
        <path d="M5.4 8.85h13.2" stroke-width=".85" />
      </g>
      <!-- The two list rows filling in one after another, the counterpart of the PC vents. -->
      <path class="handover-row handover-row-one" pathLength="100" d="M12 11h4" />
      <path class="handover-row handover-row-two" pathLength="100" d="M12 16h4" />
    </g>
  </svg>
</template>

<style scoped>
.handover-glyph { display: block; overflow: visible; }
.handover-outline { opacity: .72; }
.handover-motion { pointer-events: none; }

/* Same timings as PcLedgerIcon so the two registers read as one family. */
.handover-trace {
  --trace-start: 100;
  --trace-end: -18;
  stroke-dasharray: 18 82;
  stroke-width: 1.5;
  opacity: 0;
  animation: handover-line-travel 6.4s cubic-bezier(.4,0,.2,1) infinite;
}
.handover-trace-head {
  --trace-start: 85;
  --trace-end: -33;
  stroke-dasharray: 3 97;
  stroke-width: 1.9;
  filter: drop-shadow(0 0 .55px currentColor);
  animation-delay: .35s;
}
.handover-clip-echo {
  transform-box: fill-box;
  transform-origin: center;
  opacity: 0;
  animation: handover-clip-lift 6.4s cubic-bezier(.16,1,.3,1) infinite;
}
.handover-scan {
  opacity: 0;
  animation: handover-board-scan 6.4s cubic-bezier(.45,0,.25,1) infinite;
}
.handover-row {
  stroke-dasharray: 100;
  opacity: 0;
  animation: handover-row-load 6.4s ease-out infinite;
}
.handover-row-two { animation-delay: .22s; }

@keyframes handover-line-travel {
  0% { stroke-dashoffset: var(--trace-start); opacity: 0; }
  6% { opacity: 1; }
  40% { stroke-dashoffset: var(--trace-end); opacity: 1; }
  47%, 100% { stroke-dashoffset: var(--trace-end); opacity: 0; }
}
@keyframes handover-board-scan {
  0%, 12% { transform: translateY(0); opacity: 0; }
  18% { opacity: .85; }
  40% { transform: translateY(8.4px); opacity: .85; }
  46%, 100% { transform: translateY(8.4px); opacity: 0; }
}
/* The clip echo is the SAME rectangle as the outline clip, so a plain opacity fade is
   invisible - it would only thicken a line that is already there. It is therefore scaled
   slightly as it lifts, which reads as the clip peeling off the board. */
@keyframes handover-clip-lift {
  0%, 48% { transform: translateY(0) scale(1); opacity: 0; }
  52% { transform: translateY(-1.2px) scale(1.06); opacity: .8; }
  70%, 100% { transform: translateY(-2.6px) scale(1.12); opacity: 0; }
}
@keyframes handover-row-load {
  0%, 30% { stroke-dashoffset: 100; opacity: 0; }
  34% { opacity: 1; }
  42%, 52% { stroke-dashoffset: 0; opacity: 1; }
  60%, 100% { stroke-dashoffset: 0; opacity: 0; }
}

@media (prefers-reduced-motion: reduce), print {
  .handover-outline { opacity: 1; }
  .handover-motion { display: none; }
  .handover-motion > * { animation: none; }
}
</style>
