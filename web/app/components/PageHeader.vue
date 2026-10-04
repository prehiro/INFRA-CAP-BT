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
const props = defineProps<{
  title: string
}>()

/**
 * Tooltip + bounce for the collapse button.
 *
 * The collapsed state is read from the button's own aria-label rather than from
 * useDashboard(): that composable is an internal, non-auto-imported util, and the vendor
 * already maintains this exact string ("Collapse sidebar" / "Expand sidebar"), so reading
 * it avoids depending on internals while staying perfectly in sync.
 */
const collapsed = ref(false)

function syncCollapsedState() {
  const label = current?.getAttribute('aria-label') ?? ''
  collapsed.value = /expand/i.test(label)
}

// The vendor mutates aria-label after this component renders, so observing that attribute
// is what catches the flip; no reactive prop is exposed for the collapsed state.
let observer: MutationObserver | undefined
let current: HTMLElement | null = null

/**
 * Called from the template ref, i.e. EVERY time the button is (re)mounted.
 *
 * This is the fix for the tooltip freezing on "Expand" after a few clicks. The :key below
 * remounts the button on every flip, and the observer used to be created once in onMounted -
 * so from the first remount onwards it was watching a DETACHED node. aria-label changes on
 * the live button were never seen, `collapsed` froze, and the tooltip got stuck. (Same
 * failure mode as the old sidebar composable, and the reason the spring was rebuilt in pure
 * CSS: remount + observer do not mix.)
 */
function setCollapseBtn(el: any) {
  const node: HTMLElement | null = el?.$el ?? el ?? null
  observer?.disconnect()
  observer = undefined
  current = node
  if (!node || typeof MutationObserver === 'undefined') return
  observer = new MutationObserver(syncCollapsedState)
  observer.observe(node, { attributes: true, attributeFilter: ['aria-label'] })
  syncCollapsedState()
  // The vendor may set aria-label a tick after mount, so read once more after the DOM settles.
  nextTick(syncCollapsedState)
}

onBeforeUnmount(() => observer?.disconnect())

/** Changing :key remounts the button, which restarts the CSS animation on every click. */
const bounceKey = computed(() => (collapsed.value ? 'expanded' : 'collapsed'))
</script>

<template>
  <UDashboardNavbar>
    <template #leading>
      <UDashboardSidebarCollapse
        :key="bounceKey"
        :ref="setCollapseBtn"
        :class="collapsed ? 'anim-collapse-open' : 'anim-collapse-closed'"
        :title="collapsed ? 'Expand sidebar' : 'Collapse sidebar'"
        :ui="{ base: 'sidebar-collapse-btn hidden lg:flex' }"
      />
    </template>

    <template #trailing>
      <h1 class="truncate text-xl font-bold">{{ title }}</h1>
    </template>

    <template v-if="$slots.actions" #right>
      <slot name="actions" />
    </template>
  </UDashboardNavbar>
</template>

<style>
/* Bounce on the collapse button. Two separate keyframes rather than one direction-agnostic
   animation, so closing and opening each get their own movement. Duration is short (260ms):
   a longer spring reads as sluggish rather than lively. */
@keyframes infra-collapse-closed {
  0%   { transform: translateX(0) scale(1); }
  35%  { transform: translateX(3px) scale(0.88); }
  70%  { transform: translateX(-1.5px) scale(1.06); }
  100% { transform: translateX(0) scale(1); }
}

@keyframes infra-collapse-open {
  0%   { transform: translateX(0) scale(1); }
  35%  { transform: translateX(-3px) scale(0.88); }
  70%  { transform: translateX(1.5px) scale(1.06); }
  100% { transform: translateX(0) scale(1); }
}

.sidebar-collapse-btn.anim-collapse-closed { animation: infra-collapse-closed 260ms cubic-bezier(0.34, 1.56, 0.64, 1); }
.sidebar-collapse-btn.anim-collapse-open  { animation: infra-collapse-open  260ms cubic-bezier(0.34, 1.56, 0.64, 1); }

@media (prefers-reduced-motion: reduce) {
  /* Cancel outright, consistent with the page-load animations in main.css. */
  .sidebar-collapse-btn.anim-collapse-closed,
  .sidebar-collapse-btn.anim-collapse-open {
    animation: none !important;
  }
}
</style>
