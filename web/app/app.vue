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
 *
 * Theme restoration deliberately does NOT live here. It was tried in this file's onMounted and
 * in UserMenu's, and in both the stored colour was silently ignored - measured, with the
 * cookie set to violet (a colour that is NOT the old default and so cannot be explained by the
 * migration) and `--ui-primary` still reading green after a reload. `useCookie` resolves its
 * ref against the current Nuxt instance's payload, and a call from a component's onMounted runs
 * with that component's effect scope rather than the app's, so the cookie ref it gets back is
 * not the one the rest of the app reads. A plugin has no such problem; see
 * app/plugins/theme.client.ts.
 */
</script>

<template>
  <UApp>
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </UApp>
</template>