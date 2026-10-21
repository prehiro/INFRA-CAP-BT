<script setup lang="ts">
// Ambient AI circuit artwork adapted from the Uiverse "giant-newt-48" element by dexter-st
// (Ask AI Button, MIT License, https://uiverse.io/dexter-st/giant-newt-48).
// Only the radial circuit lines, the travelling dash and the pulsing dots are reused; the
// button, its hover/focus states and the text swap are NOT part of this component.
// Original viewBox "70 70 160 160" is kept so the geometry matches the source exactly.
</script>

<template>
  <svg class="ai-circuit" viewBox="70 70 160 160" xmlns="http://www.w3.org/2000/svg"
       aria-hidden="true" focusable="false">
    <g class="ai-static">
      <line class="ai-line-bg" x1="150" y1="143.58" x2="150" y2="97.31" />
      <line class="ai-line-bg" x1="157.98" y1="143.58" x2="157.98" y2="112.08" />
      <line class="ai-line-bg" x1="142.02" y1="143.58" x2="142.02" y2="118.08" />
      <line class="ai-line-bg" x1="130.05" y1="143.58" x2="130.05" y2="133.79" />
      <line class="ai-line-bg" x1="138.03" y1="143.58" x2="138.03" y2="131.83" />
      <line class="ai-line-bg" x1="146.01" y1="143.58" x2="146.01" y2="129.15" />
      <line class="ai-line-bg" x1="153.99" y1="143.58" x2="153.99" y2="129.15" />
      <line class="ai-line-bg" x1="161.97" y1="143.58" x2="161.97" y2="131.83" />
      <line class="ai-line-bg" x1="169.95" y1="143.58" x2="169.95" y2="133.79" />
      <line class="ai-line-bg" x1="176.04" y1="150" x2="217.32" y2="150" />
      <line class="ai-line-bg" x1="123.96" y1="150" x2="82.68" y2="150" />
      <line class="ai-line-bg" x1="150" y1="156.42" x2="150" y2="202.69" />
      <line class="ai-line-bg" x1="142.02" y1="156.42" x2="142.02" y2="187.92" />
      <line class="ai-line-bg" x1="157.98" y1="156.42" x2="157.98" y2="181.92" />
      <line class="ai-line-bg" x1="169.95" y1="156.42" x2="169.95" y2="166.21" />
      <line class="ai-line-bg" x1="161.97" y1="156.42" x2="161.97" y2="168.17" />
      <line class="ai-line-bg" x1="153.99" y1="156.42" x2="153.99" y2="170.85" />
      <line class="ai-line-bg" x1="146.01" y1="156.42" x2="146.01" y2="170.85" />
      <line class="ai-line-bg" x1="138.03" y1="156.42" x2="138.03" y2="168.17" />
      <line class="ai-line-bg" x1="130.05" y1="156.42" x2="130.05" y2="166.21" />
      <path class="ai-line-bg" d="M126.06,143.58v-10.37c0-.72-.29-1.41-.8-1.92l-4.28-4.28c-.51-.51-.8-1.2-.8-1.92v-12.37" />
      <path class="ai-line-bg" d="M165.96,143.58v-18.37c0-.72.29-1.41.8-1.92l4.28-4.28c.51-.51.8-1.2.8-1.92v-14.37" />
      <path class="ai-line-bg" d="M173.94,143.58v-10.37c0-.72.29-1.41.8-1.92l4.28-4.28c.51-.51.8-1.2.8-1.92v-10.69" />
      <path class="ai-line-bg" d="M134.04,143.58v-20.37c0-.72-.29-1.41-.8-1.92l-4.28-4.28c-.51-.51-.8-1.2-.8-1.92v-9.37" />
      <path class="ai-line-bg" d="M176.04,153.99h13.37c.72,0,1.41.29,1.92.8l4.28,4.28c.51.51,1.2.8,1.92.8h14.37" />
      <path class="ai-line-bg" d="M176.04,146.01h15.37c.72,0,1.41-.29,1.92-.8l4.28-4.28c.51-.51,1.2-.8,1.92-.8h9.37" />
      <path class="ai-line-bg" d="M123.96,146.01h-13.37c-.72,0-1.41-.29-1.92-.8l-4.28-4.28c-.51-.51-1.2-.8-1.92-.8h-14.37" />
      <path class="ai-line-bg" d="M123.96,153.99h-15.37c-.72,0-1.41.29-1.92.8l-4.28,4.28c-.51.51-1.2.8-1.92.8h-9.37" />
      <path class="ai-line-bg" d="M173.94,156.42v10.37c0,.72.29,1.41.8,1.92l4.28,4.28c.51.51.8,1.2.8,1.92v12.37" />
      <path class="ai-line-bg" d="M134.04,156.42v18.37c0,.72-.29,1.41-.8,1.92l-4.28,4.28c-.51.51-.8,1.2-.8,1.92v14.37" />
      <path class="ai-line-bg" d="M126.06,156.42v10.37c0,.72-.29,1.41-.8,1.92l-4.28,4.28c-.51.51-.8,1.2-.8,1.92v10.69" />
      <path class="ai-line-bg" d="M165.96,156.42v20.37c0,.72.29,1.41.8,1.92l4.28,4.28c.51.51.8,1.2.8,1.92v9.37" />
    </g>
    <g class="ai-motion">
      <line class="ai-line" x1="150" y1="143.58" x2="150" y2="97.31" />
      <line class="ai-line" x1="157.98" y1="143.58" x2="157.98" y2="112.08" />
      <line class="ai-line" x1="142.02" y1="143.58" x2="142.02" y2="118.08" />
      <line class="ai-line" x1="130.05" y1="143.58" x2="130.05" y2="133.79" />
      <line class="ai-line" x1="138.03" y1="143.58" x2="138.03" y2="131.83" />
      <line class="ai-line" x1="146.01" y1="143.58" x2="146.01" y2="129.15" />
      <line class="ai-line" x1="153.99" y1="143.58" x2="153.99" y2="129.15" />
      <line class="ai-line" x1="161.97" y1="143.58" x2="161.97" y2="131.83" />
      <line class="ai-line" x1="169.95" y1="143.58" x2="169.95" y2="133.79" />
      <line class="ai-line" x1="176.04" y1="150" x2="217.32" y2="150" />
      <line class="ai-line" x1="123.96" y1="150" x2="82.68" y2="150" />
      <line class="ai-line" x1="150" y1="156.42" x2="150" y2="202.69" />
      <line class="ai-line" x1="142.02" y1="156.42" x2="142.02" y2="187.92" />
      <line class="ai-line" x1="157.98" y1="156.42" x2="157.98" y2="181.92" />
      <line class="ai-line" x1="169.95" y1="156.42" x2="169.95" y2="166.21" />
      <line class="ai-line" x1="161.97" y1="156.42" x2="161.97" y2="168.17" />
      <line class="ai-line" x1="153.99" y1="156.42" x2="153.99" y2="170.85" />
      <line class="ai-line" x1="146.01" y1="156.42" x2="146.01" y2="170.85" />
      <line class="ai-line" x1="138.03" y1="156.42" x2="138.03" y2="168.17" />
      <line class="ai-line" x1="130.05" y1="156.42" x2="130.05" y2="166.21" />
      <path class="ai-line" d="M126.06,143.58v-10.37c0-.72-.29-1.41-.8-1.92l-4.28-4.28c-.51-.51-.8-1.2-.8-1.92v-12.37" />
      <path class="ai-line" d="M165.96,143.58v-18.37c0-.72.29-1.41.8-1.92l4.28-4.28c.51-.51.8-1.2.8-1.92v-14.37" />
      <path class="ai-line" d="M173.94,143.58v-10.37c0-.72.29-1.41.8-1.92l4.28-4.28c.51-.51.8-1.2.8-1.92v-10.69" />
      <path class="ai-line" d="M134.04,143.58v-20.37c0-.72-.29-1.41-.8-1.92l-4.28-4.28c-.51-.51-.8-1.2-.8-1.92v-9.37" />
      <path class="ai-line" d="M176.04,153.99h13.37c.72,0,1.41.29,1.92.8l4.28,4.28c.51.51,1.2.8,1.92.8h14.37" />
      <path class="ai-line" d="M176.04,146.01h15.37c.72,0,1.41-.29,1.92-.8l4.28-4.28c.51-.51,1.2-.8,1.92-.8h9.37" />
      <path class="ai-line" d="M123.96,146.01h-13.37c-.72,0-1.41-.29-1.92-.8l-4.28-4.28c-.51-.51-1.2-.8-1.92-.8h-14.37" />
      <path class="ai-line" d="M123.96,153.99h-15.37c-.72,0-1.41.29-1.92.8l-4.28,4.28c-.51.51-1.2.8-1.92.8h-9.37" />
      <path class="ai-line" d="M173.94,156.42v10.37c0,.72.29,1.41.8,1.92l4.28,4.28c.51.51.8,1.2.8,1.92v12.37" />
      <path class="ai-line" d="M134.04,156.42v18.37c0,.72-.29,1.41-.8,1.92l-4.28,4.28c-.51.51-.8,1.2-.8,1.92v14.37" />
      <path class="ai-line" d="M126.06,156.42v10.37c0,.72-.29,1.41-.8,1.92l-4.28,4.28c-.51.51-.8,1.2-.8,1.92v10.69" />
      <path class="ai-line" d="M165.96,156.42v20.37c0,.72.29,1.41.8,1.92l4.28,4.28c.51.51.8,1.2.8,1.92v9.37" />
    </g>
    <g class="ai-dots">
      <circle class="ai-dot" cx="150" cy="96.13" r="1.17" />
      <circle class="ai-dot" cx="157.98" cy="110.91" r="1.17" />
      <circle class="ai-dot" cx="142.02" cy="116.91" r="1.17" />
      <circle class="ai-dot" cx="157.98" cy="183.09" r="1.17" />
      <circle class="ai-dot" cx="171.83" cy="101.54" r="1.17" />
      <circle class="ai-dot" cx="179.81" cy="113.23" r="1.17" />
      <circle class="ai-dot" cx="128.17" cy="104.54" r="1.17" />
      <circle class="ai-dot" cx="120.19" cy="111.54" r="1.17" />
      <circle class="ai-dot" cx="150" cy="203.87" r="1.17" />
      <circle class="ai-dot" cx="142.02" cy="189.09" r="1.17" />
      <circle class="ai-dot" cx="128.17" cy="198.46" r="1.17" />
      <circle class="ai-dot" cx="120.19" cy="186.77" r="1.17" />
      <circle class="ai-dot" cx="171.83" cy="195.46" r="1.17" />
      <circle class="ai-dot" cx="179.81" cy="188.46" r="1.17" />
      <circle class="ai-dot" cx="210.08" cy="140.14" r="1.17" />
      <circle class="ai-dot" cx="218.49" cy="150" r="1.17" />
      <circle class="ai-dot" cx="213.08" cy="159.86" r="1.17" />
      <circle class="ai-dot" cx="89.92" cy="159.86" r="1.17" />
      <circle class="ai-dot" cx="81.51" cy="150" r="1.17" />
      <circle class="ai-dot" cx="86.92" cy="140.14" r="1.17" />
    </g>
  </svg>
</template>

<style scoped>
.ai-circuit {
  width: 100%;
  height: 100%;
  overflow: visible;
  /* The source artwork radiates from the middle, so its lines run straight through the
     centre - exactly where the badge and its icon sit. This mask keeps the circuit out of
     that area: fully hidden inside the badge, then fading in, so the lines read as a halo
     BEHIND the icon instead of crossing it. The stop offsets are in the wrapper's own
     pixels (the badge is 44px wide inside a 132px wrapper), so the cut-out tracks the
     badge exactly instead of the SVG's internal viewBox units. */
  /* The cut-out is an absolute radius, not a percentage: a percentage in radial-gradient
     is measured against the distance to the farthest CORNER, so it does not track the
     badge. 26.5px is the badge's half-diagonal including its 12px corner radius, so the
     hole clears the badge completely even in the corners. */
  --ai-cut: 26.5px;
  -webkit-mask-image: radial-gradient(circle var(--ai-cut) at 50% 50%,
      transparent 0 var(--ai-cut), #000 calc(var(--ai-cut) + 7px));
  mask-image: radial-gradient(circle var(--ai-cut) at 50% 50%,
      transparent 0 var(--ai-cut), #000 calc(var(--ai-cut) + 7px));
}
/* The source renders at 300px for a 160-unit viewBox, so its 0.5 stroke lands near 1px.
   The halo is drawn much smaller here, so the strokes are scaled up to stay visible. */
.ai-line-bg {
  fill: none;
  /* The accent is a semantic token, so the circuit follows whichever colour the user picked
     instead of staying green. */
  stroke: color-mix(in oklab, var(--ui-primary) 22%, transparent);
  stroke-width: 1.1;
  stroke-linecap: round;
  stroke-linejoin: round;
}
.ai-line {
  fill: none;
  stroke: var(--ui-primary);
  stroke-width: 0;
  stroke-dasharray: 16 56;
  stroke-linecap: round;
  stroke-linejoin: round;
  filter: drop-shadow(0 0 2px color-mix(in oklab, var(--ui-primary) 85%, transparent));
  animation: ai-dash 3s ease-out infinite;
}
.ai-line:nth-child(odd) {
  animation-direction: normal;
  animation-delay: .3s;
}
.ai-dot {
  fill: var(--ui-primary);
  opacity: .25;
  animation: ai-dot-opac 1.5s linear infinite;
}
.ai-dot:nth-child(odd) { animation-delay: .15s; }
.ai-dot:nth-child(3n) { animation-delay: .3s; }

@keyframes ai-dash {
  0% { stroke-width: 0; stroke-dashoffset: 10; stroke: var(--ui-primary); }
  50% { stroke-width: 2.4; stroke-dashoffset: 88; stroke: var(--ui-color-primary-300); }
  100% { stroke-width: 0; stroke-dashoffset: 166; stroke: var(--ui-primary); }
}
@keyframes ai-dot-opac {
  0%, 100% { opacity: .08; }
  20% { opacity: .8; }
}
@media (prefers-reduced-motion: reduce) {
  .ai-line, .ai-dot { animation: none; }
  .ai-line { stroke-width: 1.4; stroke-dasharray: none; }
  .ai-dot { opacity: .35; }
}
</style>
