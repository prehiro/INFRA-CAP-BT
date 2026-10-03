<script setup lang="ts">
import type { NavigationMenuItem } from '@nuxt/ui'

/**
 * App shell, rebuilt on the Nuxt dashboard template (UDashboardGroup + collapsible
 * UDashboardSidebar + command palette).
 *
 * The nav is a fixed list of the app's three pages. It no longer reads the entity tree
 * from the API: the Master Data / Transaksi groups and the Entity Designer were removed,
 * so there is nothing dynamic to enumerate. Note that `route`, the token and the entity
 * fetch were dropped with it — nothing in this layout used them.
 *
 * The sidebar collapse control is NOT here. It lives in each page's navbar (#leading slot
 * of UDashboardNavbar) via the shared PageHeader component, exactly as the template does
 * it. Putting it in the sidebar footer instead makes it overflow the 64px collapsed
 * sidebar and get covered by the content area, so expand stops working.
 */
const links = computed<NavigationMenuItem[][]>(() => [
  [
    { label: 'Dashboard', icon: 'i-lucide-layout-dashboard', to: '/' },
    { label: 'CCTV Log Book', icon: 'i-lucide-video', to: '/logbook/cctv' }
  ],
  [
    { label: 'User Management', icon: 'i-lucide-users', to: '/users' }
  ] as NavigationMenuItem[][]
])

// Command palette (Cmd/Ctrl+K). The runtime entity tree is deliberately NOT included:
// the Master Data / Transaksi groups are gone from the nav, so listing entities here
// would offer destinations that no longer have a way to be reached from the sidebar.
const groups = computed(() => [
  { id: 'nav', label: 'Navigation', items: links.value.flat() }
])
</script>

<template>
  <UDashboardGroup unit="rem">
    <!-- Width: 13rem = 208px. The vendor default is defaultSize 15, so stock renders at
         240px, which is wider than this app needs. min 11rem still fits the longest label
         ("User Management"); max 18rem stops it eating the content area.

         IMPORTANT — why a stale width survives a hard refresh: useResizable defaults to
         `storage: "cookie", persistent: true` and keys that cookie as
         `${storageKey}-sidebar-${id}` -> "dashboard-sidebar-app". The cookie holds
         `{size, collapsed}`, and `size` is read from it INSTEAD of defaultSize whenever
         the cookie exists. So changing :default-size has NO effect on a browser that has
         already been here — that is not an HMR artifact and no amount of refreshing will
         clear it.

         `id` is bumped to "app-v2" so the old cookie is simply never read again. Bump it
         again (v3, v4, ...) whenever the intended default width changes, OR clear the
         `dashboard-sidebar-app` cookie in devtools. The collapsed width is NOT controlled
         here: it comes from the theme's `min-w-16` (64px) on the sidebar root. -->

    <!-- `resizable` is deliberately OFF. It was removed on the belief that it caused the
         "ResizeObserver loop completed with undelivered notifications" errors; that was
         wrong — those only ever appear during HMR updates, never from user interaction
         and never in a production build. So `resizable` can safely come back if
         drag-to-resize is wanted. What is lost without it is only the width drag; the
         sidebar still collapses via the button in each page's navbar. -->
    <UDashboardSidebar
      id="app-v2"
      collapsible
      class="anim-slide-left bg-elevated/25"
      :ui="{ footer: 'lg:border-t lg:border-default' }"
      :default-size="13"
      :min-size="11"
      :max-size="18"
    >
      <template #header="{ collapsed }">
        <BrandMenu :collapsed="collapsed" />
      </template>

      <template #default="{ collapsed }">
        <UDashboardSearchButton :collapsed="collapsed" class="bg-transparent ring-default" />

        <UNavigationMenu
          :collapsed="collapsed"
          :items="links[0]"
          orientation="vertical"
          tooltip
          popover
        />

        <UNavigationMenu
          :collapsed="collapsed"
          :items="links[1]"
          orientation="vertical"
          tooltip
          class="mt-auto"
        />
      </template>

      <template #footer="{ collapsed }">
        <UserMenu :collapsed="collapsed" />
      </template>
    </UDashboardSidebar>

    <UDashboardSearch :groups="groups" />

    <slot />
  </UDashboardGroup>
</template>

<style>
/* UDashboardPanel already sets a main gutter; these two overrides stop the double
   padding that appears once a page renders its own panel. */
main[data-dashboard] {
  padding: 0;
}
</style>