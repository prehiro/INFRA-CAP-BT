<template>
  <div class="relative min-h-screen bg-default">
    <div
      class="pointer-events-none absolute inset-x-0 top-0 h-64 opacity-60"
      style="background: radial-gradient(60% 100% at 50% 0%, var(--ui-color-primary-500), transparent 70%)"
    />
    <div class="relative">
      <slot />
    </div>
  </div>
</template>
