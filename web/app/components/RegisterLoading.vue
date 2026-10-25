<script setup lang="ts">
withDefaults(defineProps<{ label?: string }>(), { label: 'Loading the register...' })
</script>

<template>
  <span class="register-loading-stack" role="status" aria-live="polite">
    <span class="register-loading-line text-primary" aria-hidden="true">
      <span class="register-loading-body"><span class="register-loading-segment" /></span>
      <span class="register-loading-tip" />
    </span>
    <span>{{ label }}</span>
  </span>
</template>

<style scoped>
/* Adapted from Uiverse asgardOP/ancient-cow-78; see THIRD-PARTY-NOTICES.md. */
@property --infra-register-tip-phase {
  syntax: '<number>';
  inherits: true;
  initial-value: 0;
}
.register-loading-stack { display: inline-grid; gap: .75rem; max-width: 100%; text-align: center; }
.register-loading-line { display: block; position: relative; width: 100%; height: 3px; border-radius: 999px; overflow: visible; background: color-mix(in oklab, var(--ui-primary) 15%, transparent); }
/* Only the bar is clipped to a capsule; the tip halo paints freely beyond both track ends. */
.register-loading-body { display: block; width: 100%; height: 100%; overflow: hidden; border-radius: inherit; }
.register-loading-segment { display: block; width: 100%; height: 100%; border-radius: 999px; background: currentColor; animation: register-line-pass 1s ease infinite; }
.register-loading-tip { position: absolute; inset: 0; pointer-events: none; animation: register-tip-pass 1s ease infinite; }
/* Bound the emitter, not its shadow. Fade only while the running bar is off-track. */
.register-loading-tip::after { content: ''; position: absolute; left: clamp(0px, calc(var(--infra-register-tip-phase) * 160% - 20% - 3px), calc(100% - 3px)); top: 0; width: 3px; height: 3px; border-radius: 999px; opacity: min(clamp(0, calc(var(--infra-register-tip-phase) * 40 - 5), 1), clamp(0, calc((.95238 - var(--infra-register-tip-phase)) * 80), 1)); background: color-mix(in oklab, currentColor 80%, white); box-shadow: 0 0 5px 2px currentColor, 0 0 12px 4px currentColor, 0 0 22px 5px color-mix(in oklab, currentColor 75%, transparent); }
@keyframes register-line-pass {
  from { transform: translateX(-100%); clip-path: inset(0 20% 0 0 round 999px); }
  to { transform: translateX(110%); clip-path: inset(0 70% 0 0 round 999px); }
}
@keyframes register-tip-pass {
  from { --infra-register-tip-phase: 0; }
  to { --infra-register-tip-phase: 1; }
}
@media (prefers-reduced-motion: reduce), print {
  .register-loading-segment { animation: none; transform: none; clip-path: inset(0 60% 0 0 round 999px); }
  .register-loading-tip { display: none; animation: none; }
}
</style>
