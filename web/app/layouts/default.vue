<script setup lang="ts">
import type { NavigationMenuItem } from '@nuxt/ui'

/**
 * App shell, rebuilt on the Nuxt dashboard template (UDashboardGroup + collapsible
 * UDashboardSidebar + command palette).
 *
 * The nav is a fixed list; it no longer reads the entity tree from the API (the Master
 * Data / Transaksi groups and the Entity Designer were removed, so there is nothing
 * dynamic to enumerate). Note that `route`, the token and the entity fetch were dropped
 * with it — nothing in this layout used them.
 *
 * The "Log Book" entry is a `type: 'trigger'` group with `defaultOpen: true`, so it
 * renders as a collapsible section that starts open, holding CCTV Access and Handover.
 *
 * The sidebar collapse control is NOT here. It lives in each page's navbar (#leading slot
 * of UDashboardNavbar) via the shared PageHeader component, exactly as the template does
 * it. Putting it in the sidebar footer instead makes it overflow the 64px collapsed
 * sidebar and get covered by the content area, so expand stops working.
 */
const links = computed<NavigationMenuItem[][]>(() => [
  [
    { label: 'Dashboard', icon: 'i-lucide-layout-dashboard', to: '/' }
  ],
  [
    {
      label: 'Log Book',
      icon: 'i-lucide-book-open',
      type: 'trigger',
      defaultOpen: true,
      children: [
        { label: 'CCTV Access', icon: 'i-lucide-video', to: '/logbook/cctvacc' },
        { label: 'Handover', icon: 'i-lucide-clipboard-list', to: '/logbook/handover' }
      ]
    }
  ],
  [
    { label: 'User Management', icon: 'i-lucide-users', to: '/users' }
  ] as NavigationMenuItem[][]
])

// Command palette (Cmd/Ctrl+K). Listed explicitly rather than derived from links.value
// because links.value.flat() would include the Log Book trigger item itself, which is not
// a destination. Keeping it explicit also guarantees the palette and the sidebar never
// disagree about where things live.
const groups = computed(() => [
  {
    id: 'nav',
    label: 'Navigation',
    items: [
      { label: 'Dashboard', icon: 'i-lucide-layout-dashboard', to: '/' },
      { label: 'CCTV Access', icon: 'i-lucide-video', to: '/logbook/cctvacc' },
      { label: 'Handover', icon: 'i-lucide-clipboard-list', to: '/logbook/handover' },
      { label: 'User Management', icon: 'i-lucide-users', to: '/users' }
    ]
  }
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
      class="bg-elevated/25 sidebar-spring"
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

        <!-- Rendered with v-for rather than links[0] / links[1] so adding a group to the
             nav list cannot be silently forgotten here.

             Long labels: the vendor already sets `truncate` on linkLabel/childLinkLabel,
             so an over-long label ellipsises instead of wrapping and breaking the sidebar
             rhythm. The `item-label` slot adds the native `title` attribute so hovering a
             truncated label still reveals the full text - the vendor has no per-item title
             support of its own. `min-w-0` on the link is what lets the truncation actually
             kick in inside the flex row; without it the text would push the sidebar wider
             than its configured size instead of shrinking. -->
        <UNavigationMenu
          v-for="(group, i) in links"
          :key="`nav-${i}`"
          :collapsed="collapsed"
          :items="group"
          orientation="vertical"
          tooltip
          :class="i === links.length - 1 ? 'mt-auto' : ''"
          :ui="{ link: 'min-w-0' }"
        >
          <template #item-label="{ item }">
            <span :title="item.label" class="truncate">{{ item.label }}</span>
          </template>
        </UNavigationMenu>
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

/* Sidebar bounce.
   The width itself is animated by the vendor switching --width, so the transform here
   only adds the spring. scaleX slightly squashes the panel on collapse and overshoots on
   open, which reads as a bounce rather than a plain slide. transform-origin is the LEFT
   edge because the sidebar is anchored left and grows rightwards.
   The width transition is declared here too: without it the vendor's instant width swap
   would make the spring look like a glitch instead of a motion. */
.sidebar-spring {
  transform-origin: left center;
  transition: width 320ms cubic-bezier(0.22, 1, 0.36, 1);
}

@keyframes infra-sidebar-close {
  0%   { transform: translateX(0) scaleX(1); }
  30%  { transform: translateX(4px) scaleX(0.94); }
  65%  { transform: translateX(-2px) scaleX(1.02); }
  100% { transform: translateX(0) scaleX(1); }
}

@keyframes infra-sidebar-open {
  0%   { transform: translateX(0) scaleX(1); }
  30%  { transform: translateX(-6px) scaleX(1.04); }
  65%  { transform: translateX(2px) scaleX(0.98); }
  100% { transform: translateX(0) scaleX(1); }
}

/* Driven by the vendor's own data-collapsed attribute rather than by a JS-bound class.
   An earlier version tracked the state in a composable and toggled a class; that silently
   stopped updating (the MutationObserver ended up watching a detached node after a
   re-mount), leaving the spring stuck on one side. Keying the animation off the attribute
   the vendor already maintains removes the state tracking entirely — and because the
   matched selector changes on every flip, the animation restarts by itself. */
#dashboard-sidebar-app-v2[data-collapsed='false'] { animation: infra-sidebar-close 360ms cubic-bezier(0.34, 1.4, 0.64, 1); }
#dashboard-sidebar-app-v2[data-collapsed='true']  { animation: infra-sidebar-open  360ms cubic-bezier(0.34, 1.4, 0.64, 1); }

@media (prefers-reduced-motion: reduce) {
  .sidebar-spring { transition: none !important; }
  #dashboard-sidebar-app-v2[data-collapsed] { animation: none !important; }
}
</style>