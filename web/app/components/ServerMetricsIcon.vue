<script setup lang="ts">
// Decorative motion only; the existing live status badge remains the source of truth.
defineProps<{ active: boolean }>()
</script>

<template>
  <svg class="metrics-symbol size-5" :class="{ 'metrics-symbol--active': active }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
    <path class="metrics-wave" d="M2 12h4l3-8 5 16 3-8h5" />
    <path class="metrics-signal" pathLength="100" d="M2 12h4l3-8 5 16 3-8h5" />
    <circle class="metrics-beacon" cx="21" cy="12" r="2.5" stroke-width="1" />
  </svg>
</template>

<style scoped>
.metrics-symbol { overflow: visible; flex: none; }
.metrics-wave { opacity: .6; }
.metrics-signal, .metrics-beacon { opacity: 0; }
.metrics-symbol--active .metrics-wave { animation: metrics-heartbeat 4s ease-in-out infinite; transform-origin: 12px 12px; }
.metrics-symbol--active .metrics-signal { stroke-dasharray: 16 84; animation: metrics-packet 4s linear infinite; }
.metrics-symbol--active .metrics-beacon { transform-origin: 21px 12px; animation: metrics-arrival 4s ease-out infinite; }
@keyframes metrics-packet {
  0% { stroke-dashoffset: 16; opacity: 0; }
  8% { opacity: 1; }
  62% { stroke-dashoffset: -100; opacity: 1; }
  70%, 100% { stroke-dashoffset: -100; opacity: 0; }
}
@keyframes metrics-heartbeat {
  0%, 26%, 52%, 100% { transform: scaleY(1); opacity: .6; }
  33% { transform: scaleY(1.12); opacity: 1; }
  40% { transform: scaleY(.95); opacity: .75; }
  46% { transform: scaleY(1.05); opacity: 1; }
}
@keyframes metrics-arrival {
  0%, 55% { transform: scale(.6); opacity: 0; }
  60% { transform: scale(.7); opacity: .7; }
  85%, 100% { transform: scale(1.9); opacity: 0; }
}
@media (prefers-reduced-motion: reduce) {
  .metrics-symbol * { animation: none !important; }
  .metrics-wave { opacity: 1; }
  .metrics-signal, .metrics-beacon { opacity: 0; }
}
</style>
