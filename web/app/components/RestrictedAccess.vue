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
 * MOTION NOTES
 * The lock is an inline SVG, not a UIcon, on purpose: Nuxt Icon renders a CSS mask, so a single span
 * has no inner parts to animate. Here the shackle is its own <g>, which lets it drop into place once
 * and then tug at its own loop - the one motion that actually says "this is locked" instead of
 * decorating it. Everything else is a nudge on hover (icons lean toward where they lead), never a size
 * or colour change to the chrome around them, and all of it drops out under prefers-reduced-motion.
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
      <!-- Two soft accent washes bleeding in from opposite corners, clipped by this card. They drift
           a few pixels over 20s so the surface is never completely static; the drift is on the
           wrapper so it cannot fight the blur or the layout. -->
      <div aria-hidden="true" class="ra-drift pointer-events-none absolute -right-20 -top-24 size-56 rounded-full bg-primary/15 blur-3xl" />
      <div aria-hidden="true" class="ra-drift ra-drift-slow pointer-events-none absolute -bottom-28 -left-24 size-56 rounded-full bg-info/10 blur-3xl" />

      <div class="relative flex flex-col items-center">
        <!-- Lock mark: the outer span takes the intro pop, the inner span takes the slow breathing
             loop, so no two animations ever own the same property. -->
        <span class="ra-mark relative grid size-20 place-items-center rounded-2xl bg-primary/10 text-primary ring-1 ring-inset ring-primary/25">
          <span aria-hidden="true" class="ra-halo absolute inset-0 rounded-2xl ring-1 ring-primary/40" />
          <span class="ra-breathe grid place-items-center">
            <svg class="ra-lock size-9" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <rect x="4.6" y="10.4" width="14.8" height="9.8" rx="2.6" />
              <g class="ra-shackle">
                <path d="M8.1 10.4V7.6a3.9 3.9 0 0 1 7.8 0v2.8" />
              </g>
              <circle cx="12" cy="15.1" r="1.15" fill="currentColor" stroke="none" />
              <path d="M12 16.2v1.7" />
            </svg>
          </span>
        </span>

        <span class="ra-chip mt-5 inline-flex items-center gap-1.5 rounded-full bg-default/40 px-2.5 py-1 text-[11px] font-semibold uppercase text-muted">
          <span aria-hidden="true" class="ra-dot size-1.5 rounded-full bg-warning" />
          Admin only
        </span>

        <h2 id="ra-title" class="ra-title mt-3 text-xl font-semibold text-highlighted sm:text-2xl">{{ title }}</h2>
        <p class="ra-desc mt-2 max-w-md text-sm leading-relaxed text-muted">{{ description }}</p>

        <!-- What is behind the wall. Left-aligned rows inside a centred column: this is the part that
             turns "denied" into "here is what you are missing". -->
        <ul v-if="features.length" class="ra-list mt-6 w-full max-w-sm space-y-1.5">
          <li
            v-for="(feature, i) in features"
            :key="feature.label"
            class="ra-row group flex items-center gap-2.5 rounded-lg border border-default/60 bg-elevated/70 px-3 py-2 text-left text-sm text-default"
            :style="{ animationDelay: `${140 + i * 70}ms` }"
          >
            <UIcon :name="feature.icon" class="ra-row-ico size-4 shrink-0 text-primary" aria-hidden="true" />
            <span class="min-w-0 truncate">{{ feature.label }}</span>
          </li>
        </ul>

        <!-- The role that caused the block, stated rather than implied: "you are signed in as X". -->
        <p class="ra-session mt-6 flex flex-wrap items-center justify-center gap-x-2 gap-y-1 text-xs text-muted">
          <span>Signed in as</span>
          <span class="font-medium text-default">{{ user?.fullName || user?.username }}</span>
          <span aria-hidden="true">·</span>
          <span class="rounded-md bg-warning/10 px-1.5 py-0.5 font-medium text-warning">{{ roleLabel }}</span>
        </p>

        <div class="ra-actions mt-6 flex flex-wrap items-center justify-center gap-2">
          <UButton to="/" color="primary" label="Back to dashboard" class="ra-back">
            <template #leading>
              <UIcon name="i-lucide-layout-dashboard" class="ra-ico size-4" />
            </template>
          </UButton>
          <UButton color="neutral" variant="ghost" label="Sign out" class="ra-out" @click="signOut">
            <template #leading>
              <UIcon name="i-lucide-log-out" class="ra-ico size-4" />
            </template>
          </UButton>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
/* ---------------------------------------------------------------- entrance */
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

/* Children arrive in reading order; each is a short fade-up, never a slide. */
.ra-chip,
.ra-title,
.ra-desc,
.ra-list,
.ra-row,
.ra-session,
.ra-actions {
  animation: ra-fade-up 420ms cubic-bezier(0.22, 1, 0.36, 1) both;
}

.ra-chip    { animation-delay: 120ms; }
.ra-title   { animation-delay: 170ms; }
.ra-desc    { animation-delay: 220ms; }
.ra-list    { animation-delay: 260ms; }
.ra-row     { animation-delay: calc(140ms + var(--ra-i, 0) * 70ms); }
.ra-session { animation-delay: 360ms; }
.ra-actions { animation-delay: 410ms; }

@keyframes ra-fade-up {
  from {
    opacity: 0;
    transform: translateY(7px);
  }
  to {
    opacity: 1;
    transform: none;
  }
}

/* ---------------------------------------------------------------- lock mark */
.ra-mark {
  animation: ra-mark-in 520ms cubic-bezier(0.34, 1.56, 0.64, 1) 60ms both;
}

@keyframes ra-mark-in {
  from { transform: scale(0.86); }
  60%  { transform: scale(1.04); }
  to   { transform: scale(1); }
}

/* A slow breath on the inner span: scale only, so the badge's box never moves. */
.ra-breathe {
  animation: ra-breathe 4.6s ease-in-out 1s infinite;
}

@keyframes ra-breathe {
  0%,
  100% { transform: scale(1); }
  50%   { transform: scale(1.045); }
}

/* The shackle drops into its body once, then tugs every few seconds - the tug is the part that reads
   as "locked" without a single word changing. The drop and the tug live on two nested groups so
   neither animation has to share a property. */
.ra-shackle {
  animation: ra-shackle-drop 900ms cubic-bezier(0.34, 1.56, 0.64, 1) 320ms both;
}

@keyframes ra-shackle-drop {
  0%   { transform: translateY(-3.4px); }
  55%  { transform: translateY(0.7px); }
  78%  { transform: translateY(-0.35px); }
  100% { transform: translateY(0); }
}

.ra-shackle path {
  transform-box: fill-box;
  transform-origin: 50% 100%;
  animation: ra-shackle-tug 5.6s ease-in-out 1.6s infinite;
}

@keyframes ra-shackle-tug {
  0%,
  86%,
  100% { transform: translateY(0) rotate(0deg); }
  90%   { transform: translateY(-1.7px) rotate(-1.2deg); }
  94%   { transform: translateY(0.4px) rotate(0.5deg); }
}

/* Hovering the card makes the padlock rattle once: same shackle, shorter and sharper. */
.ra-card:hover .ra-shackle path {
  animation: ra-rattle 620ms cubic-bezier(0.36, 0.07, 0.19, 0.97) both;
}

@keyframes ra-rattle {
  0%,
  100% { transform: translateX(0) rotate(0deg); }
  18%   { transform: translateX(-1.6px) rotate(-1.6deg); }
  38%   { transform: translateX(1.6px) rotate(1.6deg); }
  58%   { transform: translateX(-1.1px) rotate(-1deg); }
  78%   { transform: translateX(0.7px) rotate(0.6deg); }
}

/* One halo pulse, on its own ring so it never displaces the mark. */
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

/* ---------------------------------------------------------------- chip + label */
/* The dot is the only thing pulsing here; the chip itself only breathes on hover, and only its
   tracking - so nothing reflows. */
.ra-chip {
  transition: background-color 280ms ease, letter-spacing 320ms cubic-bezier(0.22, 1, 0.36, 1);
}

.ra-card:hover .ra-chip {
  letter-spacing: 0.12em;
}

.ra-dot {
  animation: ra-dot 2.4s ease-in-out infinite;
}

@keyframes ra-dot {
  0%,
  100% { opacity: 1; transform: scale(1); }
  50%   { opacity: 0.45; transform: scale(0.82); }
}

/* The heading deliberately has NO hover transform: its entrance fade-up uses fill-mode `both`, and a
   filled animation's final `transform: none` outranks any hover rule, so the lift silently never
   happened. Measured, not assumed - the rule was removed rather than left as dead CSS. */
/* ---------------------------------------------------------------- feature rows */
.ra-row {
  transition: background-color 260ms ease, border-color 260ms ease;
}

.ra-card:hover .ra-row {
  border-color: color-mix(in oklab, var(--ui-primary) 30%, transparent);
}

.ra-row:hover {
  background-color: color-mix(in oklab, var(--ui-primary) 10%, transparent);
}

.ra-row-ico {
  transition: transform 280ms cubic-bezier(0.22, 1, 0.36, 1);
}

.ra-row:hover .ra-row-ico {
  transform: translateX(3px);
}

/* ---------------------------------------------------------------- buttons */
/* The chrome of a Nuxt UI button stays exactly where it is: only the leading icon leans toward where
   the button leads. Transform, not margin, so nothing reflows. */
.ra-ico {
  transition: transform 300ms cubic-bezier(0.22, 1, 0.36, 1);
}

.ra-back:hover .ra-ico {
  transform: translateX(3px);
}

.ra-out:hover .ra-ico {
  transform: translateX(2px) rotate(-6deg);
}

/* Sign out turns red on hover: it is the destructive action on this card, and up to now it was the
   only control that gave no signal about that. Text and icon both follow `currentColor`, so one colour
   declaration covers both. Guarded to real pointers - a tap on a touch screen leaves :hover stuck on. */
.ra-out {
  transition: color 220ms ease, background-color 220ms ease;
}

@media (hover: hover) and (pointer: fine) {
  .ra-out:not(:disabled):hover {
    color: var(--ui-error);
    background-color: color-mix(in oklab, var(--ui-error) 12%, transparent);
  }
}

/* The two corner washes drift a few pixels so the glass is never fully static. */
.ra-drift {
  animation: ra-drift 20s ease-in-out infinite alternate;
}

.ra-drift-slow {
  animation-duration: 26s;
  animation-direction: alternate-reverse;
}

@keyframes ra-drift {
  from { transform: translate3d(0, 0, 0); }
  to   { transform: translate3d(-10px, 8px, 0); }
}

/* ---------------------------------------------------------------- a11y */
@media (prefers-reduced-motion: reduce) {
  .ra-card,
  .ra-mark,
  .ra-chip,
  .ra-title,
  .ra-desc,
  .ra-list,
  .ra-row,
  .ra-session,
  .ra-actions,
  .ra-breathe,
  .ra-shackle,
  .ra-shackle path,
  .ra-card:hover .ra-shackle path,
  .ra-halo,
  .ra-dot,
  .ra-drift {
    animation: none !important;
  }
  .ra-halo { opacity: 0.35; }
  .ra-row:hover .ra-row-ico,
  .ra-back:hover .ra-ico,
  .ra-out:hover .ra-ico { transform: none; }
}
</style>