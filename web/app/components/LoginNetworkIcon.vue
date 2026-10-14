<script setup lang="ts">
withDefaults(defineProps<{ size?: number }>(), { size: 32 })
</script>

<template>
  <!-- Local decorative server/network mark, independent of actual host status. -->
  <svg class="login-infra" :width="size" :height="size" viewBox="0 0 32 32" fill="none" stroke="currentColor" stroke-width="1.45" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
    <rect x="6" y="3" width="20" height="8" rx="2" class="rack" />
    <rect x="6" y="13" width="20" height="8" rx="2" class="rack" />
    <path d="M10 7h6m-6 10h6" opacity=".65" />
    <circle class="port port-a" cx="22" cy="7" r="1.25" fill="currentColor" stroke="none" />
    <circle class="port port-b" cx="22" cy="17" r="1.25" fill="currentColor" stroke="none" />
    <path class="network-bus" d="M16 21v4H5v3m11-3v3m0-3h11v3" />
    <path class="packet packet-left" pathLength="100" d="M16 21v4H5v3" />
    <path class="packet packet-right" pathLength="100" d="M16 21v4h11v3" />
    <path class="packet packet-center" pathLength="100" d="M16 21v7" />
    <g fill="currentColor" stroke="none">
      <rect class="endpoint endpoint-a" x="3" y="28" width="4" height="3" rx=".8" />
      <rect class="endpoint endpoint-b" x="14" y="28" width="4" height="3" rx=".8" />
      <rect class="endpoint endpoint-c" x="25" y="28" width="4" height="3" rx=".8" />
    </g>
  </svg>
</template>

<style scoped>
.login-infra { overflow: visible; }
.rack { fill: currentColor; fill-opacity: .07; }
.network-bus { opacity: .3; }
.port { animation: infra-port 4.8s ease-in-out infinite; }
.port-b { animation-delay: .65s; }
.packet { stroke-dasharray: 22 100; opacity: 0; animation: infra-packet 4.8s ease-in-out infinite; }
.packet-right { animation-delay: 1.6s; }
.packet-center { animation-delay: 3.2s; }
.endpoint { opacity: .5; animation: infra-endpoint 4.8s ease-in-out infinite; }
.endpoint-c { animation-delay: 1.6s; }
.endpoint-b { animation-delay: 3.2s; }
@keyframes infra-port {
  0%, 38%, 100% { opacity: .35; }
  8%, 20%, 28% { opacity: 1; }
  24% { opacity: .5; }
}
@keyframes infra-packet {
  0% { stroke-dashoffset: 22; opacity: 0; }
  4% { opacity: 1; }
  26% { stroke-dashoffset: -100; opacity: 1; }
  32%, 100% { stroke-dashoffset: -100; opacity: 0; }
}
@keyframes infra-endpoint {
  0%, 15%, 48%, 100% { opacity: .4; }
  27%, 35% { opacity: 1; }
}
@media (prefers-reduced-motion: reduce) {
  .login-infra * { animation: none !important; }
  .packet { opacity: 0; }
  .port, .endpoint { opacity: 1; }
}
</style>
