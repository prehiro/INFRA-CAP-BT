<script setup lang="ts">
// Exact Iconify ix:pc-tower-settings geometry; decorative motion, not live device status.
const shape = "M384 42.664v192.404q-5.282-.403-10.667-.405c-27.181 0-52.535 7.821-73.935 21.334H130.133v170.667h115.161a138.9 138.9 0 0 0 27.977 42.667H85.333V42.664zm-128 256v.731a137.9 137.9 0 0 0-17.623 41.936h-25.044v-42.667zm85.333-128H128v42.667h213.333zm0-85.333H128v42.666h213.333zm133.285 254.202l-18.963 10.948a85.4 85.4 0 0 1 3.094 22.85a85.4 85.4 0 0 1-3.094 22.849l18.963 10.948a106 106 0 0 1-7.78 17.723l-1.993 3.453a107 107 0 0 1-11.586 15.76l-18.957-10.945c-10.7 10.895-24.301 18.931-39.553 22.857v21.888a107 107 0 0 1-21.333 2.133c-7.306 0-14.44-.734-21.334-2.133v-21.888c-15.252-3.926-28.855-11.963-39.555-22.859l-18.956 10.944a107 107 0 0 1-11.628-15.832l-1.906-3.301a106 106 0 0 1-7.823-17.804l18.961-10.947a85.4 85.4 0 0 1-3.093-22.846a85.4 85.4 0 0 1 3.093-22.846l-18.961-10.948a106 106 0 0 1 7.825-17.807l1.902-3.294a107 107 0 0 1 11.63-15.835l18.956 10.944c10.7-10.897 24.303-18.933 39.555-22.859v-21.888a107 107 0 0 1 21.334-2.134c7.306 0 14.44.735 21.333 2.134v21.888c15.252 3.925 28.853 11.961 39.553 22.856l18.957-10.944a107 107 0 0 1 11.587 15.761l1.991 3.449a106 106 0 0 1 7.781 17.725m-69.201 33.798c0 17.673-14.327 32-32 32s-32-14.327-32-32s14.327-32 32-32s32 14.327 32 32"
// Split at the gear subpath; resolve its relative move against the preceding closed path.
const [tower, gearTail] = shape.split('m133.285 254.202')
const gear = 'M474.618 339.533' + gearTail
const id = `factory-settings-${useId()}`
</script>
<template>
  <svg class="factory-pc-glyph" viewBox="0 0 512 512" fill="currentColor" aria-hidden="true" focusable="false">
    <defs>
      <clipPath :id="`${id}-clip`">
        <path :d="tower" clip-rule="evenodd" />
        <path class="factory-gear-spin" :d="gear" clip-rule="evenodd" />
      </clipPath>
      <linearGradient :id="`${id}-shine`" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stop-color="currentColor" stop-opacity="0" />
        <stop offset=".8" stop-color="currentColor" stop-opacity=".8" />
        <stop offset="1" stop-color="currentColor" stop-opacity="0" />
      </linearGradient>
    </defs>
    <path class="factory-base factory-tower" :d="tower" fill-rule="evenodd" />
    <path class="factory-base factory-gear-spin" :d="gear" fill-rule="evenodd" />
    <g class="factory-motion" :clip-path="`url(#${id}-clip)`">
      <rect class="factory-sweep" x="60" y="-120" width="440" height="120" :fill="`url(#${id}-shine)`" />
      <circle class="factory-gear-wave" cx="373.4" cy="373.3" r="70" fill="none" stroke="currentColor" stroke-width="30" />
    </g>
  </svg>
</template>
<style scoped>
.factory-pc-glyph { display: block; }
.factory-base { opacity: .72; }
.factory-gear-spin { transform-box: view-box; transform-origin: 373.417px 373.331px; animation: factory-spin 12s linear infinite; }
@keyframes factory-spin { to { transform: rotate(360deg); } }
.factory-motion { pointer-events: none; }
.factory-sweep { animation: factory-sweep 6.4s cubic-bezier(.4,0,.2,1) infinite; opacity: 0; }
.factory-gear-wave { transform-origin: 373.4px 373.3px; animation: factory-gear-wave 6.4s ease-out infinite; opacity: 0; }
@keyframes factory-sweep {
  0% { transform: translateY(0); opacity: 0; }
  8% { opacity: 1; }
  44% { transform: translateY(640px); opacity: 1; }
  48%, 100% { transform: translateY(640px); opacity: 0; }
}
@keyframes factory-gear-wave {
  0%, 36% { transform: scale(.45); opacity: 0; }
  42% { opacity: .9; }
  64%, 100% { transform: scale(1.6); opacity: 0; }
}
@media (prefers-reduced-motion: reduce), print {
  .factory-base { opacity: 1; }
  .factory-gear-spin { animation: none; transform: none; }
  .factory-motion { display: none; }
  .factory-motion > * { animation: none; }
}
</style>

