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

/**
 * Twinkling star field, modelled on the hero panel of
 * https://changelog-template.nuxt.dev/ - where `#__nuxt > div > section.relative.isolate`
 * lays an absolutely-positioned, pointer-events-none layer of small round dots over the
 * panel, each one twinkling on its own delay.
 *
 * COLOUR: every star uses `var(--ui-primary)`, exactly as the reference does. That is the
 * variable Nuxt UI rewrites when the user picks a different accent, so the field follows the
 * theme automatically - nothing here is a hard-coded colour.
 *
 * THE SEED IS FIXED, and that is not cosmetic. The positions are generated in setup(), which
 * runs on the server for SSR and again in the browser on hydration. With `Math.random()` the
 * two runs would disagree and Vue would report a hydration mismatch (and in dev, bloat the
 * console or patch the DOM). A seeded PRNG produces the identical field both times.
 */
function mulberry32(seed: number) {
  return () => {
    seed |= 0
    seed = (seed + 0x6d2b79f5) | 0
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

const STARS = (() => {
  const rnd = mulberry32(20261004)
  return Array.from({ length: 46 }, () => ({
    left: (rnd() * 100).toFixed(2),
    top: (rnd() * 100).toFixed(2),
    // 1px to 3px, matching the reference's measured range of 1.04 - 3.00
    size: (1 + rnd() * 2).toFixed(2),
    // 0 - 5s, against the reference's measured 0.93 - 4.88
    delay: (rnd() * 5).toFixed(2),
    duration: (1.6 + rnd() * 2.4).toFixed(2)
  }))
})()

/** e.g. "Wednesday, 2 October 2026" — en-GB to match the rest of the English UI. */
const today = computed(() => now.value.toLocaleDateString('en-GB', {
  weekday: 'long', day: 'numeric', month: 'long', year: 'numeric'
}))
</script>

<template>
  <div class="relative overflow-hidden rounded-xl border border-default bg-elevated">
    <!-- Decorative layer. Decorative only, so it is hidden from assistive tech. The wrapper
         matches the reference: absolute inset-0, pointer-events-none, overflow-hidden, at z-0
         behind the content which sits at z-10. Two elements live here, in this order:
         the glow first, then the stars, so the stars paint on top of the glow. -->
    <div aria-hidden="true" class="pointer-events-none absolute inset-0 z-0 overflow-hidden">
      <!-- The soft accent glow from the reference template. It is NOT a CSS gradient: it is a
           plain circle of the accent colour with a very large blur, so the "gradient" is just
           the blur falloff. Same recipe as the source: `absolute right-[-120px] top-1/2
           -translate-y-1/2 size-[420px] rounded-full bg-primary blur-[300px]`.
           The reference uses `-right-1/2`, which is `right: -50%` of the CONTAINING BLOCK, so
           it only works because its panel is much wider than the orb. The banner is far
           narrower, so the offset is given in pixels instead - otherwise the orb would sit
           entirely outside the banner and be clipped into nothing. The banner's own
           overflow-hidden does the clipping, which is what makes the glow read as light
           bleeding in from the right edge rather than as a floating blob.
           `bg-primary` is a semantic token, so the glow follows the user's accent exactly as
           the stars do. -->
      <div class="absolute right-[-120px] top-1/2 size-[420px] -translate-y-1/2 rounded-full bg-primary blur-[300px]" />
      <span v-for="(s, i) in STARS" :key="i" class="infra-star"
            :style="{ left: s.left + '%', top: s.top + '%', '--star-size': s.size + 'px',
                      '--twinkle-delay': s.delay + 's', '--twinkle-duration': s.duration + 's' }" />
    </div>

    <div class="relative z-10 flex flex-wrap items-center gap-5 p-5 sm:p-6">
      <!-- Avatar: initials only, no remote image — the office server has no internet.
           The breathing glow lives on THIS WRAPPER, not on the avatar itself, and that is
           load-bearing rather than cosmetic: the avatar carries `ring-4 ring-primary/15`,
           and Tailwind's ring is implemented as a box-shadow. Animating box-shadow on the
           avatar would therefore wipe the ring out on the very first frame of the pulse. A
           wrapper gets its own stacking box, so the glow breathes around a ring that stays
           intact. -->
      <span class="infra-avatar-glow relative inline-flex shrink-0 rounded-full">
        <UAvatar :alt="displayName" size="xl"
                 class="bg-primary text-inverted ring-4 ring-primary/15">
          {{ initials }}
        </UAvatar>
      </span>

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

<style scoped>
/* Twinkling star field, mirroring the hero panel of https://changelog-template.nuxt.dev/.
   Deliberately opacity-only, like the reference: animating transform or size here would
   re-rasterise 46 elements every frame for no visual gain, and opacity is compositor-only. */
.infra-star {
  position: absolute;
  width: var(--star-size);
  height: var(--star-size);
  border-radius: 50%;
  /* --ui-primary, NOT a literal colour: Nuxt UI rewrites this variable whenever the user
     picks a different accent, so the whole field follows the theme on its own. */
  background-color: var(--ui-primary);
  transform: translate(-50%, -50%);
  animation: infra-twinkle var(--twinkle-duration) ease-in-out infinite;
  animation-delay: var(--twinkle-delay);
}

@keyframes infra-twinkle {
  0%,
  100% {
    opacity: 0.2;
  }
  50% {
    opacity: 1;
  }
}

/* Under the OS "reduce motion" setting the stars stop twinkling but stay visible at a fixed
   opacity, so the banner keeps its texture instead of blinking - the same rule the .anim-*
   utilities in main.css already follow. */
/* Breathing glow around the welcome avatar.
   "Breathing" means the halo expands outward and fades back in, on a slow cycle, rather than
   blinking — the easing is a symmetric ease-in-out so the inhale and the exhale take the same
   time and the loop has no visible seam.

   THE COLOUR IS NOT HARDCODED. `color-mix()` is fed `var(--ui-primary)`, the same variable
   the stars and the glow orb use, so the halo follows the user's accent automatically. A
   literal hex here would have been the one thing in this banner that stopped matching the
   theme. */
.infra-avatar-glow {
  animation: infra-breathe 4.5s cubic-bezier(0.4, 0, 0.6, 1) infinite;
}

@keyframes infra-breathe {
  0%,
  100% {
    box-shadow:
      0 0 0 0.25rem color-mix(in oklab, var(--ui-primary) 16%, transparent),
      0 0 0 0 color-mix(in oklab, var(--ui-primary) 0%, transparent);
  }
  50% {
    box-shadow:
      0 0 0 0.3rem color-mix(in oklab, var(--ui-primary) 28%, transparent),
      0 0 26px 9px color-mix(in oklab, var(--ui-primary) 42%, transparent);
  }
}

@media (prefers-reduced-motion: reduce) {
  .infra-star {
    animation: none !important;
    opacity: 0.55;
  }

  /* The avatar keeps a STATIC halo rather than losing the glow entirely - the accent ring
     still reads as intentional, it just stops pulsing. Verified with
     cdp Emulation.setEmulatedMedia: animationName must read 'none' under this media query. */
  .infra-avatar-glow {
    animation: none !important;
    box-shadow:
      0 0 0 0.25rem color-mix(in oklab, var(--ui-primary) 20%, transparent),
      0 0 16px 4px color-mix(in oklab, var(--ui-primary) 32%, transparent);
  }
}
</style>
