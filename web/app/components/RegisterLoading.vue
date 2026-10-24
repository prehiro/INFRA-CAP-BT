<script setup lang="ts">
withDefaults(defineProps<{ label?: string }>(), { label: 'Loading the register...' })
</script>

<template>
  <span class="register-loading-stack" role="status" aria-live="polite">
    <span class="register-loading-line text-primary" aria-hidden="true">
      <span class="register-loading-segment" />
      <span class="register-loading-tip" />
    </span>
    <span>{{ label }}</span>
  </span>
</template>

<style scoped>
/* Adapted from Uiverse asgardOP/ancient-cow-78; see THIRD-PARTY-NOTICES.md. */
.register-loading-stack { display: inline-grid; gap: .75rem; max-width: 100%; text-align: center; }
.register-loading-line { display: block; position: relative; width: 100%; height: 3px; border-radius: 999px; clip-path: inset(-10px 0); background: color-mix(in oklab, var(--ui-primary) 15%, transparent); }
.register-loading-segment { display: block; width: 100%; height: 100%; border-radius: 999px; background: currentColor; animation: register-line-pass 1s ease infinite; }
.register-loading-tip { position: absolute; inset: 0; pointer-events: none; animation: register-tip-pass 1s ease infinite; }
.register-loading-tip::after { content: ''; position: absolute; right: 100%; top: 0; width: 3px; height: 3px; border-radius: 999px; background: currentColor; box-shadow: 0 0 4px 1px currentColor, 0 0 9px 2px color-mix(in oklab, currentColor 60%, transparent); }
@keyframes register-line-pass {
  from { transform: translateX(-100%); clip-path: inset(0 20% 0 0 round 999px); }
  to { transform: translateX(110%); clip-path: inset(0 70% 0 0 round 999px); }
}
@keyframes register-tip-pass {
  from { transform: translateX(-20%); }
  to { transform: translateX(140%); }
}
@media (prefers-reduced-motion: reduce), print {
  .register-loading-segment { animation: none; transform: none; clip-path: inset(0 60% 0 0 round 999px); }
  .register-loading-tip { display: none; animation: none; }
}
</style>
