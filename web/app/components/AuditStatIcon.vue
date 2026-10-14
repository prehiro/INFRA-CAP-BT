<script setup lang="ts">
defineProps<{ kind: string }>()
</script>

<template>
  <!-- Local vector geometry: no remote icon request, no timers, no layout animation. -->
  <svg class="audit-symbol size-6" :class="`audit-symbol--${kind}`" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
    <template v-if="kind === 'total'">
      <path class="activity-base" d="M2 12h4l3-8 5 16 3-8h5" />
      <path class="activity-trace" pathLength="100" d="M2 12h4l3-8 5 16 3-8h5" />
    </template>
    <template v-else-if="kind === 'logins'">
      <path class="entry-door" d="M14 3h5a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-5" />
      <g class="entry-arrow"><path d="M3 12h12m-5-5 5 5-5 5" /></g>
    </template>
    <template v-else-if="kind === 'deletes'">
      <path d="m5 7 1 13h12l1-13M10 10v7m4-7v7" />
      <g class="bin-lid"><path d="M3 6h18M9 6V3h6v3" /></g>
      <path class="bin-paper" d="M11 5h2v3h-2z" stroke-width="1.3" />
    </template>
    <template v-else>
      <path class="shield-outline" d="M12 2 3 6v6c0 5 9 10 9 10s9-5 9-10V6z" />
      <g class="shield-alert"><path d="M12 7v6" /><circle cx="12" cy="17" r=".85" fill="currentColor" stroke="none" /></g>
      <path class="shield-echo" d="M12 2 3 6v6c0 5 9 10 9 10s9-5 9-10V6z" />
    </template>
  </svg>
</template>

<style scoped>
.audit-symbol { overflow: visible; flex: none; }
.activity-base { opacity: .25; }
.activity-trace { stroke-dasharray: 100; animation: audit-trace 4.8s ease-in-out infinite; }
.entry-arrow { animation: audit-entry 4.8s cubic-bezier(.4,0,.2,1) -.8s infinite; }
.entry-door { animation: audit-door 4.8s ease-in-out -.8s infinite; }
.bin-lid { animation: audit-lid 4.8s ease-in-out -1.6s infinite; }
.bin-paper { opacity: 0; animation: audit-paper 4.8s ease-in-out -1.6s infinite; }
.shield-alert { transform-origin: 12px 12px; animation: audit-alert 4.8s ease-in-out -2.4s infinite; }
.shield-echo { transform-origin: 12px 12px; opacity: 0; animation: audit-echo 4.8s ease-out -2.4s infinite; }
@keyframes audit-trace {
  0% { stroke-dashoffset: 100; opacity: .25; }
  38%, 76% { stroke-dashoffset: 0; opacity: 1; }
  94%, 100% { stroke-dashoffset: 0; opacity: 0; }
}
@keyframes audit-entry {
  0%, 12%, 82%, 100% { transform: translateX(0); opacity: 1; }
  34% { transform: translateX(4px); opacity: 1; }
  44% { transform: translateX(6px); opacity: 0; }
  45% { transform: translateX(-4px); opacity: 0; }
  65% { transform: translateX(0); opacity: 1; }
}
@keyframes audit-door { 0%, 15%, 75%, 100% { opacity: .65; } 35%, 55% { opacity: 1; } }
@keyframes audit-lid {
  0%, 12%, 68%, 100% { transform: translateY(0); }
  28%, 48% { transform: translateY(-3px); }
  58% { transform: translateY(.5px); }
}
@keyframes audit-paper {
  0%, 20% { transform: translateY(-5px); opacity: 0; }
  30% { transform: translateY(-3px); opacity: 1; }
  48% { transform: translateY(7px); opacity: 0; }
  100% { transform: translateY(7px); opacity: 0; }
}
@keyframes audit-alert {
  0%, 18%, 52%, 100% { transform: scale(1); opacity: 1; }
  28% { transform: scale(1.18); opacity: .6; }
  38% { transform: scale(1); opacity: 1; }
  44% { transform: scale(1.1); opacity: .75; }
}
@keyframes audit-echo {
  0%, 23% { transform: scale(1); opacity: 0; }
  25% { transform: scale(1); opacity: .45; }
  55%, 100% { transform: scale(1.28); opacity: 0; }
}
@media (prefers-reduced-motion: reduce) {
  .audit-symbol * { animation: none !important; }
  .activity-trace { stroke-dashoffset: 0; opacity: 1; }
  .bin-paper, .shield-echo { opacity: 0; }
}
</style>
