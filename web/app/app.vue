<script setup lang="ts">
/**
 * Root wrapper. Nuxt UI's UApp is what provides ConfigProvider and, critically,
 * TooltipProvider.
 *
 * This file was missing while the sidebar used <UNavigationMenu tooltip>. When the
 * sidebar is collapsed that prop renders a UTooltip, which injects
 * TooltipProviderContext; with no UApp ancestor the injection failed at runtime with
 * "Injection Symbol(TooltipProviderContext) not found". Only visible once the sidebar
 * collapsed, which is why it looked intermittent.
 *
 * The template this dashboard was based on has an equivalent app.vue.
 */
</script>

<template>
  <UApp>
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </UApp>
</template>