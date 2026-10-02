<script setup lang="ts">
/**
 * Dashboard welcome banner.
 *
 * Lives in the page BODY, not in PageHeader — the navbar is locked to a fixed
 * h-(--ui-header-height) of 4rem = 64px and cannot host this much content
 * (see the note in PageHeader.vue).
 *
 * Everything shown is derived from the logged-in user at RENDER time, so it always
 * reflects whoever is signed in. There is no hard-coded name anywhere in this file.
 */
const { user, roleNames } = useAuth()

const displayName = computed(() => user.value?.fullName || user.value?.username || '')

/** Initials for the avatar, same rule as UserMenu so the two never disagree. */
const initials = computed(() => {
  const parts = String(displayName.value).trim().split(/\s+/)
  if (parts.length > 1) return (parts[0]![0]! + parts[parts.length - 1]![0]!)
  return displayName.value.slice(0, 2).toUpperCase()
})

/**
 * Time-of-day greeting. Computed once on mount and then on a timer, NOT from `new Date()`
 * directly in the template: the latter is a hidden dependency that only refreshes when
 * something else happens to re-render, so the greeting could silently go stale.
 */
const now = ref(new Date())
let timer: ReturnType<typeof setInterval> | undefined
onMounted(() => { timer = setInterval(() => { now.value = new Date() }, 60_000) })
onUnmounted(() => { if (timer) clearInterval(timer) })

const greeting = computed(() => {
  const h = now.value.getHours()
  if (h < 11) return 'Good morning'
  if (h < 15) return 'Good afternoon'
  if (h < 18) return 'Good evening'
  return 'Good night'
})

/** e.g. "Wednesday, 2 October 2026" — en-GB to match the rest of the English UI. */
const today = computed(() => now.value.toLocaleDateString('en-GB', {
  weekday: 'long', day: 'numeric', month: 'long', year: 'numeric'
}))
</script>

<template>
  <div class="relative overflow-hidden rounded-xl border border-default bg-elevated">
    <!-- Soft accent wash. Decorative only, so it is hidden from assistive tech. -->
    <div aria-hidden="true"
         class="pointer-events-none absolute -right-16 -top-24 size-64 rounded-full bg-primary/10 blur-3xl" />
    <div aria-hidden="true"
         class="pointer-events-none absolute -bottom-28 right-24 size-56 rounded-full bg-primary/5 blur-3xl" />

    <div class="relative flex flex-wrap items-center gap-5 p-5 sm:p-6">
      <!-- Avatar: initials only, no remote image — the office server has no internet. -->
      <UAvatar :alt="displayName" size="xl"
               class="shrink-0 bg-primary text-inverted ring-4 ring-primary/15">
        {{ initials }}
      </UAvatar>

      <div class="min-w-0 flex-1">
        <p class="text-sm text-muted">{{ greeting }},</p>
        <h2 class="truncate text-2xl font-bold sm:text-3xl">{{ displayName }}</h2>

        <div v-if="roleNames.length" class="mt-2 flex flex-wrap items-center gap-1.5">
          <UBadge v-for="role in roleNames" :key="role" color="primary" variant="soft" size="sm">
            {{ role }}
          </UBadge>
        </div>
      </div>

      <div class="hidden shrink-0 text-right sm:block">
        <p class="text-xs uppercase tracking-wide text-dimmed">Today</p>
        <p class="text-sm font-medium">{{ today }}</p>
      </div>
    </div>
  </div>
</template>
