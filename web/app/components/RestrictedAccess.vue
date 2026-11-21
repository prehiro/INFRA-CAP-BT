<script setup lang="ts">
/**
 * The state a non-Admin sees on an Admin-only page (Log Audit, User Management).
 *
 * WHY THIS EXISTS
 * Both pages used to answer the role check with a single `UAlert variant="soft"`: one flat yellow bar
 * that said "Restricted access" and nothing else. It told the user they were blocked but not what they
 * were missing, who they were signed in as, or what to do next - so it read as a dead end rather than
 * a permission boundary.
 *
 * This is the replacement: the page name and what the page holds, the current role that caused the
 * block, and two ways forward (dashboard / sign out). It is a component rather than page markup
 * because the state is identical on both pages and must not drift apart.
 *
 * Deliberately quiet: one accent wash, one halo pulse, no hover motion, and every animation drops out
 * under prefers-reduced-motion.
 */
withDefaults(defineProps<{
  /** Page name shown in the heading, e.g. "User Management". */
  title?: string
  /** One line explaining who may open the page. */
  description?: string
  /** What the page contains, so the block reads as information rather than a dead end. */
  features?: { icon: string, label: string }[]
}>(), {
  title: 'Restricted access',
  description: 'Only an Admin can open this page.',
  features: () => []
})

const { user, roleNames, logout } = useAuth()

/** "Staff" / "Staff, Manager" - the roles that produced the block, straight from the session. */
const roleLabel = computed(() => (roleNames.value.length ? roleNames.value.join(', ') : 'No role'))

function signOut() {
  logout()
  navigateTo('/login')
}
</script>

<template>
  <div class="flex w-full justify-center px-2 py-4 sm:py-8">
    <section
      class="ra-card relative w-full max-w-2xl overflow-hidden rounded-2xl border border-default bg-elevated/60 px-6 py-9 text-center backdrop-blur-sm sm:px-10 sm:py-11"
      aria-labelledby="ra-title"
    >
      <!-- Two soft accent washes bleeding in from opposite corners, clipped by this card. Same idiom
           as the delete dialog and the stat cards; nothing animated behind the text. -->
      <div aria-hidden="true" class="pointer-events-none absolute -right-20 -top-24 size-56 rounded-full bg-primary/15 blur-3xl" />
      <div aria-hidden="true" class="pointer-events-none absolute -bottom-28 -left-24 size-56 rounded-full bg-info/10 blur-3xl" />

      <div class="relative flex flex-col items-center">
        <!-- Lock mark with one halo pulse. The halo is a separate absolutely-positioned ring so the
             mark itself never moves - no layout shift, no text jitter. -->
        <span class="relative grid size-20 place-items-center rounded-2xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/25">
          <span aria-hidden="true" class="ra-halo absolute inset-0 rounded-2xl ring-1 ring-primary/40" />
          <UIcon name="i-lucide-lock" class="size-9" aria-hidden="true" />
        </span>

        <span class="mt-5 inline-flex items-center gap-1.5 rounded-full bg-default/40 px-2.5 py-1 text-[11px] font-semibold uppercase tracking-wider text-muted">
          <span aria-hidden="true" class="size-1.5 rounded-full bg-warning" />
          Admin only
        </span>

        <h2 id="ra-title" class="mt-3 text-xl font-semibold tracking-tight text-highlighted sm:text-2xl">{{ title }}</h2>
        <p class="mt-2 max-w-md text-sm leading-relaxed text-muted">{{ description }}</p>

        <!-- What is behind the wall. Left-aligned rows inside a centred column: this is the part that
             turns "denied" into "here is what you are missing". -->
        <ul v-if="features.length" class="mt-6 w-full max-w-sm space-y-1.5">
          <li
            v-for="feature in features"
            :key="feature.label"
            class="flex items-center gap-2.5 rounded-lg border border-default/60 bg-elevated/70 px-3 py-2 text-left text-sm text-default"
          >
            <UIcon :name="feature.icon" class="size-4 shrink-0 text-primary" aria-hidden="true" />
            <span class="min-w-0 truncate">{{ feature.label }}</span>
          </li>
        </ul>

        <!-- The role that caused the block, stated rather than implied: "you are signed in as X". -->
        <p class="mt-6 flex flex-wrap items-center justify-center gap-x-2 gap-y-1 text-xs text-muted">
          <span>Signed in as</span>
          <span class="font-medium text-default">{{ user?.fullName || user?.username }}</span>
          <span aria-hidden="true">·</span>
          <span class="rounded-md bg-warning/10 px-1.5 py-0.5 font-medium text-warning">{{ roleLabel }}</span>
        </p>

        <div class="mt-6 flex flex-wrap items-center justify-center gap-2">
          <UButton to="/" color="primary" label="Back to dashboard" class="ra-back">
            <template #leading>
              <UIcon name="i-lucide-layout-dashboard" class="size-4" />
            </template>
          </UButton>
          <UButton color="neutral" variant="ghost" label="Sign out" @click="signOut">
            <template #leading>
              <UIcon name="i-lucide-log-out" class="size-4" />
            </template>
          </UButton>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.ra-card {
  animation: ra-rise 460ms cubic-bezier(0.22, 1, 0.36, 1) both;
}

@keyframes ra-rise {
  from {
    opacity: 0;
    transform: translateY(10px) scale(0.994);
  }
  to {
    opacity: 1;
    transform: none;
  }
}

/* One slow breath on the halo: opacity and scale only, so it never touches layout. */
.ra-halo {
  animation: ra-halo 3.4s ease-out infinite;
}

@keyframes ra-halo {
  0% {
    opacity: 0.55;
    transform: scale(1);
  }
  70%,
  100% {
    opacity: 0;
    transform: scale(1.28);
  }
}

@media (prefers-reduced-motion: reduce) {
  .ra-card,
  .ra-halo {
    animation: none !important;
  }
  .ra-halo {
    opacity: 0.35;
  }
}
</style>