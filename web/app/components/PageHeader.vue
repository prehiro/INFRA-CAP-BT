<script setup lang="ts">
/**
 * Shared page header, following the Nuxt dashboard template.
 *
 * The template puts the sidebar collapse control in the navbar's #leading slot, on every
 * page — NOT in the sidebar footer. This component exists so all three pages get it
 * identically; forgetting it on one page would strand a collapsed sidebar with no way
 * to expand it again.
 *
 * Layout notes that cost real debugging time:
 *  - the vendor maps the DEFAULT slot to the "center" slot (centred). Title text must
 *    therefore go in #trailing, which sits inside the navbar's left cluster right after
 *    #leading. Putting the title in the default slot silently centres it.
 *  - stock UDashboardSidebarCollapse is used as-is. The dashboard context that drives the
 *    toggle is injected by UDashboardGroup, and a hand-rolled component does not reliably
 *    receive it (useDashboard() is an internal util, neither an auto-import nor public
 *    API). Do not reimplement it.
 *
 * Title only, no subtitle. A subtitle was tried in both stacked and inline form and both
 * were dropped: the navbar has a fixed h-(--ui-header-height) of 4rem = 64px, so a second
 * line left almost no breathing room, and inline read as noise next to the title.
 */
defineProps<{
  title: string
}>()
</script>

<template>
  <UDashboardNavbar>
    <template #leading>
      <UDashboardSidebarCollapse />
    </template>

    <template #trailing>
      <h1 class="truncate text-xl font-bold">{{ title }}</h1>
    </template>

    <template v-if="$slots.actions" #right>
      <slot name="actions" />
    </template>
  </UDashboardNavbar>
</template>
