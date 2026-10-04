<script setup lang="ts">
import type { DropdownMenuItem } from '@nuxt/ui'

/**
 * Sidebar footer user menu, adapted from the Nuxt dashboard template.
 *
 * Two deviations from the template, both deliberate:
 *  - the avatar is generated from the initials instead of a remote image URL, because
 *    the production server has no internet access and a remote avatar renders blank;
 *  - "Templates" / "Deploy to Vercel" entries are dropped; they link to nuxt.com pages
 *    that mean nothing in an internal app.
 *
 * What was kept is what HIRO asked for: free primary-colour switching and a
 * light/dark toggle, both applied to the live appConfig so they take effect instantly.
 */
defineProps<{ collapsed?: boolean }>()

const colorMode = useColorMode()
/** Bound so picking Light or Dark can close the whole menu - HIRO's request. Without this the
 *  menu stayed open over the page after the theme flipped, hiding the change it just made. */
const menuOpen = ref(false)
const appConfig = useAppConfig()
const { user, logout } = useAuth()
const router = useRouter()

const colors = ['green', 'red', 'orange', 'amber', 'yellow', 'lime', 'emerald', 'teal', 'cyan', 'sky', 'blue', 'indigo', 'violet', 'purple', 'fuchsia', 'pink', 'rose']
const neutrals = ['slate', 'gray', 'zinc', 'neutral', 'stone', 'taupe', 'mauve', 'mist', 'olive']

// Initials for the sidebar avatar; no external image request.
const initials = computed(() => {
  const name = user.value?.fullName || user.value?.username || '?'
  const parts = String(name).trim().split(/\s+/)
  if (parts.length > 1) return (parts[0]![0]! + parts[parts.length - 1]![0]!)
  return String(name).slice(0, 2).toUpperCase()
})

const { restore, setPrimary, setNeutral } = useThemeChoice()

// Applied on every page load so a colour picked earlier survives a refresh.
onMounted(() => { restore() })

const displayName = computed(() => user.value?.fullName || user.value?.username || 'User')
const roleLabel = computed(() => (user.value?.roles?.[0]?.name) || '')

function doLogout() {
  logout()
  router.push('/login')
}

const items = computed<DropdownMenuItem[][]>(() => ([
  [{
    type: 'label',
    label: displayName.value,
    // Rendered as the button content instead of an avatar image.
    description: roleLabel.value
  }],
  [{
    label: 'Accent Color',
    icon: 'i-lucide-palette',
    children: colors.map(color => ({
      label: color,
      chip: color,
      slot: 'chip',
      checked: appConfig.ui.colors.primary === color,
      type: 'checkbox',
      onSelect: (e: Event) => {
        e.preventDefault()
        setPrimary(color)
      }
    }))
  }, {
    label: 'Neutral Color',
    icon: 'i-lucide-contrast',
    children: neutrals.map(color => ({
      label: color,
      chip: color === 'neutral' ? 'old-neutral' : color,
      slot: 'chip',
      type: 'checkbox',
      checked: appConfig.ui.colors.neutral === color,
      onSelect: (e: Event) => {
        e.preventDefault()
        setNeutral(color)
      }
    }))
  }],
  [{
    label: 'Appearance',
    icon: 'i-lucide-sun-moon',
    children: [{
      label: 'Light',
      icon: 'i-lucide-sun',
      type: 'checkbox',
      checked: colorMode.value === 'light',
      onSelect(e: Event) {
        e.preventDefault()
        colorMode.preference = 'light'
        menuOpen.value = false
      }
    }, {
      label: 'Dark',
      icon: 'i-lucide-moon',
      type: 'checkbox',
      checked: colorMode.value === 'dark',
      onSelect(e: Event) {
        e.preventDefault()
        colorMode.preference = 'dark'
        menuOpen.value = false
      }
    }]
  }],
  [{
    label: 'Sign Out',
    icon: 'i-lucide-log-out',
    // Red, and it STAYS red on hover. The vendor's own classes are
    // `text-default data-highlighted:text-highlighted` with the leading icon at
    // `text-dimmed group-data-highlighted:text-default`, so without an override the item would
    // turn white-ish on hover and the "destructive" reading would be lost exactly when the
    // pointer is over it.
    //
    // `signout-item` is a marker, not styling: the ICON is coloured by a NON-scoped rule at
    // the bottom of this file. An earlier attempt used `[&_svg]:text-error` here, which
    // silently did nothing - Iconify renders `<span class="iconify ...">`, never an `<svg>`,
    // so the selector matched no element at all. Beating the vendor's hover rule also needs
    // more than a plain descendant selector: `group-data-highlighted:text-default` compiles
    // with the group attribute in the chain and outranks `[&_.iconify]`, hence `!important`
    // on the rule rather than another attempt at specificity arithmetic.
    class: 'signout-item text-error data-highlighted:text-error data-[state=open]:text-error data-highlighted:before:bg-error/10',
    onSelect: doLogout
  }]
]))
</script>

<template>
  <!-- Fixes the avatar shifting sideways when the sidebar collapses.
       MEASURED before/after: in the 64px collapsed sidebar the avatar's centre sat at
       x=36 while the sidebar's centre is x=32 — 4px off, because the footer has px-4
       (16px) and the button's own p-1.5 is not fully absorbed by `:square`. Expanded, the
       avatar is correctly left-aligned at x=26, so nothing changes there.

       A -ms-1 (4px) shift on the wrapper is the whole fix. Bigger corrections were tried
       and made it worse: -mx-4 + p-0 + justify-center pushed the avatar to x=14, because
       the avatar is not in a centring flex context inside UButton, so justify-center does
       nothing and the negative margin just drags it off to the left. -->
  <div :class="collapsed ? '-ms-1' : ''">
    <UDropdownMenu
      v-model:open="menuOpen"
      :items="items"
      :content="{ align: 'center', collisionPadding: 12 }"
      :ui="{ content: collapsed ? 'w-48' : 'w-(--reka-dropdown-menu-trigger-width)' }"
    >
      <UButton
        color="neutral"
        variant="ghost"
        block
        :square="collapsed"
        class="data-[state=open]:bg-elevated"
        :ui="{ trailingIcon: 'text-dimmed' }"
      >
        <UAvatar
          :alt="displayName"
          size="sm"
          class="bg-primary text-inverted"
        >
          {{ initials }}
        </UAvatar>

      <span v-if="!collapsed" class="truncate text-left">
          <span class="block truncate text-sm font-medium">{{ displayName }}</span>
          <span v-if="roleLabel" class="block truncate text-xs text-dimmed">{{ roleLabel }}</span>
        </span>

        <UIcon v-if="!collapsed" name="i-lucide-chevrons-up-down" class="ml-auto size-3.5 text-dimmed" />
      </UButton>

      <template #chip-leading="{ item }">
        <div class="inline-flex items-center justify-center shrink-0 size-5">
          <span
            class="rounded-full ring ring-bg bg-(--chip-light) dark:bg-(--chip-dark) size-2"
            :style="{
              '--chip-light': `var(--color-${(item as any).chip}-500)`,
              '--chip-dark': `var(--color-${(item as any).chip}-400)`
            }"
          />
        </div>
      </template>
    </UDropdownMenu>
  </div>
</template>

<style>
/* Sign Out icon colour.
   Iconify renders `<span class="iconify i-lucide:log-out ...">`, not an `<svg>` - so the
   obvious `[&_svg]` selector matches nothing. The rule targets `.iconify` instead, and uses
   `!important` because the vendor's own `group-data-highlighted:text-default` on the icon
   compiles to a selector with the group attribute in the chain, which outranks any plain
   descendant selector written from here.
   Non-scoped on purpose: the dropdown content is teleported to <body>, so a scoped rule
   could never reach it. Keyed to the `signout-item` marker, so it cannot leak to any other
   menu item in the app. */
.signout-item .iconify,
.signout-item[data-highlighted] .iconify,
.signout-item[data-state='open'] .iconify {
  color: var(--ui-error) !important;
}
</style>
