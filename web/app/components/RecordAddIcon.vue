<script setup lang="ts">
withDefaults(defineProps<{ name?: string; size?: 'sm' | 'md' }>(), { name: 'i-carbon-data-add', size: 'sm' })
</script>

<template>
  <!-- Explicit opt-in for header icons; preserve the existing plate and crisp glyph size. -->
  <span class="pc-add-mark relative inline-grid shrink-0 place-items-center" :class="size === 'md' ? 'size-6' : 'size-5'" aria-hidden="true">
    <span class="pc-add-halo pointer-events-none absolute -inset-1 rounded-md" />
    <slot>
      <UIcon :name="name" class="pc-add-glyph size-5" />
    </slot>
  </span>
</template>

<style scoped>
.pc-add-glyph {
  animation: pc-add-sway 3.6s ease-in-out infinite;
  transform-origin: 50% 60%;
}

.pc-add-halo {
  border: 1px solid color-mix(in oklab, var(--ui-primary) 45%, transparent);
  animation: pc-add-halo 3.6s ease-in-out infinite;
}

@keyframes pc-add-sway {
  0%, 100% { transform: translateY(0) rotate(-4deg); }
  50% { transform: translateY(-1.5px) rotate(5deg); }
}

@keyframes pc-add-halo {
  0%, 100% { transform: scale(1); opacity: 0.4; }
  50% { transform: scale(1.25); opacity: 0.08; }
}

@media (prefers-reduced-motion: reduce) {
  .pc-add-glyph,
  .pc-add-halo { animation: none; }
  .pc-add-halo { opacity: 0.18; }
}
</style>
